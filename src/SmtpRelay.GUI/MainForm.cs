using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer; // pick WinForms Timer namespace

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private const string ServiceName = "SMTPRelayService";
        private readonly Timer _statusTimer = new() { Interval = 5000 };
        private readonly Label _verLabel = new();

        private Config _cfg = null!;

        // NEW: runtime Outbound Security controls (no Designer surgery)
        private Label _lblOutboundSecurity = null!;
        private RadioButton _rbSecNone = null!;
        private RadioButton _rbSecStartTls = null!;
        private RadioButton _rbSecSmtps = null!;

        // Guards to prevent startup events from clobbering config / creds
        private bool _loading;
        private bool _credentialsDirty;

        public MainForm()
        {
            InitializeComponent();

            BuildOutboundSecurityUi(); // add radios at runtime, hide old checkbox
            WireCredentialDirtyTracking();

            BuildFooter(); // place Version + link once
            LoadConfig();
            UpdateServiceStatus();

            _statusTimer.Tick += (_, _) => UpdateServiceStatus();
            _statusTimer.Start();
        }

        /* ───────── footer: always below “Service will continue …” ───────── */
        private void BuildFooter()
        {
            // Hide any designer version label in bottom-right
            foreach (var l in Controls.OfType<Label>()
                         .Where(l => l.Text.StartsWith("Version", StringComparison.OrdinalIgnoreCase)))
                l.Visible = false;

            // Runtime version label
            _verLabel.AutoSize = true;
            _verLabel.Text = $"Version {Program.AppVersion}";
            Controls.Add(_verLabel);

            int left = btnViewLogs.Left; // align with View Logs
            int gap = 2;                 // vertical gap

            // Position footer a constant distance below the Close button row
            int topBase = btnClose.Bottom + 22; // 22 px looks right in default font
            _verLabel.Location = new Point(left, topBase);
            linkRepo.Location = new Point(left, topBase + _verLabel.Height + gap);

            _verLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            linkRepo.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        }

        /* ───────── NEW: Outbound Security UI (runtime) ───────── */
        private void BuildOutboundSecurityUi()
        {
            // Hide old STARTTLS checkbox; we keep it as a compatibility backing field.
            chkStartTls.Visible = false;
            chkStartTls.TabStop = false;

            _lblOutboundSecurity = new Label
            {
                AutoSize = true,
                Text = "Outbound Security:",
                Location = new Point(chkStartTls.Left, chkStartTls.Top + 2)
            };

            _rbSecNone = new RadioButton
            {
                AutoSize = true,
                Text = "None",
                Location = new Point(_lblOutboundSecurity.Right + 12, chkStartTls.Top),
                Checked = true
            };

            _rbSecStartTls = new RadioButton
            {
                AutoSize = true,
                Text = "STARTTLS",
                Location = new Point(_rbSecNone.Right + 14, chkStartTls.Top)
            };

            _rbSecSmtps = new RadioButton
            {
                AutoSize = true,
                Text = "SMTPS (SSL/TLS)",
                Location = new Point(_rbSecStartTls.Right + 14, chkStartTls.Top)
            };

            _rbSecNone.CheckedChanged += SecurityRadio_CheckedChanged;
            _rbSecStartTls.CheckedChanged += SecurityRadio_CheckedChanged;
            _rbSecSmtps.CheckedChanged += SecurityRadio_CheckedChanged;

            Controls.Add(_lblOutboundSecurity);
            Controls.Add(_rbSecNone);
            Controls.Add(_rbSecStartTls);
            Controls.Add(_rbSecSmtps);

            // Keep these aligned when form resizes (same general zone as port row)
            _lblOutboundSecurity.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _rbSecNone.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _rbSecStartTls.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _rbSecSmtps.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        }

        private void WireCredentialDirtyTracking()
        {
            // Mark dirty only when user actually edits credentials (never during load)
            txtUsername.TextChanged += (_, _) =>
            {
                if (!_loading) _credentialsDirty = true;
            };
            txtPassword.TextChanged += (_, _) =>
            {
                if (!_loading) _credentialsDirty = true;
            };
        }

        private OutboundSecurityMode GetSelectedSecurity()
        {
            if (_rbSecSmtps.Checked) return OutboundSecurityMode.Smtps;
            if (_rbSecStartTls.Checked) return OutboundSecurityMode.StartTls;
            return OutboundSecurityMode.None;
        }

        private void SetSelectedSecurity(OutboundSecurityMode mode)
        {
            _rbSecNone.Checked = mode == OutboundSecurityMode.None;
            _rbSecStartTls.Checked = mode == OutboundSecurityMode.StartTls;
            _rbSecSmtps.Checked = mode == OutboundSecurityMode.Smtps;

            // Keep legacy checkbox consistent (hidden)
            chkStartTls.Checked = mode == OutboundSecurityMode.StartTls;
        }

        private void SecurityRadio_CheckedChanged(object? sender, EventArgs e)
        {
            if (_loading) return;

            var mode = GetSelectedSecurity();

            // Keep hidden checkbox consistent for older code paths
            _loading = true;
            chkStartTls.Checked = mode == OutboundSecurityMode.StartTls;
            _loading = false;

            // Default ports per requirement
            numPort.Value = mode switch
            {
                OutboundSecurityMode.None => 25,
                OutboundSecurityMode.StartTls => 587,
                OutboundSecurityMode.Smtps => 465,
                _ => numPort.Value
            };

            ToggleAuthFields(mode);
        }

        /* ───────── config load / save (SAFE) ───────── */
        private void LoadConfig()
        {
            _loading = true;

            _cfg = Config.Load();

            txtHost.Text = _cfg.SmartHost;
            numPort.Value = _cfg.SmartHostPort;

            // Use effective security (supports old configs that only have useStartTls)
            var security = _cfg.GetEffectiveSecurity();
            SetSelectedSecurity(security);

            txtUsername.Text = _cfg.Username;
            txtPassword.Text = _cfg.Password;

            radioAllowAll.Checked = _cfg.AllowAllIPs;
            radioAllowList.Checked = !_cfg.AllowAllIPs;
            txtIpList.Lines = _cfg.AllowedIPs.ToArray();

            chkEnableLogging.Checked = _cfg.EnableLogging;
            numRetentionDays.Value = _cfg.RetentionDays;

            // IMPORTANT: credentials are pristine immediately after loading from disk
            _credentialsDirty = false;

            ToggleAuthFields(security);
            ToggleIpField();
            ToggleLoggingFields();

            _loading = false;
        }

        private void SaveConfig()
        {
            _cfg.SmartHost = txtHost.Text.Trim();
            _cfg.SmartHostPort = (int)numPort.Value;

            var mode = GetSelectedSecurity();

            // NEW + backward compatibility
            _cfg.OutboundSecurity = mode;
            _cfg.UseStartTls = mode == OutboundSecurityMode.StartTls;

            // CRITICAL: never wipe creds unless user actually edited them
            if (_credentialsDirty)
            {
                _cfg.Username = txtUsername.Text;
                _cfg.Password = txtPassword.Text;
            }

            _cfg.AllowAllIPs = radioAllowAll.Checked;
            _cfg.AllowedIPs = txtIpList.Lines
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            _cfg.EnableLogging = chkEnableLogging.Checked;
            _cfg.RetentionDays = (int)numRetentionDays.Value;

            _cfg.Save();

            // After a successful save, creds now match disk again
            _credentialsDirty = false;
        }

        /* ───────── UI toggles ───────── */
        private void chkStartTls_CheckedChanged(object s, EventArgs e)
        {
            // Checkbox is hidden; we keep handler safe anyway.
            if (_loading) return;

            // Legacy behavior used to clear credentials; that is what wiped your config.
            // We NEVER clear creds automatically.
            var mode = chkStartTls.Checked ? OutboundSecurityMode.StartTls : OutboundSecurityMode.None;

            _loading = true;
            SetSelectedSecurity(mode);
            _loading = false;

            numPort.Value = chkStartTls.Checked ? 587 : 25;
            ToggleAuthFields(mode);
        }

        private void ToggleAuthFields(OutboundSecurityMode mode)
        {
            // Require auth for StartTLS and SMTPS; None disables auth fields.
            bool enable = mode != OutboundSecurityMode.None;
            txtUsername.Enabled = enable;
            txtPassword.Enabled = enable;
        }

        private void radioAllowRestrictions_CheckedChanged(object s, EventArgs e) => ToggleIpField();
        private void ToggleIpField() => txtIpList.Enabled = radioAllowList.Checked;

        private void chkEnableLogging_CheckedChanged(object s, EventArgs e) => ToggleLoggingFields();
        private void ToggleLoggingFields()
        {
            numRetentionDays.Enabled = chkEnableLogging.Checked;
            btnViewLogs.Enabled = chkEnableLogging.Checked;
        }

        /* ───────── service status (unchanged) ───────── */
        private void UpdateServiceStatus()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                bool running = sc.Status == ServiceControllerStatus.Running;
                labelServiceStatus.Text = running ? "Running" : "Stopped";
                labelServiceStatus.ForeColor = running ? Color.Green : Color.Red;
            }
            catch
            {
                labelServiceStatus.Text = "Unknown";
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
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));

                MessageBox.Show("Settings saved and service restarted.", "SMTP Relay",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                UpdateServiceStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart service:\n{ex.Message}", "SMTP Relay",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnViewLogs_Click(object s, EventArgs e)
        {
            var dir = Config.SharedLogDir;
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        private void btnClose_Click(object s, EventArgs e) => Close();

        private void linkRepo_LinkClicked(object s, LinkLabelLinkClickedEventArgs e) =>
            Process.Start(new ProcessStartInfo(linkRepo.Text) { UseShellExecute = true });

        /* ───────── single-instance activation (unchanged) ───────── */
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