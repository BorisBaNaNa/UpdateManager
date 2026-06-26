namespace UpdateManager.Forms
{
    partial class ProjectSettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        private void InitializeComponent()
        {
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.lblServer = new System.Windows.Forms.Label();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.lblUploadDir = new System.Windows.Forms.Label();
            this.txtUploadDir = new System.Windows.Forms.TextBox();
            this.lblMaintUrl = new System.Windows.Forms.Label();
            this.txtMaintUrl = new System.Windows.Forms.TextBox();
            this.chkSelfPatching = new System.Windows.Forms.CheckBox();
            this.grpPatchTypes = new System.Windows.Forms.GroupBox();
            this.chkRepair = new System.Windows.Forms.CheckBox();
            this.chkInstaller = new System.Windows.Forms.CheckBox();
            this.chkIncremental = new System.Windows.Forms.CheckBox();
            this.chkAllIncremental = new System.Windows.Forms.CheckBox();
            this.grpAdvanced = new System.Windows.Forms.GroupBox();
            this.lblCompRepair = new System.Windows.Forms.Label();
            this.cmbCompRepair = new System.Windows.Forms.ComboBox();
            this.lblCompInstaller = new System.Windows.Forms.Label();
            this.cmbCompInstaller = new System.Windows.Forms.ComboBox();
            this.lblCompIncremental = new System.Windows.Forms.Label();
            this.cmbCompIncremental = new System.Windows.Forms.ComboBox();
            this.lblBinaryDiff = new System.Windows.Forms.Label();
            this.numBinaryDiff = new System.Windows.Forms.NumericUpDown();
            this.chkDontPatchUnchanged = new System.Windows.Forms.CheckBox();
            this.lblIgnored = new System.Windows.Forms.Label();
            this.txtIgnored = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpPatchTypes.SuspendLayout();
            this.grpAdvanced.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numBinaryDiff)).BeginInit();
            this.SuspendLayout();
            //
            // lblName
            //
            this.lblName.AutoSize = true;
            this.lblName.Location = new System.Drawing.Point(12, 15);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(75, 13);
            this.lblName.Text = "Имя проекта:";
            //
            // txtName
            //
            this.txtName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtName.Location = new System.Drawing.Point(150, 12);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(318, 20);
            this.txtName.TabIndex = 0;
            //
            // lblServer
            //
            this.lblServer.AutoSize = true;
            this.lblServer.Location = new System.Drawing.Point(12, 45);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(83, 13);
            this.lblServer.Text = "Адрес сервера:";
            //
            // txtServer
            //
            this.txtServer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtServer.Location = new System.Drawing.Point(150, 42);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(318, 20);
            this.txtServer.TabIndex = 1;
            //
            // lblUploadDir
            //
            this.lblUploadDir.AutoSize = true;
            this.lblUploadDir.Location = new System.Drawing.Point(12, 75);
            this.lblUploadDir.Name = "lblUploadDir";
            this.lblUploadDir.Size = new System.Drawing.Size(123, 13);
            this.lblUploadDir.Text = "Директория загрузки:";
            //
            // txtUploadDir
            //
            this.txtUploadDir.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtUploadDir.Location = new System.Drawing.Point(150, 72);
            this.txtUploadDir.Name = "txtUploadDir";
            this.txtUploadDir.Size = new System.Drawing.Size(318, 20);
            this.txtUploadDir.TabIndex = 2;
            //
            // lblMaintUrl
            //
            this.lblMaintUrl.AutoSize = true;
            this.lblMaintUrl.Location = new System.Drawing.Point(12, 105);
            this.lblMaintUrl.Name = "lblMaintUrl";
            this.lblMaintUrl.Size = new System.Drawing.Size(120, 13);
            this.lblMaintUrl.Text = "MaintenanceCheckURL:";
            //
            // txtMaintUrl
            //
            this.txtMaintUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMaintUrl.Location = new System.Drawing.Point(150, 102);
            this.txtMaintUrl.Name = "txtMaintUrl";
            this.txtMaintUrl.Size = new System.Drawing.Size(318, 20);
            this.txtMaintUrl.TabIndex = 3;
            //
            // chkSelfPatching
            //
            this.chkSelfPatching.AutoSize = true;
            this.chkSelfPatching.Location = new System.Drawing.Point(15, 135);
            this.chkSelfPatching.Name = "chkSelfPatching";
            this.chkSelfPatching.Size = new System.Drawing.Size(168, 17);
            this.chkSelfPatching.TabIndex = 4;
            this.chkSelfPatching.Text = "Self-patching приложение";
            this.chkSelfPatching.UseVisualStyleBackColor = true;
            //
            // grpPatchTypes
            //
            this.grpPatchTypes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpPatchTypes.Controls.Add(this.chkRepair);
            this.grpPatchTypes.Controls.Add(this.chkInstaller);
            this.grpPatchTypes.Controls.Add(this.chkIncremental);
            this.grpPatchTypes.Controls.Add(this.chkAllIncremental);
            this.grpPatchTypes.Location = new System.Drawing.Point(15, 165);
            this.grpPatchTypes.Name = "grpPatchTypes";
            this.grpPatchTypes.Size = new System.Drawing.Size(453, 88);
            this.grpPatchTypes.TabIndex = 5;
            this.grpPatchTypes.TabStop = false;
            this.grpPatchTypes.Text = "Типы патчей";
            //
            // chkRepair
            //
            this.chkRepair.AutoSize = true;
            this.chkRepair.Location = new System.Drawing.Point(15, 25);
            this.chkRepair.Name = "chkRepair";
            this.chkRepair.Size = new System.Drawing.Size(60, 17);
            this.chkRepair.TabIndex = 0;
            this.chkRepair.Text = "Repair";
            this.chkRepair.UseVisualStyleBackColor = true;
            //
            // chkInstaller
            //
            this.chkInstaller.AutoSize = true;
            this.chkInstaller.Location = new System.Drawing.Point(130, 25);
            this.chkInstaller.Name = "chkInstaller";
            this.chkInstaller.Size = new System.Drawing.Size(64, 17);
            this.chkInstaller.TabIndex = 1;
            this.chkInstaller.Text = "Installer";
            this.chkInstaller.UseVisualStyleBackColor = true;
            //
            // chkIncremental
            //
            this.chkIncremental.AutoSize = true;
            this.chkIncremental.Location = new System.Drawing.Point(260, 25);
            this.chkIncremental.Name = "chkIncremental";
            this.chkIncremental.Size = new System.Drawing.Size(83, 17);
            this.chkIncremental.TabIndex = 2;
            this.chkIncremental.Text = "Incremental";
            this.chkIncremental.UseVisualStyleBackColor = true;
            //
            // chkAllIncremental
            //
            this.chkAllIncremental.AutoSize = true;
            this.chkAllIncremental.Location = new System.Drawing.Point(15, 52);
            this.chkAllIncremental.Name = "chkAllIncremental";
            this.chkAllIncremental.Size = new System.Drawing.Size(286, 17);
            this.chkAllIncremental.TabIndex = 3;
            this.chkAllIncremental.Text = "Инкрементальные от каждой предыдущей версии";
            this.chkAllIncremental.UseVisualStyleBackColor = true;
            //
            // grpAdvanced
            //
            this.grpAdvanced.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpAdvanced.Controls.Add(this.lblCompRepair);
            this.grpAdvanced.Controls.Add(this.cmbCompRepair);
            this.grpAdvanced.Controls.Add(this.lblCompInstaller);
            this.grpAdvanced.Controls.Add(this.cmbCompInstaller);
            this.grpAdvanced.Controls.Add(this.lblCompIncremental);
            this.grpAdvanced.Controls.Add(this.cmbCompIncremental);
            this.grpAdvanced.Controls.Add(this.lblBinaryDiff);
            this.grpAdvanced.Controls.Add(this.numBinaryDiff);
            this.grpAdvanced.Controls.Add(this.chkDontPatchUnchanged);
            this.grpAdvanced.Location = new System.Drawing.Point(15, 262);
            this.grpAdvanced.Name = "grpAdvanced";
            this.grpAdvanced.Size = new System.Drawing.Size(453, 140);
            this.grpAdvanced.TabIndex = 6;
            this.grpAdvanced.TabStop = false;
            this.grpAdvanced.Text = "Дополнительно";
            //
            // lblCompRepair
            //
            this.lblCompRepair.AutoSize = true;
            this.lblCompRepair.Location = new System.Drawing.Point(12, 28);
            this.lblCompRepair.Name = "lblCompRepair";
            this.lblCompRepair.Size = new System.Drawing.Size(91, 13);
            this.lblCompRepair.Text = "Сжатие Repair:";
            //
            // cmbCompRepair
            //
            this.cmbCompRepair.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCompRepair.Location = new System.Drawing.Point(150, 25);
            this.cmbCompRepair.Name = "cmbCompRepair";
            this.cmbCompRepair.Size = new System.Drawing.Size(100, 21);
            this.cmbCompRepair.TabIndex = 0;
            //
            // lblCompInstaller
            //
            this.lblCompInstaller.AutoSize = true;
            this.lblCompInstaller.Location = new System.Drawing.Point(12, 57);
            this.lblCompInstaller.Name = "lblCompInstaller";
            this.lblCompInstaller.Size = new System.Drawing.Size(95, 13);
            this.lblCompInstaller.Text = "Сжатие Installer:";
            //
            // cmbCompInstaller
            //
            this.cmbCompInstaller.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCompInstaller.Location = new System.Drawing.Point(150, 54);
            this.cmbCompInstaller.Name = "cmbCompInstaller";
            this.cmbCompInstaller.Size = new System.Drawing.Size(100, 21);
            this.cmbCompInstaller.TabIndex = 1;
            //
            // lblCompIncremental
            //
            this.lblCompIncremental.AutoSize = true;
            this.lblCompIncremental.Location = new System.Drawing.Point(12, 86);
            this.lblCompIncremental.Name = "lblCompIncremental";
            this.lblCompIncremental.Size = new System.Drawing.Size(114, 13);
            this.lblCompIncremental.Text = "Сжатие Incremental:";
            //
            // cmbCompIncremental
            //
            this.cmbCompIncremental.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCompIncremental.Location = new System.Drawing.Point(150, 83);
            this.cmbCompIncremental.Name = "cmbCompIncremental";
            this.cmbCompIncremental.Size = new System.Drawing.Size(100, 21);
            this.cmbCompIncremental.TabIndex = 2;
            //
            // lblBinaryDiff
            //
            this.lblBinaryDiff.AutoSize = true;
            this.lblBinaryDiff.Location = new System.Drawing.Point(280, 28);
            this.lblBinaryDiff.Name = "lblBinaryDiff";
            this.lblBinaryDiff.Size = new System.Drawing.Size(96, 13);
            this.lblBinaryDiff.Text = "BinaryDiffQuality:";
            //
            // numBinaryDiff
            //
            this.numBinaryDiff.Location = new System.Drawing.Point(382, 26);
            this.numBinaryDiff.Maximum = new decimal(new int[] { 9, 0, 0, 0 });
            this.numBinaryDiff.Name = "numBinaryDiff";
            this.numBinaryDiff.Size = new System.Drawing.Size(55, 20);
            this.numBinaryDiff.TabIndex = 3;
            //
            // chkDontPatchUnchanged
            //
            this.chkDontPatchUnchanged.AutoSize = true;
            this.chkDontPatchUnchanged.Location = new System.Drawing.Point(12, 113);
            this.chkDontPatchUnchanged.Name = "chkDontPatchUnchanged";
            this.chkDontPatchUnchanged.Size = new System.Drawing.Size(327, 17);
            this.chkDontPatchUnchanged.TabIndex = 4;
            this.chkDontPatchUnchanged.Text = "Не создавать патч-файлы для неизменённых файлов";
            this.chkDontPatchUnchanged.UseVisualStyleBackColor = true;
            //
            // lblIgnored
            //
            this.lblIgnored.AutoSize = true;
            this.lblIgnored.Location = new System.Drawing.Point(12, 415);
            this.lblIgnored.Name = "lblIgnored";
            this.lblIgnored.Size = new System.Drawing.Size(238, 13);
            this.lblIgnored.Text = "Игнорируемые пути (по одному в строке):";
            //
            // txtIgnored
            //
            this.txtIgnored.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtIgnored.Location = new System.Drawing.Point(15, 435);
            this.txtIgnored.Multiline = true;
            this.txtIgnored.Name = "txtIgnored";
            this.txtIgnored.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtIgnored.Size = new System.Drawing.Size(453, 78);
            this.txtIgnored.TabIndex = 7;
            this.txtIgnored.WordWrap = false;
            //
            // btnOk
            //
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(282, 525);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 28);
            this.btnOk.TabIndex = 8;
            this.btnOk.Text = "Сохранить";
            this.btnOk.UseVisualStyleBackColor = true;
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(378, 525);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Отмена";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // ProjectSettingsForm
            //
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(480, 565);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.txtIgnored);
            this.Controls.Add(this.lblIgnored);
            this.Controls.Add(this.grpAdvanced);
            this.Controls.Add(this.grpPatchTypes);
            this.Controls.Add(this.chkSelfPatching);
            this.Controls.Add(this.txtMaintUrl);
            this.Controls.Add(this.lblMaintUrl);
            this.Controls.Add(this.txtUploadDir);
            this.Controls.Add(this.lblUploadDir);
            this.Controls.Add(this.txtServer);
            this.Controls.Add(this.lblServer);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.lblName);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(420, 510);
            this.Name = "ProjectSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Настройки проекта";
            this.grpPatchTypes.ResumeLayout(false);
            this.grpPatchTypes.PerformLayout();
            this.grpAdvanced.ResumeLayout(false);
            this.grpAdvanced.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numBinaryDiff)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Label lblUploadDir;
        private System.Windows.Forms.TextBox txtUploadDir;
        private System.Windows.Forms.Label lblMaintUrl;
        private System.Windows.Forms.TextBox txtMaintUrl;
        private System.Windows.Forms.CheckBox chkSelfPatching;
        private System.Windows.Forms.GroupBox grpPatchTypes;
        private System.Windows.Forms.CheckBox chkRepair;
        private System.Windows.Forms.CheckBox chkInstaller;
        private System.Windows.Forms.CheckBox chkIncremental;
        private System.Windows.Forms.CheckBox chkAllIncremental;
        private System.Windows.Forms.GroupBox grpAdvanced;
        private System.Windows.Forms.Label lblCompRepair;
        private System.Windows.Forms.ComboBox cmbCompRepair;
        private System.Windows.Forms.Label lblCompInstaller;
        private System.Windows.Forms.ComboBox cmbCompInstaller;
        private System.Windows.Forms.Label lblCompIncremental;
        private System.Windows.Forms.ComboBox cmbCompIncremental;
        private System.Windows.Forms.Label lblBinaryDiff;
        private System.Windows.Forms.NumericUpDown numBinaryDiff;
        private System.Windows.Forms.CheckBox chkDontPatchUnchanged;
        private System.Windows.Forms.Label lblIgnored;
        private System.Windows.Forms.TextBox txtIgnored;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
