using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace SmtpRelay.GUI
{
    partial class MainForm
    {
        private IContainer components = null;

        private Label labelHost;
        private TextBox txtHost;
        private Label labelPort;
        private NumericUpDown numPort;

        private Label labelOutboundSecurity;
        private RadioButton radioSecurityNone;
        private RadioButton radioSecurityStartTls;
        private RadioButton radioSecuritySmtps;

        private Label lblUsername;
        private TextBox txtUsername;
        private Label lblPassword;
        private TextBox txtPassword;

        private Label labelRelayRestrictions;
        private RadioButton radioAllowAll;
        private RadioButton radioAllowList;
        private TextBox txtIpList;
        private Label labelIpExample;

        private Label lblLogging;
        private CheckBox chkEnableLogging;
        private Label lblRetentionDays;
        private NumericUpDown numRetentionDays;

        private Button btnSave;
        private Button btnOpenLogs;
        private Label lblServiceStatus;
        private Label labelServiceNote;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelHost = new Label();
            this.txtHost = new TextBox();
            this.labelPort = new Label();
            this.numPort = new NumericUpDown();

            this.labelOutboundSecurity = new Label();
            this.radioSecurityNone = new RadioButton();
            this.radioSecurityStartTls = new RadioButton();
            this.radioSecuritySmtps = new RadioButton();

            this.lblUsername = new Label();
            this.txtUsername = new TextBox();
            this.lblPassword = new Label();
            this.txtPassword = new TextBox();

            this.labelRelayRestrictions = new Label();
            this.radioAllowAll = new RadioButton();
            this.radioAllowList = new RadioButton();
            this.txtIpList = new TextBox();
            this.labelIpExample = new Label();

            this.lblLogging = new Label();
            this.chkEnableLogging = new CheckBox();
            this.lblRetentionDays = new Label();
            this.numRetentionDays = new NumericUpDown();

            this.btnSave = new Button();
            this.btnOpenLogs = new Button();
            this.lblServiceStatus = new Label();
            this.labelServiceNote = new Label();

            ((ISupportInitialize)(this.numPort)).BeginInit();
            ((ISupportInitialize)(this.numRetentionDays)).BeginInit();
            this.SuspendLayout();

            // SMTP Host
            this.labelHost.AutoSize = true;
            this.labelHost.Location = new Point(30, 30);
            this.labelHost.Text = "SMTP Host:";

            this.txtHost.Location = new Point(180, 27);
            this.txtHost.Size = new Size(500, 23);

            // Port
            this.labelPort.AutoSize = true;
            this.labelPort.Location = new Point(30, 75);
            this.labelPort.Text = "Port:";

            this.numPort.Location = new Point(180, 72);
            this.numPort.Maximum = 65535;
            this.numPort.Minimum = 1;
            this.numPort.Value = 25;
            this.numPort.Size = new Size(80, 23);

            // Outbound Security Label
            this.labelOutboundSecurity.AutoSize = true;
            this.labelOutboundSecurity.Location = new Point(300, 75);
            this.labelOutboundSecurity.Text = "Outbound Security:";

            // None
            this.radioSecurityNone.AutoSize = true;
            this.radioSecurityNone.Location = new Point(440, 73);
            this.radioSecurityNone.Text = "None";
            this.radioSecurityNone.Checked = true;
            this.radioSecurityNone.CheckedChanged += SecurityRadio_CheckedChanged;

            // STARTTLS
            this.radioSecurityStartTls.AutoSize = true;
            this.radioSecurityStartTls.Location = new Point(510, 73);
            this.radioSecurityStartTls.Text = "STARTTLS";
            this.radioSecurityStartTls.CheckedChanged += SecurityRadio_CheckedChanged;

            // SMTPS
            this.radioSecuritySmtps.AutoSize = true;
            this.radioSecuritySmtps.Location = new Point(600, 73);
            this.radioSecuritySmtps.Text = "SMTPS (SSL/TLS)";
            this.radioSecuritySmtps.CheckedChanged += SecurityRadio_CheckedChanged;

            // Username
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new Point(30, 120);
            this.lblUsername.Text = "Username:";

            this.txtUsername.Location = new Point(180, 117);
            this.txtUsername.Size = new Size(400, 23);

            // Password
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new Point(30, 155);
            this.lblPassword.Text = "Password:";

            this.txtPassword.Location = new Point(180, 152);
            this.txtPassword.Size = new Size(400, 23);
            this.txtPassword.PasswordChar = '*';

            // Relay Restrictions
            this.labelRelayRestrictions.AutoSize = true;
            this.labelRelayRestrictions.Location = new Point(30, 200);
            this.labelRelayRestrictions.Text = "Relay Restrictions:";

            this.radioAllowAll.AutoSize = true;
            this.radioAllowAll.Location = new Point(180, 198);
            this.radioAllowAll.Text = "Allow All";
            this.radioAllowAll.CheckedChanged += radioAllowRestrictions_CheckedChanged;

            this.radioAllowList.AutoSize = true;
            this.radioAllowList.Location = new Point(280, 198);
            this.radioAllowList.Text = "Allow Specified";
            this.radioAllowList.CheckedChanged += radioAllowRestrictions_CheckedChanged;

            this.txtIpList.Location = new Point(180, 230);
            this.txtIpList.Multiline = true;
            this.txtIpList.ScrollBars = ScrollBars.Vertical;
            this.txtIpList.Size = new Size(500, 100);

            this.labelIpExample.AutoSize = true;
            this.labelIpExample.Location = new Point(180, 335);
            this.labelIpExample.Text = "e.g. 127.0.0.1, 10.0.0.0/24, ::1";

            // Logging
            this.lblLogging.AutoSize = true;
            this.lblLogging.Location = new Point(30, 380);
            this.lblLogging.Text = "Logging:";

            this.chkEnableLogging.AutoSize = true;
            this.chkEnableLogging.Location = new Point(180, 378);
            this.chkEnableLogging.Text = "Enable";

            this.lblRetentionDays.AutoSize = true;
            this.lblRetentionDays.Location = new Point(260, 380);
            this.lblRetentionDays.Text = "Days Kept:";

            this.numRetentionDays.Location = new Point(330, 377);
            this.numRetentionDays.Minimum = 1;
            this.numRetentionDays.Maximum = 365;
            this.numRetentionDays.Value = 14;
            this.numRetentionDays.Size = new Size(60, 23);

            // Buttons
            this.btnSave.Location = new Point(180, 430);
            this.btnSave.Size = new Size(120, 35);
            this.btnSave.Text = "Save";
            this.btnSave.Click += btnSave_Click;

            this.btnOpenLogs.Location = new Point(320, 430);
            this.btnOpenLogs.Size = new Size(120, 35);
            this.btnOpenLogs.Text = "View Logs";
            this.btnOpenLogs.Click += btnOpenLogs_Click;

            // Service Status
            this.lblServiceStatus.AutoSize = true;
            this.lblServiceStatus.Location = new Point(30, 485);
            this.lblServiceStatus.Text = "Service Status:";

            this.labelServiceNote.AutoSize = true;
            this.labelServiceNote.Location = new Point(180, 485);
            this.labelServiceNote.Text = "Service will continue to run";

            // Form
            this.ClientSize = new Size(750, 520);
            this.Text = "SMTP Relay Configuration";

            this.Controls.Add(this.labelHost);
            this.Controls.Add(this.txtHost);
            this.Controls.Add(this.labelPort);
            this.Controls.Add(this.numPort);

            this.Controls.Add(this.labelOutboundSecurity);
            this.Controls.Add(this.radioSecurityNone);
            this.Controls.Add(this.radioSecurityStartTls);
            this.Controls.Add(this.radioSecuritySmtps);

            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.txtPassword);

            this.Controls.Add(this.labelRelayRestrictions);
            this.Controls.Add(this.radioAllowAll);
            this.Controls.Add(this.radioAllowList);
            this.Controls.Add(this.txtIpList);
            this.Controls.Add(this.labelIpExample);

            this.Controls.Add(this.lblLogging);
            this.Controls.Add(this.chkEnableLogging);
            this.Controls.Add(this.lblRetentionDays);
            this.Controls.Add(this.numRetentionDays);

            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnOpenLogs);
            this.Controls.Add(this.lblServiceStatus);
            this.Controls.Add(this.labelServiceNote);

            ((ISupportInitialize)(this.numPort)).EndInit();
            ((ISupportInitialize)(this.numRetentionDays)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}