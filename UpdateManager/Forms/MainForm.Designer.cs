namespace UpdateManager.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.projectMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createProjectMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openProjectMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.projectSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.recentProjectsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.projectSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lblProjectTitle = new System.Windows.Forms.Label();
            this.grpVersions = new System.Windows.Forms.GroupBox();
            this.listViewVersions = new System.Windows.Forms.ListView();
            this.colVersion = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colBuildDate = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDelivered = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lblSource = new System.Windows.Forms.Label();
            this.txtSource = new System.Windows.Forms.TextBox();
            this.btnBrowseSource = new System.Windows.Forms.Button();
            this.lblMainExe = new System.Windows.Forms.Label();
            this.btnCreatePatch = new System.Windows.Forms.Button();
            this.lblOutput = new System.Windows.Forms.Label();
            this.btnFtpServer = new System.Windows.Forms.Button();
            this.btnDeliver = new System.Windows.Forms.Button();
            this.btnVerify = new System.Windows.Forms.Button();
            this.btnOpenInExplorer = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();
            this.btnPickExe = new System.Windows.Forms.Button();
            this.menuStrip.SuspendLayout();
            this.grpVersions.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.projectMenuItem});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(724, 24);
            this.menuStrip.TabIndex = 0;
            this.menuStrip.Text = "menuStrip";
            // 
            // projectMenuItem
            // 
            this.projectMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createProjectMenuItem,
            this.openProjectMenuItem,
            this.projectSeparator1,
            this.recentProjectsMenuItem,
            this.projectSeparator2,
            this.exitMenuItem});
            this.projectMenuItem.Name = "projectMenuItem";
            this.projectMenuItem.Size = new System.Drawing.Size(59, 20);
            this.projectMenuItem.Text = "Проект";
            // 
            // createProjectMenuItem
            // 
            this.createProjectMenuItem.Name = "createProjectMenuItem";
            this.createProjectMenuItem.Size = new System.Drawing.Size(171, 22);
            this.createProjectMenuItem.Text = "Создать проект…";
            // 
            // openProjectMenuItem
            // 
            this.openProjectMenuItem.Name = "openProjectMenuItem";
            this.openProjectMenuItem.Size = new System.Drawing.Size(171, 22);
            this.openProjectMenuItem.Text = "Открыть проект…";
            // 
            // projectSeparator1
            // 
            this.projectSeparator1.Name = "projectSeparator1";
            this.projectSeparator1.Size = new System.Drawing.Size(168, 6);
            // 
            // recentProjectsMenuItem
            // 
            this.recentProjectsMenuItem.Name = "recentProjectsMenuItem";
            this.recentProjectsMenuItem.Size = new System.Drawing.Size(171, 22);
            this.recentProjectsMenuItem.Text = "Недавние";
            // 
            // projectSeparator2
            // 
            this.projectSeparator2.Name = "projectSeparator2";
            this.projectSeparator2.Size = new System.Drawing.Size(168, 6);
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(171, 22);
            this.exitMenuItem.Text = "Выход";
            // 
            // lblProjectTitle
            // 
            this.lblProjectTitle.AutoSize = true;
            this.lblProjectTitle.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.lblProjectTitle.Location = new System.Drawing.Point(12, 32);
            this.lblProjectTitle.Name = "lblProjectTitle";
            this.lblProjectTitle.Size = new System.Drawing.Size(137, 17);
            this.lblProjectTitle.TabIndex = 1;
            this.lblProjectTitle.Text = "Проект: (не открыт)";
            // 
            // grpVersions
            // 
            this.grpVersions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.grpVersions.Controls.Add(this.listViewVersions);
            this.grpVersions.Location = new System.Drawing.Point(12, 58);
            this.grpVersions.Name = "grpVersions";
            this.grpVersions.Size = new System.Drawing.Size(354, 360);
            this.grpVersions.TabIndex = 2;
            this.grpVersions.TabStop = false;
            this.grpVersions.Text = "Версии (из Versions/)";
            // 
            // listViewVersions
            // 
            this.listViewVersions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listViewVersions.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colVersion,
            this.colBuildDate,
            this.colDelivered});
            this.listViewVersions.FullRowSelect = true;
            this.listViewVersions.HideSelection = false;
            this.listViewVersions.Location = new System.Drawing.Point(10, 22);
            this.listViewVersions.Name = "listViewVersions";
            this.listViewVersions.Size = new System.Drawing.Size(338, 326);
            this.listViewVersions.TabIndex = 0;
            this.listViewVersions.UseCompatibleStateImageBehavior = false;
            this.listViewVersions.View = System.Windows.Forms.View.Details;
            // 
            // colVersion
            // 
            this.colVersion.Text = "Версия";
            this.colVersion.Width = 90;
            // 
            // colBuildDate
            // 
            this.colBuildDate.Text = "Дата сборки";
            this.colBuildDate.Width = 130;
            // 
            // colDelivered
            // 
            this.colDelivered.Text = "Доставлено";
            this.colDelivered.Width = 105;
            // 
            // lblSource
            // 
            this.lblSource.AutoSize = true;
            this.lblSource.Location = new System.Drawing.Point(372, 64);
            this.lblSource.Name = "lblSource";
            this.lblSource.Size = new System.Drawing.Size(91, 13);
            this.lblSource.TabIndex = 3;
            this.lblSource.Text = "Источник билда:";
            // 
            // txtSource
            // 
            this.txtSource.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSource.Location = new System.Drawing.Point(375, 84);
            this.txtSource.Name = "txtSource";
            this.txtSource.ReadOnly = true;
            this.txtSource.Size = new System.Drawing.Size(291, 20);
            this.txtSource.TabIndex = 4;
            // 
            // btnBrowseSource
            // 
            this.btnBrowseSource.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseSource.Location = new System.Drawing.Point(672, 82);
            this.btnBrowseSource.Name = "btnBrowseSource";
            this.btnBrowseSource.Size = new System.Drawing.Size(40, 23);
            this.btnBrowseSource.TabIndex = 5;
            this.btnBrowseSource.Text = "…";
            this.btnBrowseSource.UseVisualStyleBackColor = true;
            // 
            // lblMainExe
            // 
            this.lblMainExe.AutoSize = true;
            this.lblMainExe.Location = new System.Drawing.Point(372, 116);
            this.lblMainExe.Name = "lblMainExe";
            this.lblMainExe.Size = new System.Drawing.Size(83, 13);
            this.lblMainExe.TabIndex = 6;
            this.lblMainExe.Text = "Главный exe: —";
            // 
            // btnCreatePatch
            // 
            this.btnCreatePatch.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCreatePatch.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold);
            this.btnCreatePatch.Location = new System.Drawing.Point(375, 150);
            this.btnCreatePatch.Name = "btnCreatePatch";
            this.btnCreatePatch.Size = new System.Drawing.Size(337, 48);
            this.btnCreatePatch.TabIndex = 7;
            this.btnCreatePatch.Text = "Создать патч";
            this.btnCreatePatch.UseVisualStyleBackColor = true;
            // 
            // lblOutput
            // 
            this.lblOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblOutput.AutoSize = true;
            this.lblOutput.Location = new System.Drawing.Point(12, 436);
            this.lblOutput.Name = "lblOutput";
            this.lblOutput.Size = new System.Drawing.Size(56, 13);
            this.lblOutput.TabIndex = 8;
            this.lblOutput.Text = "Output/: —";
            //
            // btnFtpServer
            //
            this.btnFtpServer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFtpServer.Enabled = false;
            this.btnFtpServer.Location = new System.Drawing.Point(338, 430);
            this.btnFtpServer.Name = "btnFtpServer";
            this.btnFtpServer.Size = new System.Drawing.Size(118, 28);
            this.btnFtpServer.TabIndex = 14;
            this.btnFtpServer.Text = "FTP-сервер…";
            this.btnFtpServer.UseVisualStyleBackColor = true;
            //
            // btnDeliver
            //
            this.btnDeliver.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDeliver.Enabled = false;
            this.btnDeliver.Location = new System.Drawing.Point(462, 430);
            this.btnDeliver.Name = "btnDeliver";
            this.btnDeliver.Size = new System.Drawing.Size(130, 28);
            this.btnDeliver.TabIndex = 9;
            this.btnDeliver.Text = "Доставить патч";
            this.btnDeliver.UseVisualStyleBackColor = true;
            // 
            // btnVerify
            // 
            this.btnVerify.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnVerify.Enabled = false;
            this.btnVerify.Location = new System.Drawing.Point(598, 430);
            this.btnVerify.Name = "btnVerify";
            this.btnVerify.Size = new System.Drawing.Size(114, 28);
            this.btnVerify.TabIndex = 10;
            this.btnVerify.Text = "Проверить";
            this.btnVerify.UseVisualStyleBackColor = true;
            // 
            // btnOpenInExplorer
            // 
            this.btnOpenInExplorer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenInExplorer.Enabled = false;
            this.btnOpenInExplorer.Location = new System.Drawing.Point(436, 30);
            this.btnOpenInExplorer.Name = "btnOpenInExplorer";
            this.btnOpenInExplorer.Size = new System.Drawing.Size(160, 25);
            this.btnOpenInExplorer.TabIndex = 11;
            this.btnOpenInExplorer.Text = "Открыть в проводнике";
            this.btnOpenInExplorer.UseVisualStyleBackColor = true;
            // 
            // btnSettings
            // 
            this.btnSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettings.Enabled = false;
            this.btnSettings.Location = new System.Drawing.Point(602, 30);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(110, 25);
            this.btnSettings.TabIndex = 12;
            this.btnSettings.Text = "Настройки";
            this.btnSettings.UseVisualStyleBackColor = true;
            //
            // btnPickExe
            //
            this.btnPickExe.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPickExe.Enabled = false;
            this.btnPickExe.Location = new System.Drawing.Point(612, 111);
            this.btnPickExe.Name = "btnPickExe";
            this.btnPickExe.Size = new System.Drawing.Size(100, 23);
            this.btnPickExe.TabIndex = 13;
            this.btnPickExe.Text = "Выбрать exe…";
            this.btnPickExe.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(724, 470);
            this.Controls.Add(this.btnVerify);
            this.Controls.Add(this.btnDeliver);
            this.Controls.Add(this.btnFtpServer);
            this.Controls.Add(this.lblOutput);
            this.Controls.Add(this.btnCreatePatch);
            this.Controls.Add(this.btnPickExe);
            this.Controls.Add(this.lblMainExe);
            this.Controls.Add(this.btnBrowseSource);
            this.Controls.Add(this.txtSource);
            this.Controls.Add(this.lblSource);
            this.Controls.Add(this.grpVersions);
            this.Controls.Add(this.btnSettings);
            this.Controls.Add(this.btnOpenInExplorer);
            this.Controls.Add(this.lblProjectTitle);
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.MinimumSize = new System.Drawing.Size(620, 420);
            this.Name = "MainForm";
            this.Text = "UpdateManager";
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.grpVersions.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem projectMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createProjectMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openProjectMenuItem;
        private System.Windows.Forms.ToolStripSeparator projectSeparator1;
        private System.Windows.Forms.ToolStripMenuItem recentProjectsMenuItem;
        private System.Windows.Forms.ToolStripSeparator projectSeparator2;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.Label lblProjectTitle;
        private System.Windows.Forms.GroupBox grpVersions;
        private System.Windows.Forms.ListView listViewVersions;
        private System.Windows.Forms.ColumnHeader colVersion;
        private System.Windows.Forms.ColumnHeader colBuildDate;
        private System.Windows.Forms.ColumnHeader colDelivered;
        private System.Windows.Forms.Label lblSource;
        private System.Windows.Forms.TextBox txtSource;
        private System.Windows.Forms.Button btnBrowseSource;
        private System.Windows.Forms.Label lblMainExe;
        private System.Windows.Forms.Button btnCreatePatch;
        private System.Windows.Forms.Label lblOutput;
        private System.Windows.Forms.Button btnFtpServer;
        private System.Windows.Forms.Button btnDeliver;
        private System.Windows.Forms.Button btnVerify;
        private System.Windows.Forms.Button btnOpenInExplorer;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.Button btnPickExe;
    }
}
