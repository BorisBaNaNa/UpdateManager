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

        private UpdateProject _project; // текущий открытый проект (null = не открыт)

        public MainPresenter(IMainView view, ProjectService service)
        {
            _view = view;
            _service = service;

            _view.CreateProjectRequested += OnCreateProject;
            _view.OpenProjectRequested += OnOpenProject;

            _view.RenderNoProject(); // стартовое состояние — проект не открыт
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
                _view.RenderProject(_project);
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

            try
            {
                _project = _service.Open(folder);
                _view.RenderProject(_project);
            }
            catch (Exception ex)
            {
                _view.ShowError("Не удалось открыть проект:\n" + ex.Message);
            }
        }
    }
}
