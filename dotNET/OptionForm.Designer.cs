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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.nupScrollStepAmount = new System.Windows.Forms.NumericUpDown();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.buttonLibrary = new System.Windows.Forms.Button();
            this.numericUpDownCollect = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.tbHardwareKey = new System.Windows.Forms.TextBox();
            this.checkBoxStats = new System.Windows.Forms.CheckBox();
            this.checkBoxPowerPoint = new System.Windows.Forms.CheckBox();
            this.textBoxXmdsUri = new System.Windows.Forms.TextBox();
            this.textBoxServerKey = new System.Windows.Forms.TextBox();
            this.textBoxLibraryPath = new System.Windows.Forms.TextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.buttonDisplayAdmin = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.textBoxResults = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.buttonRegister = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.labelXmdsUrl = new System.Windows.Forms.Label();
            this.textBoxDisplayName = new System.Windows.Forms.TextBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.label8 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.textBoxProxyDomain = new System.Windows.Forms.TextBox();
            this.maskedTextBoxProxyPass = new System.Windows.Forms.MaskedTextBox();
            this.textBoxProxyUser = new System.Windows.Forms.TextBox();
            this.labelProxyDomain = new System.Windows.Forms.Label();
            this.labelProxyPass = new System.Windows.Forms.Label();
            this.labelProxyUser = new System.Windows.Forms.Label();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.label14 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.offsetX = new System.Windows.Forms.NumericUpDown();
            this.offsetY = new System.Windows.Forms.NumericUpDown();
            this.clientHeight = new System.Windows.Forms.NumericUpDown();
            this.clientWidth = new System.Windows.Forms.NumericUpDown();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.doubleBufferingCheckBox = new System.Windows.Forms.CheckBox();
            this.enableMouseCb = new System.Windows.Forms.CheckBox();
            this.cbExpireModifiedLayouts = new System.Windows.Forms.CheckBox();
            this.numericUpDownEmptyRegions = new System.Windows.Forms.NumericUpDown();
            this.label15 = new System.Windows.Forms.Label();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.onlineHelpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.folderBrowserLibrary = new System.Windows.Forms.FolderBrowserDialog();
            this.xmds1 = new XiboClient.xmds.xmds();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nupScrollStepAmount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownCollect)).BeginInit();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.offsetX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.offsetY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.clientHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.clientWidth)).BeginInit();
            this.tabPage5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownEmptyRegions)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(136, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "The URI of the Xibo Server";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 41);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(172, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "The Server Key for the Xibo Server";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 68);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(111, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Local Library Location";
            // 
            // buttonSaveSettings
            // 
            this.buttonSaveSettings.Location = new System.Drawing.Point(200, 320);
            this.buttonSaveSettings.Name = "buttonSaveSettings";
            this.buttonSaveSettings.Size = new System.Drawing.Size(75, 23);
            this.buttonSaveSettings.TabIndex = 6;
            this.buttonSaveSettings.Text = "Save";
            this.buttonSaveSettings.UseVisualStyleBackColor = true;
            this.buttonSaveSettings.Click += new System.EventHandler(this.buttonSaveSettings_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Location = new System.Drawing.Point(12, 28);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(444, 287);
            this.tabControl1.TabIndex = 7;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.nupScrollStepAmount);
            this.tabPage1.Controls.Add(this.label10);
            this.tabPage1.Controls.Add(this.label9);
            this.tabPage1.Controls.Add(this.buttonLibrary);
            this.tabPage1.Controls.Add(this.numericUpDownCollect);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.tbHardwareKey);
            this.tabPage1.Controls.Add(this.checkBoxStats);
            this.tabPage1.Controls.Add(this.checkBoxPowerPoint);
            this.tabPage1.Controls.Add(this.textBoxXmdsUri);
            this.tabPage1.Controls.Add(this.textBoxServerKey);
            this.tabPage1.Controls.Add(this.textBoxLibraryPath);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(436, 261);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Xibo Settings";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // nupScrollStepAmount
            // 
            this.nupScrollStepAmount.Location = new System.Drawing.Point(184, 145);
            this.nupScrollStepAmount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nupScrollStepAmount.Name = "nupScrollStepAmount";
            this.nupScrollStepAmount.Size = new System.Drawing.Size(76, 20);
            this.nupScrollStepAmount.TabIndex = 16;
            this.nupScrollStepAmount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(6, 147);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(117, 13);
            this.label10.TabIndex = 15;
            this.label10.Text = "Scroll Step Amount (px)";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(6, 122);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(147, 13);
            this.label9.TabIndex = 13;
            this.label9.Text = "The Unique Key for this Client";
            // 
            // buttonLibrary
            // 
            this.buttonLibrary.Location = new System.Drawing.Point(346, 64);
            this.buttonLibrary.Name = "buttonLibrary";
            this.buttonLibrary.Size = new System.Drawing.Size(75, 21);
            this.buttonLibrary.TabIndex = 10;
            this.buttonLibrary.Text = "Browse";
            this.buttonLibrary.UseVisualStyleBackColor = true;
            this.buttonLibrary.Click += new System.EventHandler(this.buttonLibrary_Click);
            // 
            // numericUpDownCollect
            // 
            this.numericUpDownCollect.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::XiboClient.Properties.Settings.Default, "collectInterval", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.numericUpDownCollect.Location = new System.Drawing.Point(185, 92);
            this.numericUpDownCollect.Maximum = new decimal(new int[] {
            86400,
            0,
            0,
            0});
            this.numericUpDownCollect.Minimum = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this.numericUpDownCollect.Name = "numericUpDownCollect";
            this.numericUpDownCollect.Size = new System.Drawing.Size(74, 20);
            this.numericUpDownCollect.TabIndex = 9;
            this.numericUpDownCollect.Value = new decimal(new int[] {
            60,
            0,
            0,
            0});
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 94);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(165, 13);
            this.label5.TabIndex = 8;
            this.label5.Text = "The collection interval for content";
            // 
            // tbHardwareKey
            // 
            this.tbHardwareKey.Location = new System.Drawing.Point(184, 119);
            this.tbHardwareKey.Name = "tbHardwareKey";
            this.tbHardwareKey.Size = new System.Drawing.Size(237, 20);
            this.tbHardwareKey.TabIndex = 14;
            // 
            // checkBoxStats
            // 
            this.checkBoxStats.AutoSize = true;
            this.checkBoxStats.Checked = true;
            this.checkBoxStats.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxStats.Location = new System.Drawing.Point(184, 204);
            this.checkBoxStats.Name = "checkBoxStats";
            this.checkBoxStats.Size = new System.Drawing.Size(104, 17);
            this.checkBoxStats.TabIndex = 0;
            this.checkBoxStats.Text = "Enable Statistics";
            this.checkBoxStats.UseVisualStyleBackColor = true;
            // 
            // checkBoxPowerPoint
            // 
            this.checkBoxPowerPoint.AutoSize = true;
            this.checkBoxPowerPoint.Location = new System.Drawing.Point(184, 181);
            this.checkBoxPowerPoint.Name = "checkBoxPowerPoint";
            this.checkBoxPowerPoint.Size = new System.Drawing.Size(116, 17);
            this.checkBoxPowerPoint.TabIndex = 12;
            this.checkBoxPowerPoint.Text = "Enable PowerPoint";
            this.checkBoxPowerPoint.UseVisualStyleBackColor = true;
            // 
            // textBoxXmdsUri
            // 
            this.textBoxXmdsUri.Location = new System.Drawing.Point(184, 11);
            this.textBoxXmdsUri.Name = "textBoxXmdsUri";
            this.textBoxXmdsUri.Size = new System.Drawing.Size(237, 20);
            this.textBoxXmdsUri.TabIndex = 0;
            this.textBoxXmdsUri.Text = "http://localhost/xibo";
            // 
            // textBoxServerKey
            // 
            this.textBoxServerKey.Location = new System.Drawing.Point(184, 38);
            this.textBoxServerKey.Name = "textBoxServerKey";
            this.textBoxServerKey.Size = new System.Drawing.Size(237, 20);
            this.textBoxServerKey.TabIndex = 2;
            this.textBoxServerKey.Text = "yourserverkey";
            // 
            // textBoxLibraryPath
            // 
            this.textBoxLibraryPath.Location = new System.Drawing.Point(184, 65);
            this.textBoxLibraryPath.Name = "textBoxLibraryPath";
            this.textBoxLibraryPath.Size = new System.Drawing.Size(156, 20);
            this.textBoxLibraryPath.TabIndex = 4;
            this.textBoxLibraryPath.Text = "DEFAULT";
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.buttonDisplayAdmin);
            this.tabPage2.Controls.Add(this.label7);
            this.tabPage2.Controls.Add(this.textBoxResults);
            this.tabPage2.Controls.Add(this.label6);
            this.tabPage2.Controls.Add(this.buttonRegister);
            this.tabPage2.Controls.Add(this.label4);
            this.tabPage2.Controls.Add(this.labelXmdsUrl);
            this.tabPage2.Controls.Add(this.textBoxDisplayName);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(436, 261);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Register Display";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // buttonDisplayAdmin
            // 
            this.buttonDisplayAdmin.Location = new System.Drawing.Point(223, 63);
            this.buttonDisplayAdmin.Name = "buttonDisplayAdmin";
            this.buttonDisplayAdmin.Size = new System.Drawing.Size(90, 23);
            this.buttonDisplayAdmin.TabIndex = 7;
            this.buttonDisplayAdmin.Text = "Display Admin";
            this.buttonDisplayAdmin.UseVisualStyleBackColor = true;
            this.buttonDisplayAdmin.Click += new System.EventHandler(this.buttonDisplayAdmin_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(6, 108);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(45, 13);
            this.label7.TabIndex = 6;
            this.label7.Text = "Results:";
            // 
            // textBoxResults
            // 
            this.textBoxResults.Location = new System.Drawing.Point(9, 124);
            this.textBoxResults.Multiline = true;
            this.textBoxResults.Name = "textBoxResults";
            this.textBoxResults.Size = new System.Drawing.Size(397, 96);
            this.textBoxResults.TabIndex = 5;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(6, 10);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(92, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "Xibo Service URL";
            // 
            // buttonRegister
            // 
            this.buttonRegister.Location = new System.Drawing.Point(142, 63);
            this.buttonRegister.Name = "buttonRegister";
            this.buttonRegister.Size = new System.Drawing.Size(75, 23);
            this.buttonRegister.TabIndex = 2;
            this.buttonRegister.Text = "Register";
            this.buttonRegister.UseVisualStyleBackColor = true;
            this.buttonRegister.Click += new System.EventHandler(this.buttonRegister_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 40);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(72, 13);
            this.label4.TabIndex = 1;
            this.label4.Text = "Display Name";
            // 
            // labelXmdsUrl
            // 
            this.labelXmdsUrl.AutoSize = true;
            this.labelXmdsUrl.Location = new System.Drawing.Point(139, 10);
            this.labelXmdsUrl.Name = "labelXmdsUrl";
            this.labelXmdsUrl.Size = new System.Drawing.Size(299, 13);
            this.labelXmdsUrl.TabIndex = 3;
            this.labelXmdsUrl.Text = "http://localhost/Series%201.3/131-datasets/server/xmds.php";
            // 
            // textBoxDisplayName
            // 
            this.textBoxDisplayName.Location = new System.Drawing.Point(142, 37);
            this.textBoxDisplayName.Name = "textBoxDisplayName";
            this.textBoxDisplayName.Size = new System.Drawing.Size(264, 20);
            this.textBoxDisplayName.TabIndex = 0;
            this.textBoxDisplayName.Text = "COMPUTERNAME";
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.label8);
            this.tabPage3.Controls.Add(this.groupBox2);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(436, 261);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Proxy Server";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(26, 17);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(278, 13);
            this.label8.TabIndex = 9;
            this.label8.Text = "Please set the Proxy Address and Port in Internet Explorer";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.textBoxProxyDomain);
            this.groupBox2.Controls.Add(this.maskedTextBoxProxyPass);
            this.groupBox2.Controls.Add(this.textBoxProxyUser);
            this.groupBox2.Controls.Add(this.labelProxyDomain);
            this.groupBox2.Controls.Add(this.labelProxyPass);
            this.groupBox2.Controls.Add(this.labelProxyUser);
            this.groupBox2.Location = new System.Drawing.Point(29, 48);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(276, 139);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Authentication Information";
            // 
            // textBoxProxyDomain
            // 
            this.textBoxProxyDomain.Location = new System.Drawing.Point(99, 95);
            this.textBoxProxyDomain.Name = "textBoxProxyDomain";
            this.textBoxProxyDomain.Size = new System.Drawing.Size(171, 20);
            this.textBoxProxyDomain.TabIndex = 7;
            // 
            // maskedTextBoxProxyPass
            // 
            this.maskedTextBoxProxyPass.Location = new System.Drawing.Point(99, 67);
            this.maskedTextBoxProxyPass.Name = "maskedTextBoxProxyPass";
            this.maskedTextBoxProxyPass.Size = new System.Drawing.Size(171, 20);
            this.maskedTextBoxProxyPass.TabIndex = 5;
            this.maskedTextBoxProxyPass.UseSystemPasswordChar = true;
            // 
            // textBoxProxyUser
            // 
            this.textBoxProxyUser.Location = new System.Drawing.Point(99, 41);
            this.textBoxProxyUser.Name = "textBoxProxyUser";
            this.textBoxProxyUser.Size = new System.Drawing.Size(171, 20);
            this.textBoxProxyUser.TabIndex = 3;
            // 
            // labelProxyDomain
            // 
            this.labelProxyDomain.AutoSize = true;
            this.labelProxyDomain.Location = new System.Drawing.Point(7, 98);
            this.labelProxyDomain.Name = "labelProxyDomain";
            this.labelProxyDomain.Size = new System.Drawing.Size(43, 13);
            this.labelProxyDomain.TabIndex = 2;
            this.labelProxyDomain.Text = "Domain";
            // 
            // labelProxyPass
            // 
            this.labelProxyPass.AutoSize = true;
            this.labelProxyPass.Location = new System.Drawing.Point(7, 70);
            this.labelProxyPass.Name = "labelProxyPass";
            this.labelProxyPass.Size = new System.Drawing.Size(53, 13);
            this.labelProxyPass.TabIndex = 1;
            this.labelProxyPass.Text = "Password";
            // 
            // labelProxyUser
            // 
            this.labelProxyUser.AutoSize = true;
            this.labelProxyUser.Location = new System.Drawing.Point(7, 44);
            this.labelProxyUser.Name = "labelProxyUser";
            this.labelProxyUser.Size = new System.Drawing.Size(55, 13);
            this.labelProxyUser.TabIndex = 0;
            this.labelProxyUser.Text = "Username";
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.label14);
            this.tabPage4.Controls.Add(this.label13);
            this.tabPage4.Controls.Add(this.label12);
            this.tabPage4.Controls.Add(this.label11);
            this.tabPage4.Controls.Add(this.offsetX);
            this.tabPage4.Controls.Add(this.offsetY);
            this.tabPage4.Controls.Add(this.clientHeight);
            this.tabPage4.Controls.Add(this.clientWidth);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(436, 261);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Client Positioning";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(9, 96);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(45, 13);
            this.label14.TabIndex = 3;
            this.label14.Text = "Offset Y";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(9, 69);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(45, 13);
            this.label13.TabIndex = 2;
            this.label13.Text = "Offset X";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(9, 43);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(67, 13);
            this.label12.TabIndex = 1;
            this.label12.Text = "Client Height";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(9, 17);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(64, 13);
            this.label11.TabIndex = 0;
            this.label11.Text = "Client Width";
            // 
            // offsetX
            // 
            this.offsetX.Location = new System.Drawing.Point(77, 67);
            this.offsetX.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.offsetX.Name = "offsetX";
            this.offsetX.Size = new System.Drawing.Size(77, 20);
            this.offsetX.TabIndex = 6;
            // 
            // offsetY
            // 
            this.offsetY.Location = new System.Drawing.Point(77, 94);
            this.offsetY.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.offsetY.Name = "offsetY";
            this.offsetY.Size = new System.Drawing.Size(77, 20);
            this.offsetY.TabIndex = 7;
            // 
            // clientHeight
            // 
            this.clientHeight.Location = new System.Drawing.Point(77, 41);
            this.clientHeight.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.clientHeight.Name = "clientHeight";
            this.clientHeight.Size = new System.Drawing.Size(77, 20);
            this.clientHeight.TabIndex = 5;
            // 
            // clientWidth
            // 
            this.clientWidth.Location = new System.Drawing.Point(77, 15);
            this.clientWidth.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.clientWidth.Name = "clientWidth";
            this.clientWidth.Size = new System.Drawing.Size(77, 20);
            this.clientWidth.TabIndex = 4;
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.doubleBufferingCheckBox);
            this.tabPage5.Controls.Add(this.enableMouseCb);
            this.tabPage5.Controls.Add(this.cbExpireModifiedLayouts);
            this.tabPage5.Controls.Add(this.numericUpDownEmptyRegions);
            this.tabPage5.Controls.Add(this.label15);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage5.Size = new System.Drawing.Size(436, 261);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Advanced";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // doubleBufferingCheckBox
            // 
            this.doubleBufferingCheckBox.AutoSize = true;
            this.doubleBufferingCheckBox.Location = new System.Drawing.Point(176, 140);
            this.doubleBufferingCheckBox.Name = "doubleBufferingCheckBox";
            this.doubleBufferingCheckBox.Size = new System.Drawing.Size(141, 17);
            this.doubleBufferingCheckBox.TabIndex = 4;
            this.doubleBufferingCheckBox.Text = "Enable Double Buffering";
            this.doubleBufferingCheckBox.UseVisualStyleBackColor = true;
            // 
            // enableMouseCb
            // 
            this.enableMouseCb.AutoSize = true;
            this.enableMouseCb.Checked = true;
            this.enableMouseCb.CheckState = System.Windows.Forms.CheckState.Checked;
            this.enableMouseCb.Location = new System.Drawing.Point(176, 103);
            this.enableMouseCb.Name = "enableMouseCb";
            this.enableMouseCb.Size = new System.Drawing.Size(94, 17);
            this.enableMouseCb.TabIndex = 3;
            this.enableMouseCb.Text = "Enable Mouse";
            this.enableMouseCb.UseVisualStyleBackColor = true;
            // 
            // cbExpireModifiedLayouts
            // 
            this.cbExpireModifiedLayouts.AutoSize = true;
            this.cbExpireModifiedLayouts.Checked = true;
            this.cbExpireModifiedLayouts.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbExpireModifiedLayouts.Location = new System.Drawing.Point(176, 66);
            this.cbExpireModifiedLayouts.Name = "cbExpireModifiedLayouts";
            this.cbExpireModifiedLayouts.Size = new System.Drawing.Size(243, 17);
            this.cbExpireModifiedLayouts.TabIndex = 2;
            this.cbExpireModifiedLayouts.Text = "Expire modified layouts while they are playing?";
            this.cbExpireModifiedLayouts.UseVisualStyleBackColor = true;
            // 
            // numericUpDownEmptyRegions
            // 
            this.numericUpDownEmptyRegions.Location = new System.Drawing.Point(176, 23);
            this.numericUpDownEmptyRegions.Name = "numericUpDownEmptyRegions";
            this.numericUpDownEmptyRegions.Size = new System.Drawing.Size(91, 20);
            this.numericUpDownEmptyRegions.TabIndex = 1;
            this.numericUpDownEmptyRegions.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(20, 25);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(136, 13);
            this.label15.TabIndex = 0;
            this.label15.Text = "Duration for Empty Regions";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(469, 24);
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
            // xmds1
            // 
            this.xmds1.Credentials = null;
            this.xmds1.Url = "http://localhost/Xibo/server/xmds.php";
            this.xmds1.UseDefaultCredentials = false;
            // 
            // OptionForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(469, 355);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.buttonSaveSettings);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "OptionForm";
            this.Text = "Xibo Client Options";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nupScrollStepAmount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownCollect)).EndInit();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.offsetX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.offsetY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.clientHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.clientWidth)).EndInit();
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownEmptyRegions)).EndInit();
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
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label labelXmdsUrl;
        private System.Windows.Forms.Button buttonRegister;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBoxDisplayName;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox textBoxResults;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown numericUpDownCollect;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserLibrary;
        private System.Windows.Forms.Button buttonLibrary;
        private System.Windows.Forms.ToolStripMenuItem onlineHelpToolStripMenuItem;
        private System.Windows.Forms.CheckBox checkBoxPowerPoint;
        private System.Windows.Forms.CheckBox checkBoxStats;
        private System.Windows.Forms.Button buttonDisplayAdmin;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.MaskedTextBox maskedTextBoxProxyPass;
        private System.Windows.Forms.TextBox textBoxProxyUser;
        private System.Windows.Forms.Label labelProxyDomain;
        private System.Windows.Forms.Label labelProxyPass;
        private System.Windows.Forms.Label labelProxyUser;
        private System.Windows.Forms.TextBox textBoxProxyDomain;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox tbHardwareKey;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.NumericUpDown nupScrollStepAmount;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.NumericUpDown clientWidth;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.NumericUpDown offsetY;
        private System.Windows.Forms.NumericUpDown offsetX;
        private System.Windows.Forms.NumericUpDown clientHeight;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.NumericUpDown numericUpDownEmptyRegions;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.CheckBox cbExpireModifiedLayouts;
        private System.Windows.Forms.CheckBox enableMouseCb;
        private System.Windows.Forms.CheckBox doubleBufferingCheckBox;
    }
}