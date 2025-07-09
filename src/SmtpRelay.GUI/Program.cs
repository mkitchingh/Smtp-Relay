using System;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace SmtpRelay.GUI
{
    internal static class Program
    {
        private const string MutexName = "SMTPRelayGuiSingleton";

        [STAThread]
        private static void Main()
        {
            using var mx = new Mutex(true, MutexName, out bool isFirst);
            if (!isFirst) { ShowExistingWindow(); return; }

            if (!IsAdmin())
            {
                MessageBox.Show("SMTP Relay Configuration must be run as Administrator.",
                                "SMTP Relay", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        /* ───── helpers ───── */

        private static bool IsAdmin()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void ShowExistingWindow()
        {
            NativeMethods.PostMessage(
                (IntPtr)NativeMethods.HWND_BROADCAST,
                NativeMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
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
