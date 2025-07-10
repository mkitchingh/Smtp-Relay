using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;   // make Timer unambiguous

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private const string ServiceName = "SMTPRelayService";
        private readonly Timer _statusTimer = new() { Interval = 5000 };
        private Config _cfg = null!;

        public MainForm()
        {
            InitializeComponent();
            BuildFooter();                    // ← new dynamic layout
            LoadConfig();
            UpdateServiceStatus();

            _statusTimer.Tick += (_, _) => UpdateServiceStatus();
            _statusTimer.Start();
        }

        /* ───────── dynamic footer layout ───────── */
        private void BuildFooter()
        {
            /* find View Logs button left-edge */
            int leftEdge = btnViewLogs.Left;
            int gap      = 6;     // vertical space

            /* locate the “service will continue…” hint if present */
            Control? hint = Controls
                .OfType<Label>()
                .FirstOrDefault(l => l.Text.StartsWith("Service will continue",
                                                       StringComparison.OrdinalIgnoreCase));

            int yBase = (hint?.Bottom ?? btnViewLogs.Bottom) + gap;

            /* use existing designer labelVersion if present; otherwise create */
            Label verLabel = Controls.OfType<Label>()
                                     .FirstOrDefault(l => l.Name == "labelVersion")
                          ?? new Label { AutoSize = true };

            verLabel.Text   = $"Version {Program.AppVersion}";
            verLabel.Left   = leftEdge;
            verLabel.Top    = yBase;
            verLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

            if (!Controls.Contains(verLabel))
                Controls.Add(verLabel);

            /* position linkRepo beneath version label, same left edge */
            linkRepo.Left   = leftEdge;
            linkRepo.Top    = verLabel.Bottom + 2;
            linkRepo.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        }

        /* ───────── config load / save (unchanged) ───────── */
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

        /* ───────── UI toggles (unchanged) ───────── */
        private void chkStartTls_CheckedChanged(object s, EventArgs e) { ToggleAuthFields(); if (!txtUsername.Enabled) { txtUsername.Clear(); txtPassword.Clear(); } numPort.Value = chkStartTls.Checked ? 587 : 25; }
        private void ToggleAuthFields() { txtUsername.Enabled = chkStartTls.Checked; txtPassword.Enabled = chkStartTls.Checked; }
        private void radioAllowRestrictions_CheckedChanged(object s, EventArgs e) => ToggleIpField();
        private void ToggleIpField() => txtIpList.Enabled = radioAllowList.Checked;
        private void chkEnableLogging_CheckedChanged(object s, EventArgs e) => ToggleLoggingFields();
        private void ToggleLoggingFields() { numRetentionDays.Enabled = chkEnableLogging.Checked; btnViewLogs.Enabled = chkEnableLogging.Checked; }

        /* ───────── service status refresh (unchanged) ───────── */
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
            var logDir = Config.SharedLogDir;
            Directory.CreateDirectory(logDir);
            Process.Start("explorer.exe", logDir);
        }
        private void btnClose_Click(object s, EventArgs e) => Close();
        private void linkRepo_LinkClicked(object s, LinkLabelLinkClickedEventArgs e)
            => Process.Start(new ProcessStartInfo(linkRepo.Text) { UseShellExecute = true });

        /* ───────── single-instance activation (unchanged) ───────── */
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
