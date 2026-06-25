using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using UpdateManager.Core;
using UpdateManager.Views;

namespace UpdateManager.Forms
{
    /// <summary>
    /// Главное окно. Реализует IMainView: пробрасывает действия пользователя в события
    /// и умеет отрисовать состояние. Вся логика — в презентере, форма "тупая".
    /// </summary>
    public partial class MainForm : Form, IMainView
    {
        public MainForm()
        {
            InitializeComponent();

            // Форма знает только про свои контролы: клики меню -> события интерфейса.
            // Кто и как на них реагирует (презентер) — форму не касается.
            createProjectMenuItem.Click += (s, e) => CreateProjectRequested?.Invoke(this, EventArgs.Empty);
            openProjectMenuItem.Click += (s, e) => OpenProjectRequested?.Invoke(this, EventArgs.Empty);
            exitMenuItem.Click += (s, e) => Close();
            btnBrowseSource.Click += (s, e) => BrowseBuildSourceRequested?.Invoke(this, EventArgs.Empty);
            btnCreatePatch.Click += (s, e) => CreatePatchRequested?.Invoke(this, EventArgs.Empty);
            btnOpenInExplorer.Click += (s, e) => OpenInExplorerRequested?.Invoke(this, EventArgs.Empty);
            btnSettings.Click += (s, e) => EditSettingsRequested?.Invoke(this, EventArgs.Empty);
            btnDeliver.Click += (s, e) => DeliverPatchRequested?.Invoke(this, EventArgs.Empty);
            btnVerify.Click += (s, e) => VerifyRequested?.Invoke(this, EventArgs.Empty);
        }

        // --- IMainView: события ---

        public event EventHandler CreateProjectRequested;
        public event EventHandler OpenProjectRequested;
        public event EventHandler<string> OpenRecentRequested;
        public event EventHandler BrowseBuildSourceRequested;
        public event EventHandler CreatePatchRequested;
        public event EventHandler OpenInExplorerRequested;
        public event EventHandler EditSettingsRequested;
        public event EventHandler DeliverPatchRequested;
        public event EventHandler VerifyRequested;

        // --- IMainView: отрисовка ---

        public void RenderProject(UpdateProject project)
        {
            lblProjectTitle.Text = "Проект: " + project.Name;
            lblMainExe.Text = "Главный exe: " +
                (string.IsNullOrEmpty(project.Meta.MainExecutable) ? "—" : project.Meta.MainExecutable);
            txtSource.Text = project.Meta.LastBuildSource ?? "";

            // Проект открыт — действия с проектом доступны.
            btnBrowseSource.Enabled = true;
            btnOpenInExplorer.Enabled = true;
            btnSettings.Enabled = true;
            btnDeliver.Enabled = true;
            btnVerify.Enabled = true;
            btnCreatePatch.Enabled = !string.IsNullOrEmpty(project.Meta.LastBuildSource);

            listViewVersions.BeginUpdate();
            listViewVersions.Items.Clear();
            foreach (var v in project.Versions)
            {
                var item = new ListViewItem(v.Version);
                item.SubItems.Add(v.BuildDate.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(v.Delivered ? "Да" : "—");
                listViewVersions.Items.Add(item);
            }
            listViewVersions.EndUpdate();
        }

        public void RenderNoProject()
        {
            lblProjectTitle.Text = "Проект: (не открыт)";
            lblMainExe.Text = "Главный exe: —";
            txtSource.Text = "";
            listViewVersions.Items.Clear();

            // Проект не открыт — действия с проектом недоступны.
            btnBrowseSource.Enabled = false;
            btnOpenInExplorer.Enabled = false;
            btnSettings.Enabled = false;
            btnDeliver.Enabled = false;
            btnVerify.Enabled = false;
            btnCreatePatch.Enabled = false;
        }

        public void RenderBuildSource(string sourcePath, string mainExecutable, string version)
        {
            txtSource.Text = sourcePath ?? "";

            if (mainExecutable == null)
                lblMainExe.Text = "Главный exe: не найден";
            else
                lblMainExe.Text = "Главный exe: " + mainExecutable +
                    (version != null ? " (" + version + ")" : "");

            btnCreatePatch.Enabled = !string.IsNullOrEmpty(sourcePath);
        }

        public void RenderRecentProjects(IReadOnlyList<string> projectPaths)
        {
            recentProjectsMenuItem.DropDownItems.Clear();

            if (projectPaths.Count == 0)
            {
                recentProjectsMenuItem.DropDownItems.Add(new ToolStripMenuItem("(пусто)") { Enabled = false });
                return;
            }

            foreach (var path in projectPaths)
            {
                var item = new ToolStripMenuItem(path);
                item.Click += (s, e) => OpenRecentRequested?.Invoke(this, path);
                recentProjectsMenuItem.DropDownItems.Add(item);
            }
        }

        // --- IMainView: диалоги ---

        public string BrowseForFolder(string description)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = true;
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        public string PromptProjectName(string defaultName)
        {
            // InputBox из Microsoft.VisualBasic — простой ввод строки без отдельной формы.
            var name = Microsoft.VisualBasic.Interaction.InputBox("Имя проекта:", "Новый проект", defaultName).Trim();
            return name.Length > 0 ? name : null; // пусто/Отмена -> null
        }

        public string ConfirmVersion(string detectedVersion)
        {
            var prompt = detectedVersion != null
                ? "Обнаружена версия " + detectedVersion + ".\nПодтвердите или измените версию собираемого патча:"
                : "Версию определить не удалось.\nВведите версию собираемого патча:";

            var result = Microsoft.VisualBasic.Interaction.InputBox(prompt, "Версия патча", detectedVersion ?? "").Trim();
            return result.Length > 0 ? result : null; // пусто/Отмена -> null
        }

        public bool Confirm(string message)
        {
            return MessageBox.Show(this, message, "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public void ShowError(string message)
        {
            MessageBox.Show(this, message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void ShowInfo(string message)
        {
            MessageBox.Show(this, message, "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void ShowOperation(IEngineOperation operation)
        {
            using (var dialog = new OperationProgressForm(operation))
                dialog.ShowDialog(this);
        }

        public ProjectSettings EditSettings(ProjectSettings current)
        {
            using (var dialog = new ProjectSettingsForm(current))
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Result : null;
        }

        public DeliveryConfig ConfigureDelivery(DeliveryConfig current)
        {
            using (var dialog = new DeliveryForm(current))
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Result : null;
        }

        public void OpenInExplorer(string path)
        {
            Process.Start("explorer.exe", path);
        }
    }
}
