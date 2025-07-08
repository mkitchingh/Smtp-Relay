using System;
using System.Windows.Forms;

namespace SmtpRelay.GUI
{
    public partial class MainForm : Form
    {
        private readonly Config _cfg = Config.Load();   // ← same loader as service

        public MainForm() { InitializeComponent(); Bind(); }

        private void Bind()
        {
            txtSmartHost.Text     = _cfg.SmartHost;
            numPort.Value         = _cfg.SmartHostPort;
            chkStartTls.Checked   = _cfg.UseStartTls;
            txtUser.Text          = _cfg.Username;
            txtPass.Text          = _cfg.Password;
            chkAllowAll.Checked   = _cfg.AllowAllIPs;
            txtAllowed.Text       = string.Join(", ", _cfg.AllowedIPs);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            _cfg.SmartHost     = txtSmartHost.Text;
            _cfg.SmartHostPort = (int)numPort.Value;
            _cfg.UseStartTls   = chkStartTls.Checked;
            _cfg.Username      = txtUser.Text;
            _cfg.Password      = txtPass.Text;
            _cfg.AllowAllIPs   = chkAllowAll.Checked;
            _cfg.AllowedIPs    = new() { txtAllowed.Text };

            _cfg.Save();                        //  ← writes beside service EXE
            MessageBox.Show("Settings saved.", "SMTP Relay", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
