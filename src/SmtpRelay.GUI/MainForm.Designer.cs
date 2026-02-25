using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SmtpRelay.GUI
{
    partial class MainForm
    {
        private IContainer components = null;

        private Label       labelHost;
        private TextBox     txtHost;
        private Label       labelPort;
        private NumericUpDown numPort;
        private CheckBox    chkStartTls;
        private Label       lblUsername;
        private TextBox     txtUsername;
        private Label       lblPassword;
        private TextBox     txtPassword;
        private Label       labelRelayRestrictions;
        private RadioButton radioAllowAll;
        private RadioButton radioAllowList;
        private TextBox     txtIpList;
        private Label       labelIpExample;
        private Label       lblLogging;
        private CheckBox    chkEnableLogging;
        private Label       labelDaysKept;
        private NumericUpDown numRetentionDays;
        private Button      btnViewLogs;
        private Button      btnSave;
        private Button      btnClose;
        private Label       labelWillContinue1;
        private Label       labelWillContinue2;
        private Label       labelServiceStatusCaption;
        private Label       labelServiceStatus;
        private Label       lblVersion;
        private LinkLabel   linkRepo;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components                 = new Container();
            this.labelHost                  = new Label();
            this.txtHost                    = new TextBox();
            this.labelPort                  = new Label();
            this.numPort                    = new NumericUpDown();
            this.chkStartTls                = new CheckBox();
            this.lblUsername                = new Label();
            this.txtUsername                = new TextBox();
            this.lblPassword                = new Label();
            this.txtPassword                = new TextBox();
            this.labelRelayRestrictions     = new Label();
            this.radioAllowAll              = new RadioButton();
            this.radioAllowList             = new RadioButton();
            this.txtIpList                  = new TextBox();
            this.labelIpExample             = new Label();
            this.lblLogging                 = new Label();
            this.chkEnableLogging           = new CheckBox();
            this.labelDaysKept              = new Label();
            this.numRetentionDays           = new NumericUpDown();
            this.btnViewLogs                = new Button();
            this.btnSave                    = new Button();
            this.btnClose                   = new Button();
            this.labelWillContinue1         = new Label();
            this.labelWillContinue2         = new Label();
            this.labelServiceStatusCaption  = new Label();
            this.labelServiceStatus         = new Label();
            this.lblVersion                 = new Label();
            this.linkRepo                   = new LinkLabel();

            ((ISupportInitialize)(this.numPort)).BeginInit();
            ((ISupportInitialize)(this.numRetentionDays)).BeginInit();
            this.SuspendLayout();

            // MainForm
            this.AutoScaleMode           = AutoScaleMode.Font;
            this.ClientSize              = new Size(900, 700);
            this.MinimumSize             = new Size(900, 700);
            this.FormBorderStyle         = FormBorderStyle.Sizable;
            this.StartPosition           = FormStartPosition.CenterScreen;
            this.Text                    = "SMTP Relay Configuration";
            this.Icon                    = new Icon("smtp.ico");
            this.Font                    = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

            // labelHost
            this.labelHost.AutoSize      = true;
            this.labelHost.Location      = new Point(30, 30);
            this.labelHost.Text          = "SMTP Host:";

            // txtHost
            this.txtHost.Location        = new Point(180, 27);
            this.txtHost.Size            = new Size(650, 28);
            this.txtHost.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // labelPort
            this.labelPort.AutoSize      = true;
            this.labelPort.Location      = new Point(30, 80);
            this.labelPort.Text          = "Port:";

            // numPort
            this.numPort.Location        = new Point(180, 77);
            this.numPort.Maximum         = 65535;
            this.numPort.Minimum         = 1;
            this.numPort.Value           = 25;
            this.numPort.Size            = new Size(100, 28);

            // chkStartTls
            this.chkStartTls.AutoSize    = true;
            this.chkStartTls.Location    = new Point(300, 79);
            this.chkStartTls.Text        = "STARTTLS";
            this.chkStartTls.CheckedChanged += chkStartTls_CheckedChanged;

            // lblUsername
            this.lblUsername.AutoSize    = true;
            this.lblUsername.Location    = new Point(30, 130);
            this.lblUsername.Text        = "Username:";

            // txtUsername
            this.txtUsername.Location    = new Point(180, 127);
            this.txtUsername.Size        = new Size(650, 28);
            this.txtUsername.Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.txtUsername.Enabled     = false;

            // lblPassword
            this.lblPassword.AutoSize    = true;
            this.lblPassword.Location    = new Point(30, 180);
            this.lblPassword.Text        = "Password:";

            // txtPassword
            this.txtPassword.Location    = new Point(180, 177);
            this.txtPassword.Size        = new Size(650, 28);
            this.txtPassword.Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.txtPassword.PasswordChar= '●';
            this.txtPassword.Enabled     = false;

            // labelRelayRestrictions
            this.labelRelayRestrictions.AutoSize = true;
            this.labelRelayRestrictions.Location = new Point(30, 230);
            this.labelRelayRestrictions.Text     = "Relay Restrictions:";

            // radioAllowAll
            this.radioAllowAll.AutoSize     = true;
            this.radioAllowAll.Location     = new Point(200, 228);
            this.radioAllowAll.Text         = "Allow All";
            this.radioAllowAll.Checked      = true;
            this.radioAllowAll.CheckedChanged += radioAllowRestrictions_CheckedChanged;

            // radioAllowList
            this.radioAllowList.AutoSize    = true;
            this.radioAllowList.Location    = new Point(320, 228);
            this.radioAllowList.Text        = "Allow Specified";
            this.radioAllowList.CheckedChanged += radioAllowRestrictions_CheckedChanged;

            // txtIpList
            this.txtIpList.Location         = new Point(180, 260);
            this.txtIpList.Multiline        = true;
            this.txtIpList.Size             = new Size(650, 100);
            this.txtIpList.Anchor           = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.txtIpList.Enabled          = false;
            this.txtIpList.ScrollBars       = ScrollBars.Vertical;

            // labelIpExample
            this.labelIpExample.AutoSize    = true;
            this.labelIpExample.ForeColor   = Color.Gray;
            this.labelIpExample.Location    = new Point(180, 370);
            this.labelIpExample.Text        = "e.g. 127.0.0.1, 10.0.0.0/24, ::1";

            // lblLogging
            this.lblLogging.AutoSize        = true;
            this.lblLogging.Location        = new Point(30, 420);
            this.lblLogging.Text            = "Logging:";

            // chkEnableLogging
            this.chkEnableLogging.AutoSize  = true;
            this.chkEnableLogging.Location  = new Point(180, 418);
            this.chkEnableLogging.Text      = "Enable";
            this.chkEnableLogging.CheckedChanged += chkEnableLogging_CheckedChanged;

            // labelDaysKept
            this.labelDaysKept.AutoSize     = true;
            this.labelDaysKept.Location     = new Point(300, 420);
            this.labelDaysKept.Text         = "Days Kept:";

            // numRetentionDays
            this.numRetentionDays.Location   = new Point(400, 417);
            this.numRetentionDays.Maximum    = 365;
            this.numRetentionDays.Minimum    = 1;
            this.numRetentionDays.Value      = 30;
            this.numRetentionDays.Size       = new Size(80, 28);

            // btnViewLogs
            this.btnViewLogs.Location       = new Point(500, 415);
            this.btnViewLogs.Size           = new Size(120, 32);
            this.btnViewLogs.Text           = "View Logs";
            this.btnViewLogs.Click          += btnViewLogs_Click;

            // btnSave
            this.btnSave.Location           = new Point(180, 480);
            this.btnSave.Size               = new Size(120, 40);
            this.btnSave.Text               = "Save";
            this.btnSave.Click              += btnSave_Click;

            // btnClose
            this.btnClose.Location          = new Point(320, 480);
            this.btnClose.Size              = new Size(120, 40);
            this.btnClose.Text              = "Close";
            this.btnClose.Click             += btnClose_Click;

            // labelWillContinue1
            this.labelWillContinue1.AutoSize   = true;
            this.labelWillContinue1.Location    = new Point(320, 530);
            this.labelWillContinue1.Text        = "Service will";

            // labelWillContinue2
            this.labelWillContinue2.AutoSize   = true;
            this.labelWillContinue2.Location    = new Point(320, 550);
            this.labelWillContinue2.Text        = "continue to run";

            // labelServiceStatusCaption
            this.labelServiceStatusCaption.AutoSize = true;
            this.labelServiceStatusCaption.Font      = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            this.labelServiceStatusCaption.Location  = new Point(30, 600);
            this.labelServiceStatusCaption.Text      = "Service Status:";

            // labelServiceStatus
            this.labelServiceStatus.AutoSize    = true;
            this.labelServiceStatus.Font        = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            this.labelServiceStatus.Location    = new Point(180, 600);
            this.labelServiceStatus.Text        = "Unknown";

            // lblVersion
            this.lblVersion.AutoSize           = true;
            this.lblVersion.Location           = new Point(650, 600);
            this.lblVersion.Text               = $"Version: {Program.AppVersion}";

            // linkRepo
            this.linkRepo.AutoSize             = true;
            this.linkRepo.Location             = new Point(650, 630);
            this.linkRepo.Text                 = "https://github.com/mkitchingh/Smtp-Relay";
            this.linkRepo.LinkClicked          += linkRepo_LinkClicked;

            // Add all controls
            this.Controls.AddRange(new Control[]
            {
                this.labelHost, this.txtHost,
                this.labelPort, this.numPort, this.chkStartTls,
                this.lblUsername, this.txtUsername,
                this.lblPassword, this.txtPassword,
                this.labelRelayRestrictions, this.radioAllowAll, this.radioAllowList,
                this.txtIpList, this.labelIpExample,
                this.lblLogging, this.chkEnableLogging, this.labelDaysKept, this.numRetentionDays, this.btnViewLogs,
                this.btnSave, this.btnClose, this.labelWillContinue1, this.labelWillContinue2,
                this.labelServiceStatusCaption, this.labelServiceStatus,
                this.lblVersion, this.linkRepo
            });

            ((ISupportInitialize)(this.numPort)).EndInit();
            ((ISupportInitialize)(this.numRetentionDays)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
