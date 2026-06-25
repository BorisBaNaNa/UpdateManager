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
        private readonly VersionDetector _detector;

        private UpdateProject _project; // текущий открытый проект (null = не открыт)

        public MainPresenter(IMainView view, ProjectService service, RecentProjectsStore recent, VersionDetector detector)
        {
            _view = view;
            _service = service;
            _recent = recent;
            _detector = detector;

            _view.CreateProjectRequested += OnCreateProject;
            _view.OpenProjectRequested += OnOpenProject;
            _view.OpenRecentRequested += OnOpenRecent;
            _view.BrowseBuildSourceRequested += OnBrowseBuildSource;
            _view.CreatePatchRequested += OnCreatePatch;
            _view.OpenInExplorerRequested += OnOpenInExplorer;
            _view.EditSettingsRequested += OnEditSettings;

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

            // Если источник билда уже запомнен — показать его и определить версию (без повторной записи).
            if (!string.IsNullOrEmpty(_project.Meta.LastBuildSource))
                ApplyBuildSource(_project.Meta.LastBuildSource, persist: false);
        }

        private void OnOpenInExplorer(object sender, EventArgs e)
        {
            if (_project != null)
                _view.OpenInExplorer(_project.RootPath);
        }

        private void OnEditSettings(object sender, EventArgs e)
        {
            if (_project == null)
                return;

            var current = _service.LoadSettings(_project.RootPath);
            var updated = _view.EditSettings(current);
            if (updated == null)
                return; // отмена

            try
            {
                _service.SaveSettings(_project.RootPath, updated);
            }
            catch (Exception ex)
            {
                _view.ShowError("Не удалось сохранить настройки:\n" + ex.Message);
                return;
            }

            // Имя проекта могло измениться — перечитываем и показываем заново.
            _project = _service.Open(_project.RootPath);
            _view.RenderProject(_project);
            if (!string.IsNullOrEmpty(_project.Meta.LastBuildSource))
                ApplyBuildSource(_project.Meta.LastBuildSource, persist: false);
        }

        private void OnBrowseBuildSource(object sender, EventArgs e)
        {
            if (_project == null)
                return;

            var folder = _view.BrowseForFolder("Выберите папку с билдом приложения");
            if (folder == null)
                return;

            ApplyBuildSource(folder, persist: true);
        }

        private void OnCreatePatch(object sender, EventArgs e)
        {
            if (_project == null)
                return;

            var source = _project.Meta.LastBuildSource;
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
            {
                _view.ShowError("Папка билда не выбрана или не существует.");
                return;
            }

            // C — подтверждение версии (всегда, даже если определили сами).
            var detected = _detector.Detect(source, _project.Name, _project.Meta.MainExecutable);
            var version = _view.ConfirmVersion(detected.Version);
            if (version == null)
                return; // отмена

            // Версия должна быть принимаемой движком (только числа через точку).
            if (!VersionRules.IsValid(version))
            {
                _view.ShowError("Версия \"" + version + "\" недопустима.\n" +
                    "Движок принимает только числа через точку, например 1.0.0.0\n" +
                    "(буквы и суффиксы вроде 1.2-b1 или v1.0 нельзя).");
                return;
            }

            // Версия уже есть? Спросить про перезапись.
            if (_service.VersionExists(_project.RootPath, version) &&
                !_view.Confirm("Версия " + version + " уже есть в Versions/. Перезаписать её?"))
                return;

            // D — копируем билд в Versions/<версия>.
            try
            {
                _service.StageBuild(_project.RootPath, source, version);
            }
            catch (Exception ex)
            {
                _view.ShowError("Не удалось подготовить версию:\n" + ex.Message);
                return;
            }

            // E — сборка патча в модальном окне с логом.
            _view.ShowPatchProgress(new PatchBuilder(_project.RootPath));

            // Обновляем проект (в Versions/ появилась новая версия) и показываем заново.
            _project = _service.Open(_project.RootPath);
            _view.RenderProject(_project);
            if (!string.IsNullOrEmpty(_project.Meta.LastBuildSource))
                ApplyBuildSource(_project.Meta.LastBuildSource, persist: false);
        }

        // Запомнить папку-источник (опц. сохранить в файл проекта), определить версию и показать.
        private void ApplyBuildSource(string folder, bool persist)
        {
            _project.Meta.LastBuildSource = folder;
            if (persist)
                _project.Meta.Save(_project.RootPath);

            var build = _detector.Detect(folder, _project.Name, _project.Meta.MainExecutable);
            _view.RenderBuildSource(folder, build.ExecutableName, build.Version);
        }

        private void RefreshRecent()
        {
            _view.RenderRecentProjects(_recent.Load());
        }
    }
}
