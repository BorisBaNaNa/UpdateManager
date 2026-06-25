using System;
using System.Linq;
using System.Windows.Forms;
using UpdateManager.Core.Project;

namespace UpdateManager.Forms
{
    /// <summary>
    /// Окно редактирования основных настроек проекта (движковый Settings.xml через DTO).
    /// При «Сохранить» возвращает заполненный ProjectSettings в Result и DialogResult.OK.
    /// </summary>
    public partial class ProjectSettingsForm : Form
    {
        /// <summary>Итоговые настройки (заполняются по «Сохранить»).</summary>
        public ProjectSettings Result { get; private set; }

        public ProjectSettingsForm(ProjectSettings settings)
        {
            InitializeComponent();

            txtName.Text = settings.Name;
            txtBaseUrl.Text = settings.BaseDownloadURL;
            txtMaintUrl.Text = settings.MaintenanceCheckURL;
            chkSelfPatching.Checked = settings.IsSelfPatchingApp;
            chkRepair.Checked = settings.CreateRepairPatch;
            chkInstaller.Checked = settings.CreateInstallerPatch;
            chkIncremental.Checked = settings.CreateIncrementalPatch;
            txtIgnored.Lines = settings.IgnoredPaths.ToArray();

            btnOk.Click += OnOk;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        }

        private void OnOk(object sender, EventArgs e)
        {
            Result = new ProjectSettings
            {
                Name = txtName.Text.Trim(),
                BaseDownloadURL = txtBaseUrl.Text.Trim(),
                MaintenanceCheckURL = txtMaintUrl.Text.Trim(),
                IsSelfPatchingApp = chkSelfPatching.Checked,
                CreateRepairPatch = chkRepair.Checked,
                CreateInstallerPatch = chkInstaller.Checked,
                CreateIncrementalPatch = chkIncremental.Checked,
                IgnoredPaths = txtIgnored.Lines
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .ToList()
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
