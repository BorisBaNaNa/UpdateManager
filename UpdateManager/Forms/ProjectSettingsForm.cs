using SimplePatchToolCore;
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
            Theming.ThemeManager.Register(this);

            // Списки форматов сжатия — из движкового enum (LZMA/GZIP/NONE).
            var formats = Enum.GetValues(typeof(CompressionFormat)).Cast<object>().ToArray();
            cmbCompRepair.Items.AddRange(formats);
            cmbCompInstaller.Items.AddRange(formats);
            cmbCompIncremental.Items.AddRange(formats);

            txtName.Text = settings.Name;
            txtBaseUrl.Text = settings.BaseDownloadURL;
            txtMaintUrl.Text = settings.MaintenanceCheckURL;
            chkSelfPatching.Checked = settings.IsSelfPatchingApp;
            chkRepair.Checked = settings.CreateRepairPatch;
            chkInstaller.Checked = settings.CreateInstallerPatch;
            chkIncremental.Checked = settings.CreateIncrementalPatch;
            chkAllIncremental.Checked = settings.CreateAllIncrementalPatches;
            txtIgnored.Lines = settings.IgnoredPaths.ToArray();

            cmbCompRepair.SelectedItem = settings.CompressionFormatRepairPatch;
            cmbCompInstaller.SelectedItem = settings.CompressionFormatInstallerPatch;
            cmbCompIncremental.SelectedItem = settings.CompressionFormatIncrementalPatch;
            numBinaryDiff.Value = Math.Max(numBinaryDiff.Minimum,
                Math.Min(numBinaryDiff.Maximum, settings.BinaryDiffQuality));
            chkDontPatchUnchanged.Checked = settings.DontCreatePatchFilesForUnchangedFiles;

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
                CreateAllIncrementalPatches = chkAllIncremental.Checked,
                IgnoredPaths = txtIgnored.Lines
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .ToList(),
                CompressionFormatRepairPatch = (CompressionFormat)cmbCompRepair.SelectedItem,
                CompressionFormatInstallerPatch = (CompressionFormat)cmbCompInstaller.SelectedItem,
                CompressionFormatIncrementalPatch = (CompressionFormat)cmbCompIncremental.SelectedItem,
                BinaryDiffQuality = (int)numBinaryDiff.Value,
                DontCreatePatchFilesForUnchangedFiles = chkDontPatchUnchanged.Checked
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
