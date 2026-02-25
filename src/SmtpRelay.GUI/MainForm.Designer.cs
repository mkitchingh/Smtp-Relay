using System.ComponentModel;
using System.Drawing;
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

        // Names that MainForm.cs EXPECTS:
        private Button btnViewLogs;
        private Button btnClose;
        private LinkLabel linkRepo;
        private Label labelServiceStatus;
        private Label labelServiceNote;

        private Button btnSave;

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
            this.btnViewLogs = new Button();
            this.btnClose = new Button();
            this.linkRepo = new LinkLabel();
            this.labelServiceStatus = new Label();
            this.labelServiceNote = new Label();

            ((ISupportInitialize)(this.numPort)).BeginInit();
            ((ISupportInitialize)(this.numRetentionDays)).BeginInit();
            this.SuspendLayout();

            // Host
            this.labelHost.AutoSize = true;
            this.labelHost.Location = new Point(30, 30);
            this.labelHost.Text = "Smart Host:";

            this.txtHost.Location = new Point(180, 27);
            this.txtHost.Size = new Size(520, 23);

            // Port
            this.labelPort.AutoSize = true;
            this.labelPort.Location = new Point(30, 75);
            this.labelPort.Text = "Port:";

            this.numPort.Location = new Point(180, 72);
            this.numPort.Maximum = 65535;
            this.numPort.Minimum = 1;
            this.numPort.Value = 25;
            this.numPort.Size = new Size(90, 23);

            // Outbound Security
            this.labelOutboundSecurity.AutoSize = true;
            this.labelOutboundSecurity.Location = new Point(300, 75);
            this.labelOutboundSecurity.Text = "Outbound Security:";

            this.radioSecurityNone.AutoSize = true;
            this.radioSecurityNone.Location = new Point(440, 73);
            this.radioSecurityNone.Text = "None";
            this.radioSecurityNone.Checked = true;
            this.radioSecurityNone.CheckedChanged += new System.EventHandler(this.SecurityRadio_CheckedChanged);

            this.radioSecurityStartTls.AutoSize = true;
            this.radioSecurityStartTls.Location = new Point(515, 73);
            this.radioSecurityStartTls.Text = "STARTTLS";
            this.radioSecurityStartTls.CheckedChanged += new System.EventHandler(this.SecurityRadio_CheckedChanged);

            this.radioSecuritySmtps.AutoSize = true;
            this.radioSecuritySmtps.Location = new Point(610, 73);
            this.radioSecuritySmtps.Text = "SMTPS (SSL/TLS)";
            this.radioSecuritySmtps.CheckedChanged += new System.EventHandler(this.SecurityRadio_CheckedChanged);

            // Username
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new Point(30, 120);
            this.lblUsername.Text = "Username:";

            this.txtUsername.Location = new Point(180, 117);
            this.txtUsername.Size = new Size(420, 23);

            // Password
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new Point(30, 155);
            this.lblPassword.Text = "Password:";

            this.txtPassword.Location = new Point(180, 152);
            this.txtPassword.Size = new Size(420, 23);
            this.txtPassword.PasswordChar = '*';

            // Relay restrictions
            this.labelRelayRestrictions.AutoSize = true;
            this.labelRelayRestrictions.Location = new Point(30, 205);
            this.labelRelayRestrictions.Text = "Relay Restrictions:";

            this.radioAllowAll.AutoSize = true;
            this.radioAllowAll.Location = new Point(180, 203);
            this.radioAllowAll.Text = "Allow all IPs";
            this.radioAllowAll.CheckedChanged += new System.EventHandler(this.radioAllowRestrictions_CheckedChanged);

            this.radioAllowList.AutoSize = true;
            this.radioAllowList.Location = new Point(320, 203);
            this.radioAllowList.Text = "Allow list only";
            this.radioAllowList.CheckedChanged += new System.EventHandler(this.radioAllowRestrictions_CheckedChanged);

            this.txtIpList.Location = new Point(180, 235);
            this.txtIpList.Multiline = true;
            this.txtIpList.ScrollBars = ScrollBars.Vertical;
            this.txtIpList.Size = new Size(520, 95);

            this.labelIpExample.AutoSize = true;
            this.labelIpExample.Location = new Point(180, 335);
            this.labelIpExample.Text = "Example: 10.0.0.0/24, 192.168.1.10-192.168.1.20";

            // Logging
            this.lblLogging.AutoSize = true;
            this.lblLogging.Location = new Point(30, 380);
            this.lblLogging.Text = "Logging:";

            this.chkEnableLogging.AutoSize = true;
            this.chkEnableLogging.Location = new Point(180, 378);
            this.chkEnableLogging.Text = "Enable Logging";
            this.chkEnableLogging.CheckedChanged += new System.EventHandler(this.chkEnableLogging_CheckedChanged);

            this.lblRetentionDays.AutoSize = true;
            this.lblRetentionDays.Location = new Point(30, 415);
            this.lblRetentionDays.Text = "Retention (days):";

            this.numRetentionDays.Location = new Point(180, 412);
            this.numRetentionDays.Minimum = 1;
            this.numRetentionDays.Maximum = 365;
            this.numRetentionDays.Value = 14;
            this.numRetentionDays.Size = new Size(90, 23);

            // Buttons
            this.btnSave.Location = new Point(30, 460);
            this.btnSave.Size = new Size(120, 35);
            this.btnSave.Text = "Save";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

            this.btnViewLogs.Location = new Point(170, 460);
            this.btnViewLogs.Size = new Size(120, 35);
            this.btnViewLogs.Text = "View Logs";
            this.btnViewLogs.Click += new System.EventHandler(this.btnViewLogs_Click);

            this.btnClose.Location = new Point(310, 460);
            this.btnClose.Size = new Size(120, 35);
            this.btnClose.Text = "Close";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);

            // Service status + note
            this.labelServiceStatus.AutoSize = true;
            this.labelServiceStatus.Location = new Point(30, 510);
            this.labelServiceStatus.Text = "Service Status:";

            this.labelServiceNote.AutoSize = true;
            this.labelServiceNote.Location = new Point(30, 535);
            this.labelServiceNote.Text = "Service will continue running even after closing this application.";

            // GitHub link
            this.linkRepo.AutoSize = true;
            this.linkRepo.Location = new Point(30, 560);
            this.linkRepo.Text = "View on GitHub";
            this.linkRepo.LinkClicked += new LinkLabelLinkClickedEventHandler(this.linkRepo_LinkClicked);

            // Form
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(760, 610);
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
            this.Controls.Add(this.btnViewLogs);
            this.Controls.Add(this.btnClose);

            this.Controls.Add(this.labelServiceStatus);
            this.Controls.Add(this.labelServiceNote);
            this.Controls.Add(this.linkRepo);

            ((ISupportInitialize)(this.numPort)).EndInit();
            ((ISupportInitialize)(this.numRetentionDays)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}