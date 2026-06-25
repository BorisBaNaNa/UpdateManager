using System;
using System.IO;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Project;
using UpdateManager.Core.Versioning;
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
            _view.DeliverPatchRequested += OnDeliverPatch;
            _view.VerifyRequested += OnVerify;

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
            _recent.Add(folder);
            RefreshRecent();
            ShowProject();
        }

        private void OnOpenInExplorer(object sender, EventArgs e)
        {
            if (_project != null)
                _view.OpenInExplorer(_project.RootPath);
        }

        private void OnDeliverPatch(object sender, EventArgs e)
        {
            if (_project == null)
                return;

            // Есть ли что доставлять?
            var outputDir = _service.GetOutputPath(_project.RootPath);
            if (!Directory.Exists(outputDir) || Directory.GetFileSystemEntries(outputDir).Length == 0)
            {
                _view.ShowError("Папка Output пуста — нечего доставлять. Сначала соберите патч.");
                return;
            }

            // Окно доставки: метод (преселект сохранённого) + путь.
            var config = _view.ConfigureDelivery(_project.Meta.Delivery);
            if (config == null)
                return; // отмена

            // Запоминаем выбор.
            _project.Meta.Delivery = config;
            _project.Meta.Save(_project.RootPath);

            // Доставляем через выбранный обработчик.
            try
            {
                DeliveryFactory.Create(config).Deliver(outputDir);

                // Отмечаем факт доставки — отсюда считается «Доставлено» по версиям.
                _project.Meta.LastDeliveredAt = DateTime.Now;
                _project.Meta.Save(_project.RootPath);
                ShowProject();

                _view.ShowInfo("Патч доставлен в:\n" + config.Path);
            }
            catch (Exception ex)
            {
                _view.ShowError("Доставка не удалась:\n" + ex.Message);
            }
        }

        private void OnVerify(object sender, EventArgs e)
        {
            if (_project == null)
                return;

            // versionInfoURL берём из BaseDownloadURL (всё лежит в одном месте).
            var baseUrl = (_service.LoadSettings(_project.RootPath).BaseDownloadURL ?? "").Trim();
            if (baseUrl.Length == 0)
            {
                _view.ShowError("Не задан BaseDownloadURL. Укажите его в настройках проекта.");
                return;
            }

            var versionInfoUrl = _service.BuildVersionInfoUrl(baseUrl);

            // Движок реально качает патч во временную папку; после — удаляем её.
            var tempDir = Path.Combine(Path.GetTempPath(), "UpdateManagerVerify_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                _view.ShowOperation(new PatchVerifier(tempDir, versionInfoUrl));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // не критично, если временную папку не удалить
                }
            }
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
            ShowProject();
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

            // Защита: BaseDownloadURL запекается в патч при сборке. Пустой — соберётся молча,
            // но клиент не сможет скачать файлы (битые ссылки). Блокируем заранее.
            if ((_service.LoadSettings(_project.RootPath).BaseDownloadURL ?? "").Trim().Length == 0)
            {
                _view.ShowError("Не задан BaseDownloadURL.\n" +
                    "Он запекается в патч при сборке — без него клиент не скачает файлы.\n" +
                    "Укажите его в настройках проекта и соберите снова.");
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
            _view.ShowOperation(new PatchBuilder(_project.RootPath));

            // Обновляем проект (в Versions/ появилась новая версия) и показываем заново.
            _project = _service.Open(_project.RootPath);
            ShowProject();
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

        // Показать текущий проект: отрисовать + подхватить запомненный источник билда (с версией).
        private void ShowProject()
        {
            _view.RenderProject(_project);
            if (!string.IsNullOrEmpty(_project.Meta.LastBuildSource))
                ApplyBuildSource(_project.Meta.LastBuildSource, persist: false);
        }
    }
}
