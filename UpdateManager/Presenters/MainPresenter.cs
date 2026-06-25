using System;
using System.IO;
using UpdateManager.Core;
using UpdateManager.Views;

namespace UpdateManager.Presenters
{
    /// <summary>
    /// Логика главного окна. Связывает события вью с доменными сервисами (Core).
    /// Не зависит от WinForms — общается с UI только через IMainView.
    /// </summary>
    public class MainPresenter
    {
        private readonly IMainView _view;
        private readonly ProjectService _service;
        private readonly RecentProjectsStore _recent;

        private UpdateProject _project; // текущий открытый проект (null = не открыт)

        public MainPresenter(IMainView view, ProjectService service, RecentProjectsStore recent)
        {
            _view = view;
            _service = service;
            _recent = recent;

            _view.CreateProjectRequested += OnCreateProject;
            _view.OpenProjectRequested += OnOpenProject;
            _view.OpenRecentRequested += OnOpenRecent;

            _view.RenderNoProject(); // стартовое состояние — проект не открыт
            RefreshRecent();
        }

        private void OnCreateProject(object sender, EventArgs e)
        {
            var folder = _view.BrowseForFolder("Выберите папку для нового проекта обновления");
            if (folder == null)
                return;

            var suggestedName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
            var name = _view.PromptProjectName(suggestedName);
            if (name == null)
                return;

            try
            {
                _project = _service.Create(folder, name);
                OnProjectOpened(folder);
            }
            catch (Exception ex)
            {
                _view.ShowError("Не удалось создать проект:\n" + ex.Message);
            }
        }

        private void OnOpenProject(object sender, EventArgs e)
        {
            var folder = _view.BrowseForFolder("Выберите папку существующего проекта обновления");
            if (folder == null)
                return;

            OpenPath(folder);
        }

        private void OnOpenRecent(object sender, string path)
        {
            OpenPath(path);
        }

        // Открыть проект по известному пути (из диалога или из списка недавних).
        private void OpenPath(string folder)
        {
            try
            {
                _project = _service.Open(folder);
                OnProjectOpened(folder);
            }
            catch (Exception ex)
            {
                _view.ShowError("Не удалось открыть проект:\n" + ex.Message);
            }
        }

        // Общий хвост создания/открытия: показать проект и обновить список недавних.
        private void OnProjectOpened(string folder)
        {
            _view.RenderProject(_project);
            _recent.Add(folder);
            RefreshRecent();
        }

        private void RefreshRecent()
        {
            _view.RenderRecentProjects(_recent.Load());
        }
    }
}
