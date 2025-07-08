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
        private Config _cfg;

        public MainForm()
        {
            InitializeComponent();
            LoadConfig();
            UpdateServiceStatus();
        }

        /* ───────── config helpers ───────── */

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
            _cfg.AllowAllIPs   = radioAllowAll.Checked;
            _cfg.AllowedIPs    = txtIpList.Lines.ToList();
            _cfg.EnableLogging = chkEnableLogging.Checked;
            _cfg.RetentionDays = (int)numRetentionDays.Value;
            _cfg.Save();
        }

        /* ───────── UI state toggles ───────── */

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

        private void radioAllowRestrictions_CheckedChanged(object sender, EventArgs e)
            => ToggleIpField();

        private void ToggleIpField() => txtIpList.Enabled = radioAllowList.Checked;

        private void chkEnableLogging_CheckedChanged(object sender, EventArgs e)
            => ToggleLoggingFields();

        private void ToggleLoggingFields()
        {
            numRetentionDays.Enabled = chkEnableLogging.Checked;
            btnViewLogs.Enabled      = chkEnableLogging.Checked;
        }

        /* ───────── service helpers ───────── */

        private void UpdateServiceStatus()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                var running = sc.Status == ServiceControllerStatus.Running;
                labelServiceStatus.Text      = running ? "Running" : "Stopped";
                labelServiceStatus.ForeColor = running ? Color.Green : Color.Red;
            }
            catch
            {
                labelServiceStatus.Text      = "Unknown";
                labelServiceStatus.ForeColor = Color.Orange;
            }
        }

        /* ───────── buttons ───────── */

        private void btnViewLogs_Click(object sender, EventArgs e)
        {
            var logDir = Config.SharedLogDir;
            if (!Directory.Exists(logDir) || Directory.GetFiles(logDir).Length == 0)
            {
                MessageBox.Show("No logs have been created yet.",
                                "SMTP Relay", MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                return;
            }
            Process.Start("explorer.exe", logDir);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveConfig();
            try
            {
                using var sc = new ServiceController(ServiceName);
                sc.Stop();  sc.WaitForStatus(ServiceControllerStatus.Stopped,  TimeSpan.FromSeconds(10));
                sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));

                MessageBox.Show("Settings saved and service restarted.", "Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateServiceStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart service: {ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClose_Click(object sender, EventArgs e) => Close();

        private void linkRepo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
            => Process.Start(new ProcessStartInfo(linkRepo.Text) { UseShellExecute = true });
    }
}
