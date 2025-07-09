using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace SmtpRelay.GUI
{
    internal static class Program
    {
        private const string MutexName = "SMTPRelayGuiSingleton";

        /// <summary>Version string used by MainForm.Designer.cs</summary>
        public static readonly string AppVersion = GetVersionString();

        [STAThread]
        private static void Main()
        {
            /* ── single-instance guard ───────────────────────────── */
            using var mx = new Mutex(true, MutexName, out bool isFirst);
            if (!isFirst) { ShowExistingWindow(); return; }

            /* ── admin check ─────────────────────────────────────── */
            if (!IsAdmin())
            {
                MessageBox.Show("SMTP Relay Configuration must be run as Administrator.",
                                "SMTP Relay", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            /* ── normal startup ─────────────────────────────────── */
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        /* ───────────────────────── helpers ─────────────────────── */

        private static bool IsAdmin()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void ShowExistingWindow()
        {
            NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST,
                                      NativeMethods.WM_SHOWME,
                                      IntPtr.Zero, IntPtr.Zero);
        }

        private static string GetVersionString()
        {
            // single-file: Assembly.Location is empty, so use process path
            try
            {
                var path = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    var ver = FileVersionInfo.GetVersionInfo(path).FileVersion;
                    if (!string.IsNullOrEmpty(ver)) return ver;
                }
            }
            catch { /* ignore and fall through */ }

            // fallback to assembly version
            return Assembly.GetExecutingAssembly()
                           .GetName().Version?.ToString() ?? "1.0.0";
        }

        internal static class NativeMethods
        {
            public const int HWND_BROADCAST = 0xffff;
            public static readonly int WM_SHOWME =
                RegisterWindowMessage("SMTP_RELAY_GUI_SHOWME");

            [System.Runtime.InteropServices.DllImport("user32")] public static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
            [System.Runtime.InteropServices.DllImport("user32")] public static extern int  RegisterWindowMessage(string s);
        }
    }
}
