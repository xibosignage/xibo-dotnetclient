namespace XiboClient.Log
{
    partial class ClientInfo
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ClientInfo));
            this.logDataGridView = new System.Windows.Forms.DataGridView();
            this.Thread = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Date = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Type = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Method = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Message = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label1 = new System.Windows.Forms.Label();
            this.scheduleStatusLabel = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.requiredFilesStatus = new System.Windows.Forms.Label();
            this.requiredFilesTextBox = new System.Windows.Forms.TextBox();
            this.scheduleManagerStatus = new System.Windows.Forms.TextBox();
            this.saveLogToDisk = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.logDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // logDataGridView
            // 
            this.logDataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.logDataGridView.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.Disable;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.logDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.logDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.logDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Thread,
            this.Date,
            this.Type,
            this.Method,
            this.Message});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.logDataGridView.DefaultCellStyle = dataGridViewCellStyle2;
            this.logDataGridView.Location = new System.Drawing.Point(12, 314);
            this.logDataGridView.Name = "logDataGridView";
            this.logDataGridView.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.logDataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.logDataGridView.Size = new System.Drawing.Size(786, 408);
            this.logDataGridView.TabIndex = 0;
            // 
            // Thread
            // 
            this.Thread.HeaderText = "Thread";
            this.Thread.Name = "Thread";
            this.Thread.ReadOnly = true;
            this.Thread.Width = 66;
            // 
            // Date
            // 
            this.Date.HeaderText = "Date";
            this.Date.Name = "Date";
            this.Date.ReadOnly = true;
            this.Date.Width = 55;
            // 
            // Type
            // 
            this.Type.HeaderText = "Type";
            this.Type.Name = "Type";
            this.Type.ReadOnly = true;
            this.Type.Width = 56;
            // 
            // Method
            // 
            this.Method.HeaderText = "Method";
            this.Method.Name = "Method";
            this.Method.ReadOnly = true;
            this.Method.Width = 68;
            // 
            // Message
            // 
            this.Message.HeaderText = "Message";
            this.Message.Name = "Message";
            this.Message.ReadOnly = true;
            this.Message.Width = 75;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 11);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(88, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Schedule Status:";
            // 
            // scheduleStatusLabel
            // 
            this.scheduleStatusLabel.AutoSize = true;
            this.scheduleStatusLabel.Location = new System.Drawing.Point(148, 11);
            this.scheduleStatusLabel.Name = "scheduleStatusLabel";
            this.scheduleStatusLabel.Size = new System.Drawing.Size(61, 13);
            this.scheduleStatusLabel.TabIndex = 2;
            this.scheduleStatusLabel.Text = "Not Started";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(403, 11);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(110, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Required Files Status:";
            // 
            // requiredFilesStatus
            // 
            this.requiredFilesStatus.AutoSize = true;
            this.requiredFilesStatus.Location = new System.Drawing.Point(519, 11);
            this.requiredFilesStatus.Name = "requiredFilesStatus";
            this.requiredFilesStatus.Size = new System.Drawing.Size(61, 13);
            this.requiredFilesStatus.TabIndex = 4;
            this.requiredFilesStatus.Text = "Not Started";
            // 
            // requiredFilesTextBox
            // 
            this.requiredFilesTextBox.Location = new System.Drawing.Point(406, 27);
            this.requiredFilesTextBox.Multiline = true;
            this.requiredFilesTextBox.Name = "requiredFilesTextBox";
            this.requiredFilesTextBox.ReadOnly = true;
            this.requiredFilesTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.requiredFilesTextBox.Size = new System.Drawing.Size(392, 281);
            this.requiredFilesTextBox.TabIndex = 5;
            // 
            // scheduleManagerStatus
            // 
            this.scheduleManagerStatus.Location = new System.Drawing.Point(12, 27);
            this.scheduleManagerStatus.Multiline = true;
            this.scheduleManagerStatus.Name = "scheduleManagerStatus";
            this.scheduleManagerStatus.ReadOnly = true;
            this.scheduleManagerStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.scheduleManagerStatus.Size = new System.Drawing.Size(387, 281);
            this.scheduleManagerStatus.TabIndex = 8;
            // 
            // saveLogToDisk
            // 
            this.saveLogToDisk.Location = new System.Drawing.Point(13, 729);
            this.saveLogToDisk.Name = "saveLogToDisk";
            this.saveLogToDisk.Size = new System.Drawing.Size(75, 23);
            this.saveLogToDisk.TabIndex = 9;
            this.saveLogToDisk.Text = "Save Log";
            this.saveLogToDisk.UseVisualStyleBackColor = true;
            this.saveLogToDisk.Click += new System.EventHandler(this.saveLogToDisk_Click);
            // 
            // ClientInfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(810, 758);
            this.Controls.Add(this.saveLogToDisk);
            this.Controls.Add(this.scheduleManagerStatus);
            this.Controls.Add(this.requiredFilesTextBox);
            this.Controls.Add(this.requiredFilesStatus);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.scheduleStatusLabel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.logDataGridView);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ClientInfo";
            this.Text = "Client Information and Status";
            ((System.ComponentModel.ISupportInitialize)(this.logDataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView logDataGridView;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label scheduleStatusLabel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label requiredFilesStatus;
        private System.Windows.Forms.TextBox requiredFilesTextBox;
        private System.Windows.Forms.DataGridViewTextBoxColumn Thread;
        private System.Windows.Forms.DataGridViewTextBoxColumn Date;
        private System.Windows.Forms.DataGridViewTextBoxColumn Type;
        private System.Windows.Forms.DataGridViewTextBoxColumn Method;
        private System.Windows.Forms.DataGridViewTextBoxColumn Message;
        private System.Windows.Forms.TextBox scheduleManagerStatus;
        private System.Windows.Forms.Button saveLogToDisk;
    }
}