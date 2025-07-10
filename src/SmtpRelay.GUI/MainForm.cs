using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;   // WinForms Timer

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private const string ServiceName = "SMTPRelayService";
        private readonly Timer _statusTimer = new() { Interval = 5000 };
        private readonly Label _verLabel = new();

        private Config _cfg = null!;

        public MainForm()
        {
            InitializeComponent();
            BuildFooter();
            LoadConfig();
            UpdateServiceStatus();

            _statusTimer.Tick += (_, _) => UpdateServiceStatus();
            _statusTimer.Start();
        }

        /* ───── footer: Version + link aligned with hint row ───── */
        private void BuildFooter()
        {
            // Hide any designer version label (bottom-right)
            foreach (var l in Controls.OfType<Label>()
                                      .Where(l => l.Text.StartsWith("Version", StringComparison.OrdinalIgnoreCase)))
                l.Visible = false;

            // Create runtime Version label
            _verLabel.AutoSize = true;
            _verLabel.Text = $"Version {Program.AppVersion}";
            Controls.Add(_verLabel);

            int left = btnViewLogs.Left;   // align left edge
            int gap  = 2;                  // gap between version + link rows

            // Find the hint label by text fragment
            Control? hint = Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text.IndexOf("Service will continue", StringComparison.OrdinalIgnoreCase) >= 0);

            int y = hint?.Top ?? (btnViewLogs.Bottom + 6);

            _verLabel.Location = new Point(left, y);
            linkRepo.Location  = new Point(left, y + _verLabel.Height + gap);

            _verLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            linkRepo.Anchor  = AnchorStyles.Left | AnchorStyles.Bottom;
        }

        /* ───── Config load / save (unchanged) ───── */
        private void LoadConfig()
        {
            _cfg = Config.Load();
            txtHost.Text        = _cfg.SmartHost;
            numPort.Value       = _cfg.SmartHostPort;
            chkStartTls.Checked = _cfg.UseStartTls;
            txtUsername.Text    = _cfg.Username;
            txtPassword.Text    = _cfg.Password;

            radioAllowAll.Checked  = _cfg.AllowAllIPs;
            radioAllowList.Checked = !_cfg.AllowAllIPs;
            txtIpList.Lines        = _cfg.AllowedIPs.ToArray();

            chkEnableLogging.Checked = _cfg.EnableLogging;
            numRetentionDays.Value   = _cfg.RetentionDays;

            ToggleAuthFields();
            ToggleIpField();
            ToggleLoggingFields();
        }
        private void SaveConfig()
        {
            _cfg.SmartHost     = txtHost.Text.Trim();
            _cfg.SmartHostPort = (int)numPort.Value;
            _cfg.UseStartTls   = chkStartTls.Checked;
            _cfg.Username      = txtUsername.Text;
            _cfg.Password      = txtPassword.Text;
            _cfg.AllowAllIPs   = radioAllowAll.Checked;
            _cfg.AllowedIPs    = txtIpList.Lines.Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            _cfg.EnableLogging = chkEnableLogging.Checked;
            _cfg.RetentionDays = (int)numRetentionDays.Value;
            _cfg.Save();
        }

        /* ───── UI toggles (unchanged) ───── */
        private void chkStartTls_CheckedChanged(object s, EventArgs e)
        {
            ToggleAuthFields();
            if (!txtUsername.Enabled) { txtUsername.Clear(); txtPassword.Clear(); }
            numPort.Value = chkStartTls.Checked ? 587 : 25;
        }
        private void ToggleAuthFields() { txtUsername.Enabled = chkStartTls.Checked; txtPassword.Enabled = chkStartTls.Checked; }
        private void radioAllowRestrictions_CheckedChanged(object s, EventArgs e) => ToggleIpField();
        private void ToggleIpField() => txtIpList.Enabled = radioAllowList.Checked;
        private void chkEnableLogging_CheckedChanged(object s, EventArgs e) => ToggleLoggingFields();
        private void ToggleLoggingFields() { numRetentionDays.Enabled = chkEnableLogging.Checked; btnViewLogs.Enabled = chkEnableLogging.Checked; }

        /* ───── Service status refresh (unchanged) ───── */
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

        /* ───── Buttons (unchanged) ───── */
        private void btnSave_Click(object s, EventArgs e)
        {
            SaveConfig();
            try
            {
                using var sc = new ServiceController(ServiceName);
                sc.Stop();  sc.WaitForStatus(ServiceControllerStatus.Stopped,  TimeSpan.FromSeconds(10));
                sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                MessageBox.Show("Settings saved and service restarted.",
                                "SMTP Relay", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateServiceStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart service:\n{ex.Message}",
                                "SMTP Relay", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnViewLogs_Click(object s, EventArgs e)
        {
            var dir = Config.SharedLogDir;
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }
        private void btnClose_Click(object s, EventArgs e) => Close();
        private void linkRepo_LinkClicked(object s, LinkLabelLinkClickedEventArgs e)
            => Process.Start(new ProcessStartInfo(linkRepo.Text) { UseShellExecute = true });

        /* ───── Single-instance activation (unchanged) ───── */
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.NativeMethods.WM_SHOWME)
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
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
