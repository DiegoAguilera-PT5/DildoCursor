using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DildoCursor
{
    /// <summary>Arranque con Windows via HKCU\...\Run (no necesita permisos de admin).</summary>
    internal static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "DildoCursor";

        public static string ExePath
        {
            get
            {
                try { return Process.GetCurrentProcess().MainModule.FileName; }
                catch { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DildoCursor.exe"); }
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    return !string.IsNullOrEmpty(k.GetValue(ValueName) as string);
                }
            }
            catch { return false; }
        }

        public static void Enable()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k != null) k.SetValue(ValueName, "\"" + ExePath + "\"", RegistryValueKind.String);
                }
            }
            catch { }
        }

        public static void Disable()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k != null) k.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }
    }
}
