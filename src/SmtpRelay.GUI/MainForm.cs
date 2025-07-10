using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;   // choose WinForms Timer

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private const string ServiceName = "SMTPRelayService";
        private readonly Timer _statusTimer = new() { Interval = 5000 };
        private readonly Label _footerVersion = new();
        private Config _cfg = null!;

        public MainForm()
        {
            InitializeComponent();

            BuildFooter();          // place version + link
            LoadConfig();
            UpdateServiceStatus();

            _statusTimer.Tick += (_, _) => UpdateServiceStatus();
            _statusTimer.Start();
        }

        /* ───────────────── footer layout ───────────────── */
        private void BuildFooter()
        {
            /* Hide the designer-placed version label (bottom-right) */
            var designerVer = Controls
                .OfType<Label>()
                .FirstOrDefault(l => l.Text.StartsWith("Version", StringComparison.OrdinalIgnoreCase));
            if (designerVer != null) designerVer.Visible = false;

            /* Add new version label */
            _footerVersion.AutoSize = true;
            _footerVersion.Text     = $"Version {Program.AppVersion}";
            Controls.Add(_footerVersion);

            const int gap = 6;  // vertical spacing

            int left = btnViewLogs.Left;                       // align with View Logs
            int top  = lblServiceHint.Bottom + gap;            // lblServiceHint is the
                                                               // “Service will continue…” label
            _footerVersion.Location = new Point(left, top);

            linkRepo.Location = new Point(left, _footerVersion.Bottom + 2);
            linkRepo.AutoSize = true;

            _footerVersion.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            linkRepo.Anchor       = AnchorStyles.Left | AnchorStyles.Bottom;
        }

        /* ───────── config load / save (unchanged) ───────── */
        private void LoadConfig()
        {
            _cfg = Config.Load();
            txtHost.Text         = _cfg.SmartHost;
            numPort.Value        = _cfg.SmartHostPort;
            chkStartTls.Checked  = _cfg.UseStartTls;
            txtUsername.Text     = _cfg.Username;
            txtPassword.Text     = _cfg.Password;
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
            _cfg.AllowedIPs    = txtIpList.Lines.Select(s => s.Trim())
                                                .Where(s => s.Length > 0).ToList();
            _cfg.EnableLogging = chkEnableLogging.Checked;
            _cfg.RetentionDays = (int)numRetentionDays.Value;
            _cfg.Save();
        }

        /* ───────── UI toggles (unchanged code) ───────── */
        private void chkStartTls_CheckedChanged(object s, EventArgs e)
        {
            ToggleAuthFields();
            if (!txtUsername.Enabled) { txtUsername.Clear(); txtPassword.Clear(); }
            numPort.Value = chkStartTls.Checked ? 587 : 25;
        }
        private void ToggleAuthFields()       { txtUsername.Enabled = chkStartTls.Checked; txtPassword.Enabled = chkStartTls.Checked; }
        private void radioAllowRestrictions_CheckedChanged(object s, EventArgs e) => ToggleIpField();
        private void ToggleIpField()          => txtIpList.Enabled = radioAllowList.Checked;
        private void chkEnableLogging_CheckedChanged(object s, EventArgs e) => ToggleLoggingFields();
        private void ToggleLoggingFields()    { numRetentionDays.Enabled = chkEnableLogging.Checked; btnViewLogs.Enabled = chkEnableLogging.Checked; }

        /* ───────── service status (unchanged) ───────── */
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

        /* ───────── buttons (unchanged) ───────── */
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

        /* ───────── single-instance activate (unchanged) ───────── */
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
