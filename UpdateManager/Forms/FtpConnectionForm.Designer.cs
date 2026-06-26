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
            this.lblHost.Size = new System.Drawing.Size(35, 13);
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
            this.lblPort.Size = new System.Drawing.Size(34, 13);
            this.lblPort.Text = "Порт:";
            //
            // numPort
            //
            this.numPort.Location = new System.Drawing.Point(366, 12);
            this.numPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numPort.Name = "numPort";
            this.numPort.Size = new System.Drawing.Size(66, 20);
            this.numPort.TabIndex = 1;
            this.numPort.Value = new decimal(new int[] { 21, 0, 0, 0 });
            //
            // lblUser
            //
            this.lblUser.AutoSize = true;
            this.lblUser.Location = new System.Drawing.Point(12, 48);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(78, 13);
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
            this.lblRemote.Size = new System.Drawing.Size(106, 13);
            this.lblRemote.Text = "Папка на сервере:";
            //
            // txtRemote
            //
            this.txtRemote.Location = new System.Drawing.Point(130, 111);
            this.txtRemote.Name = "txtRemote";
            this.txtRemote.Size = new System.Drawing.Size(302, 20);
            this.txtRemote.TabIndex = 4;
            //
            // lblHint
            //
            this.lblHint.AutoSize = true;
            this.lblHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHint.Location = new System.Drawing.Point(12, 144);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(379, 13);
            this.lblHint.Text = "Реквизиты хранятся в профиле пользователя; пароль зашифрован (DPAPI).";
            //
            // btnTest
            //
            this.btnTest.Location = new System.Drawing.Point(12, 172);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(160, 28);
            this.btnTest.TabIndex = 7;
            this.btnTest.Text = "Проверить соединение";
            this.btnTest.UseVisualStyleBackColor = true;
            //
            // btnOk
            //
            this.btnOk.Location = new System.Drawing.Point(246, 172);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 28);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "Сохранить";
            this.btnOk.UseVisualStyleBackColor = true;
            //
            // btnCancel
            //
            this.btnCancel.Location = new System.Drawing.Point(342, 172);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Отмена";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // FtpConnectionForm
            //
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(444, 212);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnTest);
            this.Controls.Add(this.lblHint);
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
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
