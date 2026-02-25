using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private const string ServiceName = "SMTPRelayService";
        private readonly Timer _statusTimer = new() { Interval = 5000 };
        private readonly Label _verLabel = new();

        private Config _cfg = null!;

        // Runtime Outbound Security controls (no Designer edits)
        private Label _lblOutboundSecurity = null!;
        private RadioButton _rbSecNone = null!;
        private RadioButton _rbSecStartTls = null!;
        private RadioButton _rbSecSmtps = null!;

        // Guards to prevent startup events from clobbering config / creds
        private bool _loading;
        private bool _credentialsDirty;

        // Pending selection until the window handle exists
        private OutboundSecurityMode? _pendingSecuritySelection;

        public MainForm()
        {
            InitializeComponent();

            BuildOutboundSecurityUi();
            WireCredentialDirtyTracking();
            BuildFooter();

            // Apply any pending security selection once handle exists (safe; no BeginInvoke needed)
            this.HandleCreated += (_, _) =>
            {
                if (_pendingSecuritySelection.HasValue)
                {
                    _loading = true;
                    SetSelectedSecurity(_pendingSecuritySelection.Value);
                    _loading = false;
                    _pendingSecuritySelection = null;
                }
            };

            LoadConfig();
            UpdateServiceStatus();

            _statusTimer.Tick += (_, _) => UpdateServiceStatus();
            _statusTimer.Start();
        }

        /* ───────── footer ───────── */
        private void BuildFooter()
        {
            foreach (var l in Controls.OfType<Label>()
                         .Where(l => l.Text.StartsWith("Version", StringComparison.OrdinalIgnoreCase)))
                l.Visible = false;

            _verLabel.AutoSize = true;
            _verLabel.Text = $"Version {Program.AppVersion}";
            Controls.Add(_verLabel);

            int left = btnViewLogs.Left;
            int gap = 2;

            int topBase = btnClose.Bottom + 22;
            _verLabel.Location = new Point(left, topBase);
            linkRepo.Location = new Point(left, topBase + _verLabel.Height + gap);

            _verLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            linkRepo.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        }

        /* ───────── Outbound Security UI (runtime, stable positioning) ───────── */
        private void BuildOutboundSecurityUi()
        {
            // Hide old STARTTLS checkbox (legacy only)
            chkStartTls.Visible = false;
            chkStartTls.TabStop = false;

            // Anchor relative to Port control (stable reference)
            int baseTop = numPort.Top;
            int baseLeft = numPort.Right + 20;

            _lblOutboundSecurity = new Label
            {
                AutoSize = true,
                Text = "Outbound Security:",
                Location = new Point(baseLeft, baseTop + 3)
            };

            _rbSecNone = new RadioButton
            {
                AutoSize = true,
                Text = "None",
                Location = new Point(_lblOutboundSecurity.Right + 10, baseTop)
            };

            _rbSecStartTls = new RadioButton
            {
                AutoSize = true,
                Text = "STARTTLS",
                Location = new Point(_rbSecNone.Right + 14, baseTop)
            };

            _rbSecSmtps = new RadioButton
            {
                AutoSize = true,
                Text = "SMTPS (SSL/TLS)",
                Location = new Point(_rbSecStartTls.Right + 14, baseTop)
            };

            _rbSecNone.CheckedChanged += SecurityRadio_CheckedChanged;
            _rbSecStartTls.CheckedChanged += SecurityRadio_CheckedChanged;
            _rbSecSmtps.CheckedChanged += SecurityRadio_CheckedChanged;

            Controls.Add(_lblOutboundSecurity);
            Controls.Add(_rbSecNone);
            Controls.Add(_rbSecStartTls);
            Controls.Add(_rbSecSmtps);
        }

        private void WireCredentialDirtyTracking()
        {
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
            // Ensure one is always selected visibly
            _rbSecNone.Checked = mode == OutboundSecurityMode.None;
            _rbSecStartTls.Checked = mode == OutboundSecurityMode.StartTls;
            _rbSecSmtps.Checked = mode == OutboundSecurityMode.Smtps;

            // Keep legacy checkbox consistent (hidden)
            chkStartTls.Checked = mode == OutboundSecurityMode.StartTls;

            ToggleAuthFields(mode);
        }

        private void ApplySecuritySelectionSafely(OutboundSecurityMode mode)
        {
            // If handle not created yet, stash selection to apply later
            if (!IsHandleCreated)
            {
                _pendingSecuritySelection = mode;
                return;
            }

            _loading = true;
            SetSelectedSecurity(mode);
            _loading = false;
        }

        private void SecurityRadio_CheckedChanged(object? sender, EventArgs e)
        {
            if (_loading) return;

            var mode = GetSelectedSecurity();

            // Keep hidden checkbox consistent for older code paths
            _loading = true;
            chkStartTls.Checked = mode == OutboundSecurityMode.StartTls;
            _loading = false;

            // Default ports per requirement when USER changes selection
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

            var security = _cfg.GetEffectiveSecurity();
            ApplySecuritySelectionSafely(security);

            txtUsername.Text = _cfg.Username;
            txtPassword.Text = _cfg.Password;

            radioAllowAll.Checked = _cfg.AllowAllIPs;
            radioAllowList.Checked = !_cfg.AllowAllIPs;
            txtIpList.Lines = _cfg.AllowedIPs.ToArray();

            chkEnableLogging.Checked = _cfg.EnableLogging;
            numRetentionDays.Value = _cfg.RetentionDays;

            // After loading from disk, credentials are not "dirty"
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

            // New + backward compatibility
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
            _credentialsDirty = false;
        }

        /* ───────── legacy handlers kept safe ───────── */
        private void chkStartTls_CheckedChanged(object s, EventArgs e)
        {
            if (_loading) return;

            var mode = chkStartTls.Checked ? OutboundSecurityMode.StartTls : OutboundSecurityMode.None;

            _loading = true;
            ApplySecuritySelectionSafely(mode);
            _loading = false;

            numPort.Value = chkStartTls.Checked ? 587 : 25;
        }

        private void ToggleAuthFields(OutboundSecurityMode mode)
        {
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

        /* ───────── service status ───────── */
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

        /* ───────── buttons ───────── */
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

        /* ───────── single-instance activation ───────── */
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