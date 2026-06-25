using System;
using System.Windows.Forms;
using UpdateManager.Core;
using UpdateManager.Presenters;
using UpdateManager.Views;

namespace UpdateManager.Forms
{
    /// <summary>
    /// Главное окно. Реализует IMainView: пробрасывает действия пользователя в события
    /// и умеет отрисовать состояние. Вся логика — в презентере, форма "тупая".
    /// </summary>
    public partial class MainForm : Form, IMainView
    {
        // Держим презентер в поле, чтобы он не уехал в сборщик мусора.
        private readonly MainPresenter _presenter;

        public MainForm()
        {
            InitializeComponent();

            // Клики меню -> события интерфейса (логику дальше дёргает презентер).
            createProjectMenuItem.Click += (s, e) => CreateProjectRequested?.Invoke(this, EventArgs.Empty);
            openProjectMenuItem.Click += (s, e) => OpenProjectRequested?.Invoke(this, EventArgs.Empty);
            exitMenuItem.Click += (s, e) => Close();

            // Композиция: создаём презентер и отдаём ему себя как вью.
            _presenter = new MainPresenter(this, new ProjectService());
        }

        // --- IMainView: события ---

        public event EventHandler CreateProjectRequested;
        public event EventHandler OpenProjectRequested;

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
