namespace XiboClient
{
    partial class OptionForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OptionForm));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.buttonSaveSettings = new System.Windows.Forms.Button();
            this.label9 = new System.Windows.Forms.Label();
            this.buttonLibrary = new System.Windows.Forms.Button();
            this.tbHardwareKey = new System.Windows.Forms.TextBox();
            this.textBoxXmdsUri = new System.Windows.Forms.TextBox();
            this.textBoxServerKey = new System.Windows.Forms.TextBox();
            this.textBoxLibraryPath = new System.Windows.Forms.TextBox();
            this.buttonDisplayAdmin = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.textBoxProxyDomain = new System.Windows.Forms.TextBox();
            this.maskedTextBoxProxyPass = new System.Windows.Forms.MaskedTextBox();
            this.textBoxProxyUser = new System.Windows.Forms.TextBox();
            this.labelProxyDomain = new System.Windows.Forms.Label();
            this.labelProxyPass = new System.Windows.Forms.Label();
            this.labelProxyUser = new System.Windows.Forms.Label();
            this.splashButtonBrowse = new System.Windows.Forms.Button();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.label17 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.splashOverride = new System.Windows.Forms.TextBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.onlineHelpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.folderBrowserLibrary = new System.Windows.Forms.FolderBrowserDialog();
            this.splashScreenOverride = new System.Windows.Forms.OpenFileDialog();
            this.tbStatus = new System.Windows.Forms.TextBox();
            this.xmds1 = new XiboClient.xmds.xmds();
            this.buttonExit = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.groupBox2.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(11, 46);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(128, 22);
            this.label1.TabIndex = 1;
            this.label1.Text = "CMS Address";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(522, 46);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(43, 22);
            this.label2.TabIndex = 3;
            this.label2.Text = "Key";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(21, 92);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(118, 22);
            this.label3.TabIndex = 5;
            this.label3.Text = "Local Library";
            // 
            // buttonSaveSettings
            // 
            this.buttonSaveSettings.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonSaveSettings.Location = new System.Drawing.Point(12, 508);
            this.buttonSaveSettings.Name = "buttonSaveSettings";
            this.buttonSaveSettings.Size = new System.Drawing.Size(387, 41);
            this.buttonSaveSettings.TabIndex = 6;
            this.buttonSaveSettings.Text = "Save";
            this.buttonSaveSettings.UseVisualStyleBackColor = true;
            this.buttonSaveSettings.Click += new System.EventHandler(this.buttonSaveSettings_Click);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.Location = new System.Drawing.Point(468, 92);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(97, 22);
            this.label9.TabIndex = 13;
            this.label9.Text = "Display ID";
            // 
            // buttonLibrary
            // 
            this.buttonLibrary.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonLibrary.Location = new System.Drawing.Point(353, 87);
            this.buttonLibrary.Name = "buttonLibrary";
            this.buttonLibrary.Size = new System.Drawing.Size(109, 31);
            this.buttonLibrary.TabIndex = 10;
            this.buttonLibrary.Text = "Browse";
            this.buttonLibrary.UseVisualStyleBackColor = true;
            this.buttonLibrary.Click += new System.EventHandler(this.buttonLibrary_Click);
            // 
            // tbHardwareKey
            // 
            this.tbHardwareKey.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbHardwareKey.Location = new System.Drawing.Point(571, 89);
            this.tbHardwareKey.Name = "tbHardwareKey";
            this.tbHardwareKey.Size = new System.Drawing.Size(116, 29);
            this.tbHardwareKey.TabIndex = 14;
            // 
            // textBoxXmdsUri
            // 
            this.textBoxXmdsUri.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxXmdsUri.Location = new System.Drawing.Point(145, 43);
            this.textBoxXmdsUri.Name = "textBoxXmdsUri";
            this.textBoxXmdsUri.Size = new System.Drawing.Size(371, 29);
            this.textBoxXmdsUri.TabIndex = 0;
            this.textBoxXmdsUri.Text = "http://localhost/xibo";
            // 
            // textBoxServerKey
            // 
            this.textBoxServerKey.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxServerKey.Location = new System.Drawing.Point(571, 43);
            this.textBoxServerKey.Name = "textBoxServerKey";
            this.textBoxServerKey.Size = new System.Drawing.Size(116, 29);
            this.textBoxServerKey.TabIndex = 2;
            // 
            // textBoxLibraryPath
            // 
            this.textBoxLibraryPath.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxLibraryPath.Location = new System.Drawing.Point(145, 89);
            this.textBoxLibraryPath.Name = "textBoxLibraryPath";
            this.textBoxLibraryPath.Size = new System.Drawing.Size(202, 29);
            this.textBoxLibraryPath.TabIndex = 4;
            this.textBoxLibraryPath.Text = "DEFAULT";
            // 
            // buttonDisplayAdmin
            // 
            this.buttonDisplayAdmin.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonDisplayAdmin.Location = new System.Drawing.Point(501, 508);
            this.buttonDisplayAdmin.Name = "buttonDisplayAdmin";
            this.buttonDisplayAdmin.Size = new System.Drawing.Size(90, 41);
            this.buttonDisplayAdmin.TabIndex = 7;
            this.buttonDisplayAdmin.Text = "Display Admin";
            this.buttonDisplayAdmin.UseVisualStyleBackColor = true;
            this.buttonDisplayAdmin.Click += new System.EventHandler(this.buttonDisplayAdmin_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.textBoxProxyDomain);
            this.groupBox2.Controls.Add(this.maskedTextBoxProxyPass);
            this.groupBox2.Controls.Add(this.textBoxProxyUser);
            this.groupBox2.Controls.Add(this.labelProxyDomain);
            this.groupBox2.Controls.Add(this.labelProxyPass);
            this.groupBox2.Controls.Add(this.labelProxyUser);
            this.groupBox2.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(12, 263);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(674, 150);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Proxy Information";
            // 
            // textBoxProxyDomain
            // 
            this.textBoxProxyDomain.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxProxyDomain.Location = new System.Drawing.Point(110, 95);
            this.textBoxProxyDomain.Name = "textBoxProxyDomain";
            this.textBoxProxyDomain.Size = new System.Drawing.Size(558, 29);
            this.textBoxProxyDomain.TabIndex = 7;
            // 
            // maskedTextBoxProxyPass
            // 
            this.maskedTextBoxProxyPass.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maskedTextBoxProxyPass.Location = new System.Drawing.Point(441, 37);
            this.maskedTextBoxProxyPass.Name = "maskedTextBoxProxyPass";
            this.maskedTextBoxProxyPass.Size = new System.Drawing.Size(227, 29);
            this.maskedTextBoxProxyPass.TabIndex = 5;
            this.maskedTextBoxProxyPass.UseSystemPasswordChar = true;
            // 
            // textBoxProxyUser
            // 
            this.textBoxProxyUser.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxProxyUser.Location = new System.Drawing.Point(110, 37);
            this.textBoxProxyUser.Name = "textBoxProxyUser";
            this.textBoxProxyUser.Size = new System.Drawing.Size(225, 29);
            this.textBoxProxyUser.TabIndex = 3;
            // 
            // labelProxyDomain
            // 
            this.labelProxyDomain.AutoSize = true;
            this.labelProxyDomain.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelProxyDomain.Location = new System.Drawing.Point(29, 95);
            this.labelProxyDomain.Name = "labelProxyDomain";
            this.labelProxyDomain.Size = new System.Drawing.Size(75, 22);
            this.labelProxyDomain.TabIndex = 2;
            this.labelProxyDomain.Text = "Domain";
            // 
            // labelProxyPass
            // 
            this.labelProxyPass.AutoSize = true;
            this.labelProxyPass.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelProxyPass.Location = new System.Drawing.Point(341, 40);
            this.labelProxyPass.Name = "labelProxyPass";
            this.labelProxyPass.Size = new System.Drawing.Size(94, 22);
            this.labelProxyPass.TabIndex = 1;
            this.labelProxyPass.Text = "Password";
            // 
            // labelProxyUser
            // 
            this.labelProxyUser.AutoSize = true;
            this.labelProxyUser.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelProxyUser.Location = new System.Drawing.Point(7, 40);
            this.labelProxyUser.Name = "labelProxyUser";
            this.labelProxyUser.Size = new System.Drawing.Size(97, 22);
            this.labelProxyUser.TabIndex = 0;
            this.labelProxyUser.Text = "Username";
            // 
            // splashButtonBrowse
            // 
            this.splashButtonBrowse.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.splashButtonBrowse.Location = new System.Drawing.Point(353, 136);
            this.splashButtonBrowse.Name = "splashButtonBrowse";
            this.splashButtonBrowse.Size = new System.Drawing.Size(109, 29);
            this.splashButtonBrowse.TabIndex = 14;
            this.splashButtonBrowse.Text = "Browse";
            this.splashButtonBrowse.UseVisualStyleBackColor = true;
            this.splashButtonBrowse.Click += new System.EventHandler(this.splashButtonBrowse_Click);
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel1.Location = new System.Drawing.Point(246, 226);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(177, 18);
            this.linkLabel1.TabIndex = 13;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "http://xibo.org.uk/donate/";
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label17.Location = new System.Drawing.Point(95, 195);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(509, 18);
            this.label17.TabIndex = 12;
            this.label17.Text = "If you override the splash screen please consider donating to the project. ";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label16.Location = new System.Drawing.Point(3, 139);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(134, 22);
            this.label16.TabIndex = 11;
            this.label16.Text = "Splash Screen";
            // 
            // splashOverride
            // 
            this.splashOverride.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.splashOverride.Location = new System.Drawing.Point(145, 136);
            this.splashOverride.Name = "splashOverride";
            this.splashOverride.Size = new System.Drawing.Size(202, 29);
            this.splashOverride.TabIndex = 10;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(698, 24);
            this.menuStrip1.TabIndex = 8;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(92, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem1,
            this.onlineHelpToolStripMenuItem});
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.aboutToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem1
            // 
            this.aboutToolStripMenuItem1.Name = "aboutToolStripMenuItem1";
            this.aboutToolStripMenuItem1.Size = new System.Drawing.Size(137, 22);
            this.aboutToolStripMenuItem1.Text = "About";
            this.aboutToolStripMenuItem1.Click += new System.EventHandler(this.aboutToolStripMenuItem1_Click);
            // 
            // onlineHelpToolStripMenuItem
            // 
            this.onlineHelpToolStripMenuItem.Name = "onlineHelpToolStripMenuItem";
            this.onlineHelpToolStripMenuItem.Size = new System.Drawing.Size(137, 22);
            this.onlineHelpToolStripMenuItem.Text = "Online Help";
            this.onlineHelpToolStripMenuItem.Click += new System.EventHandler(this.onlineHelpToolStripMenuItem_Click);
            // 
            // splashScreenOverride
            // 
            this.splashScreenOverride.FileName = "Splash Screen.jpg";
            // 
            // tbStatus
            // 
            this.tbStatus.BackColor = System.Drawing.SystemColors.Control;
            this.tbStatus.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tbStatus.Cursor = System.Windows.Forms.Cursors.Default;
            this.tbStatus.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbStatus.Location = new System.Drawing.Point(12, 431);
            this.tbStatus.Multiline = true;
            this.tbStatus.Name = "tbStatus";
            this.tbStatus.Size = new System.Drawing.Size(674, 60);
            this.tbStatus.TabIndex = 15;
            // 
            // xmds1
            // 
            this.xmds1.Credentials = null;
            this.xmds1.Url = "http://localhost/Xibo/server/xmds.php";
            this.xmds1.UseDefaultCredentials = false;
            // 
            // buttonExit
            // 
            this.buttonExit.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonExit.Location = new System.Drawing.Point(597, 508);
            this.buttonExit.Name = "buttonExit";
            this.buttonExit.Size = new System.Drawing.Size(90, 41);
            this.buttonExit.TabIndex = 16;
            this.buttonExit.Text = "Exit";
            this.buttonExit.UseVisualStyleBackColor = true;
            this.buttonExit.Click += new System.EventHandler(this.buttonExit_Click);
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.Location = new System.Drawing.Point(405, 508);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(90, 41);
            this.button1.TabIndex = 17;
            this.button1.Text = "Launch Client";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // OptionForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(698, 561);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.buttonExit);
            this.Controls.Add(this.tbStatus);
            this.Controls.Add(this.splashButtonBrowse);
            this.Controls.Add(this.buttonDisplayAdmin);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.label17);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.label16);
            this.Controls.Add(this.splashOverride);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.buttonSaveSettings);
            this.Controls.Add(this.tbHardwareKey);
            this.Controls.Add(this.buttonLibrary);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBoxXmdsUri);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBoxServerKey);
            this.Controls.Add(this.textBoxLibraryPath);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "OptionForm";
            this.Text = "Xibo Client Options";
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private XiboClient.xmds.xmds xmds1;
        private System.Windows.Forms.TextBox textBoxXmdsUri;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxServerKey;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxLibraryPath;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button buttonSaveSettings;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserLibrary;
        private System.Windows.Forms.Button buttonLibrary;
        private System.Windows.Forms.ToolStripMenuItem onlineHelpToolStripMenuItem;
        private System.Windows.Forms.Button buttonDisplayAdmin;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.MaskedTextBox maskedTextBoxProxyPass;
        private System.Windows.Forms.TextBox textBoxProxyUser;
        private System.Windows.Forms.Label labelProxyDomain;
        private System.Windows.Forms.Label labelProxyPass;
        private System.Windows.Forms.Label labelProxyUser;
        private System.Windows.Forms.TextBox textBoxProxyDomain;
        private System.Windows.Forms.TextBox tbHardwareKey;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.OpenFileDialog splashScreenOverride;
        private System.Windows.Forms.Button splashButtonBrowse;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox splashOverride;
        private System.Windows.Forms.TextBox tbStatus;
        private System.Windows.Forms.Button buttonExit;
        private System.Windows.Forms.Button button1;
    }
}