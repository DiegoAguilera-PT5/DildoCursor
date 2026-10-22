using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DildoCursor
{
    internal static class Program
    {
        const string MutexName = @"Local\DildoCursor_SingleInstance_6f2a";
        public const string QuitEventName = @"Local\DildoCursor_Quit_6f2a";

        [STAThread]
        static int Main(string[] args)
        {
            foreach (var a in args)
            {
                string s = a.TrimStart('-', '/').ToLowerInvariant();
                if (s == "uninstall" || s == "desinstalar") return UninstallSilent();
                if (s == "quit" || s == "salir") return SignalQuit();
                if (s == "restore" || s == "restaurar") { SafeRestore(); return 0; }
            }

            // Hay que pedir DPI awareness antes de crear cualquier ventana, si no Windows
            // escala el overlay y el sprite queda corrido respecto del puntero real.
            try { Native.SetProcessDPIAware(); } catch { }

            bool isFirst;
            using (var mtx = new Mutex(true, MutexName, out isFirst))
            {
                if (!isFirst) return 0;   // ya hay una instancia corriendo

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Red de seguridad: si algo explota, no dejamos al usuario sin cursor.
                AppDomain.CurrentDomain.UnhandledException += delegate { SafeRestore(); };
                AppDomain.CurrentDomain.ProcessExit += delegate { SafeRestore(); };
                Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
                {
                    SafeRestore();
                    MessageBox.Show("DildoCursor se cerro por un error:\n\n" + e.Exception.Message,
                        "DildoCursor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                };
                SystemEvents.SessionEnding += delegate { SafeRestore(); };

                var cfg = Config.Load();
                try
                {
                    Application.Run(new Overlay(cfg));
                }
                finally
                {
                    SafeRestore();
                    GC.KeepAlive(mtx);
                }
            }
            return 0;
        }

        static void SafeRestore()
        {
            try { CursorHider.Restore(); } catch { }
        }

        /// <summary>Le pide a la instancia en ejecucion que cierre ordenadamente.</summary>
        static int SignalQuit()
        {
            try
            {
                EventWaitHandle ev;
                if (!EventWaitHandle.TryOpenExisting(QuitEventName, out ev)) return 1;  // no habia nada corriendo
                using (ev) ev.Set();
                return 0;
            }
            catch { return 1; }
        }

        static int UninstallSilent()
        {
            Startup.Disable();
            SafeRestore();
            try { if (Directory.Exists(Config.Dir)) Directory.Delete(Config.Dir, true); }
            catch { }
            return 0;
        }
    }
}
