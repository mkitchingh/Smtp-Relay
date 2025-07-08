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
        // MUST match the actual Windows Service short name
        private const string ServiceName = "SMTPRelayService";

        public MainForm()
        {
            InitializeComponent();
            LoadConfig();
            UpdateServiceStatus();
        }

        private void LoadConfig()
        {
            var cfg = Config.Load();
            txtHost.Text             = cfg.SmartHost;
            numPort.Value            = cfg.SmartHostPort;
            chkStartTls.Checked      = cfg.UseStartTls;
            txtUsername.Text         = cfg.Username;
            txtPassword.Text         = cfg.Password;
            radioAllowAll.Checked    = cfg.AllowAllIPs;
            radioAllowList.Checked   = !cfg.AllowAllIPs;
            txtIpList.Lines          = cfg.AllowedIPs.ToArray();
            chkEnableLogging.Checked = cfg.EnableLogging;
            numRetentionDays.Value   = cfg.RetentionDays;
            ToggleAuthFields();
            ToggleIpField();
            ToggleLoggingFields();
        }

        private void UpdateServiceStatus()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                var status = sc.Status;
                labelServiceStatus.Text      = status == ServiceControllerStatus.Running ? "Running" : "Stopped";
                labelServiceStatus.ForeColor = status == ServiceControllerStatus.Running ? Color.Green : Color.Red;
            }
            catch
            {
                labelServiceStatus.Text      = "Unknown";
                labelServiceStatus.ForeColor = Color.Orange;
            }
        }

        private void chkStartTls_CheckedChanged(object sender, EventArgs e)
        {
            ToggleAuthFields();
            numPort.Value = chkStartTls.Checked ? 587 : 25;
        }

        private void ToggleAuthFields()
        {
            txtUsername.Enabled = chkStartTls.Checked;
            txtPassword.Enabled = chkStartTls.Checked;
        }

        private void radioAllowRestrictions_CheckedChanged(object sender, EventArgs e)
        {
            ToggleIpField();
        }

        private void ToggleIpField()
        {
            txtIpList.Enabled = radioAllowList.Checked;
        }

        private void chkEnableLogging_CheckedChanged(object sender, EventArgs e)
        {
            ToggleLoggingFields();
        }

        private void ToggleLoggingFields()
        {
            numRetentionDays.Enabled = chkEnableLogging.Checked;
            btnViewLogs.Enabled      = chkEnableLogging.Checked;
        }

        private void btnViewLogs_Click(object sender, EventArgs e)
        {
            var logDir = Program.GetServiceLogDirectory();
            if (Directory.Exists(logDir))
                Process.Start("explorer.exe", logDir);
            else
                MessageBox.Show("Log folder not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var cfg = new Config
            {
                SmartHost     = txtHost.Text.Trim(),
                SmartHostPort = (int)numPort.Value,
                UseStartTls   = chkStartTls.Checked,
                Username      = txtUsername.Text,
                Password      = txtPassword.Text,
                AllowAllIPs   = radioAllowAll.Checked,
                AllowedIPs    = txtIpList.Lines.ToList(),
                EnableLogging = chkEnableLogging.Checked,
                RetentionDays = (int)numRetentionDays.Value
            };
            cfg.Save();

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

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void linkRepo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(linkRepo.Text) { UseShellExecute = true });
        }
    }
}
