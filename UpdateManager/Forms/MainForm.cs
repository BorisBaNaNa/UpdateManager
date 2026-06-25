using System;
using System.Collections.Generic;
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
        }

        // --- IMainView: события ---

        public event EventHandler CreateProjectRequested;
        public event EventHandler OpenProjectRequested;
        public event EventHandler<string> OpenRecentRequested;

        // --- IMainView: отрисовка ---

        public void RenderProject(UpdateProject project)
        {
            lblProjectTitle.Text = "Проект: " + project.Name;
            lblMainExe.Text = "Главный exe: " +
                (string.IsNullOrEmpty(project.Meta.MainExecutable) ? "—" : project.Meta.MainExecutable);
            txtSource.Text = project.Meta.LastBuildSource ?? "";

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

        public void ShowError(string message)
        {
            MessageBox.Show(this, message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
