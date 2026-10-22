using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DildoCursorSetup
{
    /// <summary>
    /// Instalador de un solo archivo. Lleva DildoCursor.exe adentro como recurso y lo
    /// deja instalado por usuario, sin pedir permisos de administrador.
    ///
    /// El mismo ejecutable hace de desinstalador: se copia junto al programa y Windows
    /// lo llama con /uninstall desde "Aplicaciones instaladas".
    ///
    ///   (sin argumentos)  instala mostrando una ventana
    ///   /silent           instala sin preguntar nada
    ///   /uninstall        desinstala
    /// </summary>
    internal static class Program
    {
        public const string AppName = "DildoCursor";
        public const string Version = "1.0";

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string UnKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DildoCursor";
        const string QuitEvent = @"Local\DildoCursor_Quit_6f2a";

        public static string Dest
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            }
        }

        public static string AppExe { get { return Path.Combine(Dest, "DildoCursor.exe"); } }
        public static string UninstExe { get { return Path.Combine(Dest, "desinstalar.exe"); } }

        [STAThread]
        static int Main(string[] args)
        {
            bool silent = false, uninstall = false;
            foreach (string a in args)
            {
                string s = a.TrimStart('-', '/').ToLowerInvariant();
                if (s == "silent" || s == "s") silent = true;
                if (s == "uninstall" || s == "u" || s == "desinstalar") uninstall = true;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (uninstall) return Uninstall(silent);
            if (silent) return Install(true, true) ? 0 : 1;

            using (var f = new SetupForm()) { Application.Run(f); }
            return 0;
        }

        // ------------------------------------------------------------------ instalar

        public static bool Install(bool autostart, bool shortcut)
        {
            try
            {
                StopRunning();
                Directory.CreateDirectory(Dest);

                ExtractResource("DildoCursor.exe", AppExe);
                File.Copy(CurrentExe(), UninstExe, true);

                if (autostart)
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                        if (k != null) k.SetValue(AppName, "\"" + AppExe + "\"", RegistryValueKind.String);
                }
                else
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                        if (k != null) k.DeleteValue(AppName, false);
                }

                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UnKey))
                {
                    if (k != null)
                    {
                        k.SetValue("DisplayName", AppName);
                        k.SetValue("DisplayVersion", Version);
                        k.SetValue("Publisher", AppName);
                        k.SetValue("InstallLocation", Dest);
                        k.SetValue("DisplayIcon", AppExe);
                        k.SetValue("UninstallString", "\"" + UninstExe + "\" /uninstall");
                        k.SetValue("QuietUninstallString", "\"" + UninstExe + "\" /uninstall /silent");
                        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                        k.SetValue("EstimatedSize", 400, RegistryValueKind.DWord);
                    }
                }

                if (shortcut) MakeShortcut();

                Process.Start(new ProcessStartInfo(AppExe) { WorkingDirectory = Dest });
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo instalar:\n\n" + ex.Message,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // ------------------------------------------------------------------ desinstalar

        static int Uninstall(bool silent)
        {
            if (!silent)
            {
                var r = MessageBox.Show(
                    "Quitar DildoCursor?\n\n" +
                    "Se cierra el programa, se restauran los cursores de Windows,\n" +
                    "se saca el arranque automatico y se borra la configuracion.",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return 1;
            }

            StopRunning();
            RestoreCursors();

            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null) k.DeleteValue(AppName, false);
            }
            catch { }

            try { Registry.CurrentUser.DeleteSubKeyTree(UnKey, false); } catch { }

            try
            {
                string cfg = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
                if (Directory.Exists(cfg)) Directory.Delete(cfg, true);
            }
            catch { }

            try
            {
                string lnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
                if (File.Exists(lnk)) File.Delete(lnk);
            }
            catch { }

            ScheduleFolderDelete(Dest);

            if (!silent)
                MessageBox.Show("Listo. Los cursores de Windows quedaron como estaban.",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        // ------------------------------------------------------------------ auxiliares

        static string CurrentExe()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch { return Assembly.GetExecutingAssembly().Location; }
        }

        static void ExtractResource(string endsWith, string target)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (!name.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase)) continue;
                using (Stream src = asm.GetManifestResourceStream(name))
                using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write))
                    src.CopyTo(dst);
                return;
            }
            throw new InvalidOperationException("El instalador no trae " + endsWith + " adentro.");
        }

        /// <summary>Le pide a la app que cierre sola (asi restaura los cursores) y recien
        /// despues la fuerza.</summary>
        static void StopRunning()
        {
            try
            {
                EventWaitHandle ev;
                if (EventWaitHandle.TryOpenExisting(QuitEvent, out ev))
                    using (ev) ev.Set();
            }
            catch { }

            for (int i = 0; i < 30; i++)
            {
                if (Process.GetProcessesByName("DildoCursor").Length == 0) return;
                Thread.Sleep(100);
            }
            foreach (Process p in Process.GetProcessesByName("DildoCursor"))
            {
                try { p.Kill(); p.WaitForExit(2000); } catch { }
            }
        }

        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(int a, int b, IntPtr c, int d);

        static void RestoreCursors()
        {
            try { SystemParametersInfo(0x57, 0, IntPtr.Zero, 0x02); } catch { }
        }

        static void MakeShortcut()
        {
            try
            {
                string lnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

                // Via COM, sin referencias extra: el shell lo arma igual
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                                           new object[] { lnk });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { AppExe });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Dest });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc,
                                new object[] { "Cursor de goma con fisica" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }

        /// <summary>El desinstalador vive adentro de la carpeta que hay que borrar, asi que
        /// la borra un .cmd que arranca y espera a que este proceso termine.</summary>
        static void ScheduleFolderDelete(string folder)
        {
            try
            {
                string cmd = Path.Combine(Path.GetTempPath(), "dildocursor_rmdir.cmd");
                File.WriteAllText(cmd,
                    "@echo off\r\n" +
                    "ping -n 4 127.0.0.1 >nul\r\n" +
                    "rmdir /s /q \"" + folder + "\"\r\n" +
                    "del \"%~f0\"\r\n");

                var psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
        }
    }
}
