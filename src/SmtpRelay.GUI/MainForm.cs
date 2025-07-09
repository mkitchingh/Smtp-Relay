using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;   // disambiguate Timer

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private const string ServiceName = "SMTPRelayService";
        private readonly Timer _statusTimer = new() { Interval = 5000 };
        private readonly Label _versionLabel = new();
        private Config _cfg = null!;

        public MainForm()
        {
            InitializeComponent();

            /* ── create version label dynamically ─────────────── */
            _versionLabel.AutoSize = true;
            _versionLabel.Text     = $"Version {Program.AppVersion}";
            _versionLabel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(_versionLabel);

            /* ── align version + repo link with View Logs button ─ */
            const int gap = 8; // space between items
            int leftEdge  = btnViewLogs.Left;
            _versionLabel.Left = leftEdge;
            _versionLabel.Top  = btnViewLogs.Top + btnViewLogs.Height + gap;

            linkRepo.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            linkRepo.Top    = _versionLabel.Top;
            linkRepo.Left   = leftEdge + btnViewLogs.Width - linkRepo.PreferredWidth;

            /* ── load config and start status timer ────────────── */
            LoadConfig();
            UpdateServiceStatus();
            _statusTimer.Tick += (_, _) => UpdateServiceStatus();
            _statusTimer.Start();
        }

        /* ───── CONFIG LOAD / SAVE ───── */
        private void LoadConfig()
        {
            _cfg = Config.Load();

            txtHost.Text           = _cfg.SmartHost;
            numPort.Value          = _cfg.SmartHostPort;
            chkStartTls.Checked    = _cfg.UseStartTls;
            txtUsername.Text       = _cfg.Username;
            txtPassword.Text       = _cfg.Password;

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

            _cfg.AllowAllIPs = radioAllowAll.Checked;
            _cfg.AllowedIPs  = txtIpList.Lines
                               .Select(s => s.Trim())
                               .Where(s => s.Length > 0)
                               .ToList();

            _cfg.EnableLogging = chkEnableLogging.Checked;
            _cfg.RetentionDays = (int)numRetentionDays.Value;
            _cfg.Save();
        }

        /* ───── UI toggles ───── */
        private void chkStartTls_CheckedChanged(object sender, EventArgs e)
        {
            ToggleAuthFields();
            if (!txtUsername.Enabled) { txtUsername.Clear(); txtPassword.Clear(); }
            numPort.Value = chkStartTls.Checked ? 587 : 25;
        }
        private void ToggleAuthFields()
        {
            txtUsername.Enabled = chkStartTls.Checked;
            txtPassword.Enabled = chkStartTls.Checked;
        }

        private void radioAllowRestrictions_CheckedChanged(object s, EventArgs e)
            => ToggleIpField();
        private void ToggleIpField()
            => txtIpList.Enabled = radioAllowList.Checked;

        private void chkEnableLogging_CheckedChanged(object s, EventArgs e)
            => ToggleLoggingFields();
        private void ToggleLoggingFields()
        {
            numRetentionDays.Enabled = chkEnableLogging.Checked;
            btnViewLogs.Enabled      = chkEnableLogging.Checked;
        }

        /* ───── SERVICE STATUS ───── */
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

        /* ───── BUTTONS ───── */
        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveConfig();
            try
            {
                using var sc = new ServiceController(ServiceName);
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped,  TimeSpan.FromSeconds(10));
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));

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

        private void btnViewLogs_Click(object sender, EventArgs e)
        {
            var logDir = Config.SharedLogDir;
            Directory.CreateDirectory(logDir);
            Process.Start("explorer.exe", logDir);
        }

        private void btnClose_Click(object sender, EventArgs e) => Close();

        private void linkRepo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
            => Process.Start(new ProcessStartInfo(linkRepo.Text) { UseShellExecute = true });

        /* ───── SINGLE-INSTANCE ACTIVATE ───── */
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
