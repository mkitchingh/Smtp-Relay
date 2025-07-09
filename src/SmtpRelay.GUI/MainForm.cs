using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private const string ServiceName = "SMTPRelayService";
        private readonly Timer _statusTimer = new() { Interval = 5000 };  // 5-second refresh
        private Config _cfg;

        public MainForm()
        {
            InitializeComponent();

            // move version + link to left so they don't run off the form
            labelVersion.Left = 12;
            linkRepo.Left     = 12;
            labelVersion.Text = $"Version {Program.AppVersion}";

            LoadConfig();
            UpdateServiceStatus();

            _statusTimer.Tick += (_, _) => UpdateServiceStatus();
            _statusTimer.Start();
        }

        /* ───── CONFIG LOAD / SAVE (unchanged) ───── */
        private void LoadConfig() { /* ... same content as before ... */ }
        private void SaveConfig() { /* ... same content as before ... */ }

        /* ───── UI state toggles (unchanged) ───── */
        // chkStartTls_CheckedChanged, ToggleAuthFields, etc.

        /* ───── Service status refresh ───── */
        private void UpdateServiceStatus()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                bool running = sc.Status == ServiceControllerStatus.Running;
                labelServiceStatus.Text      = running ? "Running" : "Stopped";
                labelServiceStatus.ForeColor = running ? Color.Green : Color.Red;
            }
            catch
            {
                labelServiceStatus.Text      = "Unknown";
                labelServiceStatus.ForeColor = Color.Orange;
            }
        }

        /* ───── Buttons (unchanged except ViewLogs) ───── */
        private void btnSave_Click(object sender, EventArgs e) { /* same */ }

        private void btnViewLogs_Click(object sender, EventArgs e)
        {
            var logDir = Config.SharedLogDir;
            Directory.CreateDirectory(logDir);
            Process.Start("explorer.exe", logDir);
        }

        private void btnClose_Click(object sender, EventArgs e) => Close();

        private void linkRepo_LinkClicked(object s, LinkLabelLinkClickedEventArgs e)
            => Process.Start(new ProcessStartInfo(linkRepo.Text) { UseShellExecute = true });

        /* ───── Single-instance activation ───── */
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.NativeMethods.WM_SHOWME)
            {
                if (WindowState == FormWindowState.Minimized)
                    WindowState = FormWindowState.Normal;
                Activate();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _statusTimer.Stop();
            _statusTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
