using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private readonly Config _cfg;

        public MainForm()
        {
            InitializeComponent();
            _cfg = Config.Load();          // service & GUI share same file
            BindToControls();
        }

        /* ───────── helpers ───────── */

        private void BindToControls()
        {
            txtSmartHost.Text   = _cfg.SmartHost;
            numPort.Value       = _cfg.SmartHostPort;
            chkStartTls.Checked = _cfg.UseStartTls;
            txtUser.Text        = _cfg.Username;
            txtPass.Text        = _cfg.Password;
            chkAllowAll.Checked = _cfg.AllowAllIPs;
            txtAllowed.Text     = string.Join(", ", _cfg.AllowedIPs);
        }

        /* ───────── event handlers ───────── */

        private void btnSave_Click(object sender, EventArgs e)
        {
            _cfg.SmartHost     = txtSmartHost.Text;
            _cfg.SmartHostPort = (int)numPort.Value;
            _cfg.UseStartTls   = chkStartTls.Checked;
            _cfg.Username      = txtUser.Text;
            _cfg.Password      = txtPass.Text;
            _cfg.AllowAllIPs   = chkAllowAll.Checked;

            _cfg.AllowedIPs = txtAllowed.Text
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

            _cfg.Save();
            MessageBox.Show("Settings saved.", "SMTP Relay",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnViewLogs_Click(object sender, EventArgs e)
        {
            var logDir = Config.SharedLogDir;   // single canonical location

            if (!Directory.Exists(logDir) || Directory.GetFiles(logDir).Length == 0)
            {
                MessageBox.Show("No logs have been created yet.",
                                "SMTP Relay", MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                return;
            }

            Process.Start("explorer.exe", logDir);
        }

        /* stubs wired by the Designer (unchanged) */
        private void chkStartTls_CheckedChanged(object sender, EventArgs e) { }
        private void chkEnableLogging_CheckedChanged(object sender, EventArgs e) { }
        private void radioAllowRestrictions_CheckedChanged(object sender, EventArgs e) { }
        private void linkRepo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
            => Process.Start(new ProcessStartInfo("https://github.com/your-repo") { UseShellExecute = true });
        private void btnClose_Click(object sender, EventArgs e) => Close();
    }
}
