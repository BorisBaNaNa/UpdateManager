namespace UpdateManager.Forms
{
    partial class FtpConnectionForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FtpConnectionForm));
            this.lblHost = new System.Windows.Forms.Label();
            this.txtHost = new System.Windows.Forms.TextBox();
            this.lblPort = new System.Windows.Forms.Label();
            this.numPort = new System.Windows.Forms.NumericUpDown();
            this.lblUser = new System.Windows.Forms.Label();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.lblPass = new System.Windows.Forms.Label();
            this.txtPass = new System.Windows.Forms.TextBox();
            this.lblRemote = new System.Windows.Forms.Label();
            this.txtRemote = new System.Windows.Forms.TextBox();
            this.btnBrowseRemote = new System.Windows.Forms.Button();
            this.lblComputed = new System.Windows.Forms.Label();
            this.lblHint = new System.Windows.Forms.Label();
            this.btnTest = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numPort)).BeginInit();
            this.SuspendLayout();
            // 
            // lblHost
            // 
            this.lblHost.AutoSize = true;
            this.lblHost.Location = new System.Drawing.Point(12, 15);
            this.lblHost.Name = "lblHost";
            this.lblHost.Size = new System.Drawing.Size(34, 13);
            this.lblHost.TabIndex = 15;
            this.lblHost.Text = "Хост:";
            // 
            // txtHost
            // 
            this.txtHost.Location = new System.Drawing.Point(130, 12);
            this.txtHost.Name = "txtHost";
            this.txtHost.Size = new System.Drawing.Size(190, 20);
            this.txtHost.TabIndex = 0;
            // 
            // lblPort
            // 
            this.lblPort.AutoSize = true;
            this.lblPort.Location = new System.Drawing.Point(326, 15);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(35, 13);
            this.lblPort.TabIndex = 14;
            this.lblPort.Text = "Порт:";
            // 
            // numPort
            // 
            this.numPort.Location = new System.Drawing.Point(366, 12);
            this.numPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numPort.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numPort.Name = "numPort";
            this.numPort.Size = new System.Drawing.Size(66, 20);
            this.numPort.TabIndex = 1;
            this.numPort.Value = new decimal(new int[] {
            21,
            0,
            0,
            0});
            // 
            // lblUser
            // 
            this.lblUser.AutoSize = true;
            this.lblUser.Location = new System.Drawing.Point(12, 48);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(83, 13);
            this.lblUser.TabIndex = 13;
            this.lblUser.Text = "Пользователь:";
            // 
            // txtUser
            // 
            this.txtUser.Location = new System.Drawing.Point(130, 45);
            this.txtUser.Name = "txtUser";
            this.txtUser.Size = new System.Drawing.Size(302, 20);
            this.txtUser.TabIndex = 2;
            // 
            // lblPass
            // 
            this.lblPass.AutoSize = true;
            this.lblPass.Location = new System.Drawing.Point(12, 81);
            this.lblPass.Name = "lblPass";
            this.lblPass.Size = new System.Drawing.Size(48, 13);
            this.lblPass.TabIndex = 12;
            this.lblPass.Text = "Пароль:";
            // 
            // txtPass
            // 
            this.txtPass.Location = new System.Drawing.Point(130, 78);
            this.txtPass.Name = "txtPass";
            this.txtPass.Size = new System.Drawing.Size(302, 20);
            this.txtPass.TabIndex = 3;
            this.txtPass.UseSystemPasswordChar = true;
            // 
            // lblRemote
            // 
            this.lblRemote.AutoSize = true;
            this.lblRemote.Location = new System.Drawing.Point(12, 114);
            this.lblRemote.Name = "lblRemote";
            this.lblRemote.Size = new System.Drawing.Size(96, 13);
            this.lblRemote.TabIndex = 11;
            this.lblRemote.Text = "Папка с патчами:";
            // 
            // txtRemote
            // 
            this.txtRemote.Location = new System.Drawing.Point(130, 111);
            this.txtRemote.Name = "txtRemote";
            this.txtRemote.Size = new System.Drawing.Size(266, 20);
            this.txtRemote.TabIndex = 4;
            // 
            // btnBrowseRemote
            // 
            this.btnBrowseRemote.Location = new System.Drawing.Point(402, 110);
            this.btnBrowseRemote.Name = "btnBrowseRemote";
            this.btnBrowseRemote.Size = new System.Drawing.Size(30, 22);
            this.btnBrowseRemote.TabIndex = 5;
            this.btnBrowseRemote.Text = "…";
            this.btnBrowseRemote.UseVisualStyleBackColor = true;
            // 
            // lblComputed
            // 
            this.lblComputed.AutoEllipsis = true;
            this.lblComputed.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblComputed.Location = new System.Drawing.Point(12, 138);
            this.lblComputed.Name = "lblComputed";
            this.lblComputed.Size = new System.Drawing.Size(420, 15);
            this.lblComputed.TabIndex = 10;
            this.lblComputed.Text = "Итоговый путь загрузки: —";
            // 
            // lblHint
            // 
            this.lblHint.AutoSize = true;
            this.lblHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHint.Location = new System.Drawing.Point(12, 162);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(395, 13);
            this.lblHint.TabIndex = 9;
            this.lblHint.Text = "Реквизиты хранятся в профиле пользователя; пароль зашифрован (DPAPI).";
            // 
            // btnTest
            // 
            this.btnTest.Location = new System.Drawing.Point(12, 192);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(160, 28);
            this.btnTest.TabIndex = 8;
            this.btnTest.Text = "Проверить соединение";
            this.btnTest.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(246, 192);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 28);
            this.btnOk.TabIndex = 6;
            this.btnOk.Text = "Сохранить";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(342, 192);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Отмена";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // FtpConnectionForm
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(444, 232);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnTest);
            this.Controls.Add(this.lblHint);
            this.Controls.Add(this.lblComputed);
            this.Controls.Add(this.btnBrowseRemote);
            this.Controls.Add(this.txtRemote);
            this.Controls.Add(this.lblRemote);
            this.Controls.Add(this.txtPass);
            this.Controls.Add(this.lblPass);
            this.Controls.Add(this.txtUser);
            this.Controls.Add(this.lblUser);
            this.Controls.Add(this.numPort);
            this.Controls.Add(this.lblPort);
            this.Controls.Add(this.txtHost);
            this.Controls.Add(this.lblHost);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FtpConnectionForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "FTP-подключение";
            ((System.ComponentModel.ISupportInitialize)(this.numPort)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblHost;
        private System.Windows.Forms.TextBox txtHost;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.NumericUpDown numPort;
        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.TextBox txtUser;
        private System.Windows.Forms.Label lblPass;
        private System.Windows.Forms.TextBox txtPass;
        private System.Windows.Forms.Label lblRemote;
        private System.Windows.Forms.TextBox txtRemote;
        private System.Windows.Forms.Button btnBrowseRemote;
        private System.Windows.Forms.Label lblComputed;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
