using System;
using System.IO;
using UpdateManager.Core.Common;
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
        private readonly FtpConnectionStore _ftpStore;

        private UpdateProject _project; // текущий открытый проект (null = не открыт)

        private const string BuildSourceOverlapMessage =
            "Папка билда совпадает с папкой проекта или вложена в неё.\n" +
            "Сборка скопировала бы служебные файлы проекта (Versions/Output/…) внутрь себя — " +
            "рекурсивно и без смысла.\n" +
            "Выберите отдельную папку с билдом приложения.";

        public MainPresenter(IMainView view, ProjectService service, RecentProjectsStore recent,
            VersionDetector detector, FtpConnectionStore ftpStore)
        {
            _view = view;
            _service = service;
            _recent = recent;
            _detector = detector;
            _ftpStore = ftpStore;

            _view.CreateProjectRequested += OnCreateProject;
            _view.OpenProjectRequested += OnOpenProject;
            _view.OpenRecentRequested += OnOpenRecent;
            _view.BrowseBuildSourceRequested += OnBrowseBuildSource;
            _view.BrowseMainExecutableRequested += OnBrowseMainExecutable;
            _view.CreatePatchRequested += OnCreatePatch;
            _view.OpenInExplorerRequested += OnOpenInExplorer;
            _view.EditSettingsRequested += OnEditSettings;
            _view.DeliverPatchRequested += OnDeliverPatch;
            _view.VerifyRequested += OnVerify;
            _view.ConfigureFtpRequested += OnConfigureFtp;

            _view.RenderNoProject(); // стартовое состояние — проект не открыт
            RefreshRecent();
        }

        private void OnCreateProject(object sender, EventArgs e)
        {
            var folder = _view.BrowseForFolder("Выберите папку для нового проекта обновления");
            if (folder == null)
                return;

            // Папка уже содержит проект → создание перезапишет его настройки. Лучше открыть.
            if (_service.IsProjectFolder(folder))
            {
                if (!_view.Confirm(
                    "В этой папке уже есть проект обновления (" + ProjectService.EngineSettingsFile + ").\n" +
                    "Создание перезапишет все файлы в папке.\n\n" +
                    "Чтобы просто открыть его — отмените и выберите «Открыть проект».\n" +
                    "Всё равно создавать заново?"))
                    return;

                // Чистим папку под новый проект. Может упасть на занятом файле/правах — ловим, чтобы не крашить.
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (Exception ex)
                {
                    _view.ShowError("Не удалось очистить папку:\n" + ex.Message);
                    return;
                }
            }
            // Папка не пуста (но не проект) → движок подмешает Settings.xml/Versions/Output к чужим файлам.
            else if (_service.IsFolderNonEmpty(folder))
            {
                if (!_view.Confirm(
                    "Папка не пуста. В неё будут добавлены файлы проекта обновления " +
                    "(Settings.xml, Versions/Output/SelfPatcher/Other).\n" +
                    "Продолжить?"))
                    return;
            }

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
            // Папку проекта удалили/перенесли → запись мёртвая. Как с ярлыком Windows:
            // предупреждаем и предлагаем убрать её из списка.
            if (!_service.IsProjectFolder(path))
            {
                if (_view.Confirm(
                    "Проект не найден:\n" + path + "\n\n" +
                    "Папку удалили или перенесли. Убрать запись из списка недавних?"))
                {
                    _recent.Remove(path);
                    RefreshRecent();
                }
                return;
            }

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

            // Настройки менялись после сборки → в Output запечены старые (например, BaseDownloadURL).
            // Доставка выложит устаревший патч — предупреждаем (отпечаток сравнивается по значениям).
            if (_project.Meta.LastBuiltSettings.Length > 0 &&
                _project.Meta.LastBuiltSettings != _service.BuildSettingsFingerprint(_project.RootPath))
            {
                if (!_view.Confirm(
                    "Настройки проекта менялись после последней сборки патча.\n" +
                    "В Output запечены прежние настройки (например, BaseDownloadURL) — " +
                    "доставка выложит устаревший патч.\n\n" +
                    "Рекомендуется пересобрать патч.\n" +
                    "Всё равно доставить текущий Output?"))
                    return;
            }

            // Окно доставки: метод (преселект сохранённого) + путь.
            var config = _view.ConfigureDelivery(_project.Meta.Delivery);
            if (config == null)
                return; // отмена

            // Запоминаем выбор.
            _project.Meta.Delivery = config;
            _project.Meta.Save(_project.RootPath);

            // FTP идёт отдельной веткой — как фоновая операция с прогрессом.
            if (config.Method == DeliveryMethods.Ftp)
            {
                DeliverViaFtp(outputDir);
                return;
            }

            // Доставляем через выбранный обработчик (папка).
            try
            {
                DeliveryFactory.Create(config).Deliver(outputDir);
                MarkDelivered();
                _view.ShowInfo("Патч доставлен в:\n" + config.Path);
            }
            catch (Exception ex)
            {
                _view.ShowError("Доставка не удалась:\n" + ex.Message);
            }
        }

        // Заливка на FTP: реквизиты берём из профиля пользователя; если их нет — просим сейчас.
        // Путь = корень патчей (из реквизитов) + директория загрузки проекта (хвост BaseDownloadURL).
        private void DeliverViaFtp(string outputDir)
        {
            var uploadDir = GetUploadDirectory();

            var conn = _ftpStore.Load(_project.RootPath);
            WarnIfPasswordUndecryptable(conn);

            // Просим реквизиты, если их нет, они неполны или пароль не расшифровался (иначе залив
            // пойдёт с пустым паролем и упадёт невнятной ошибкой авторизации).
            if (conn == null || !conn.IsComplete() || conn.PasswordDecryptFailed)
            {
                conn = _view.ConfigureFtp(conn ?? new FtpConnection(), uploadDir);
                if (conn == null)
                    return; // отмена
                _ftpStore.Save(_project.RootPath, conn);
            }

            var remotePath = DownloadUrl.CombineRemote(conn.RemotePath, uploadDir);

            // Окно операции модальное: после его закрытия op.Succeeded уже валиден.
            var op = new FtpUploadOperation(conn, outputDir, remotePath);
            _view.ShowOperation(op);

            if (op.Succeeded)
            {
                MarkDelivered();
                _view.ShowInfo("Патч залит на FTP:\n" + remotePath);
            }
        }

        // Директория загрузки проекта = хвост BaseDownloadURL (общий и для URL, и для пути на FTP).
        private string GetUploadDirectory()
        {
            string server, directory;
            DownloadUrl.Split(_service.LoadSettings(_project.RootPath).BaseDownloadURL, out server, out directory);
            return directory;
        }

        // Отмечаем факт доставки — отсюда считается «Доставлено» по версиям.
        private void MarkDelivered()
        {
            _project.Meta.LastDeliveredAt = DateTime.Now;
            _project.Meta.Save(_project.RootPath);
            ShowProject();
        }

        private void OnConfigureFtp(object sender, EventArgs e)
        {
            if (_project == null)
                return;

            var current = _ftpStore.Load(_project.RootPath) ?? new FtpConnection();
            WarnIfPasswordUndecryptable(current);
            var updated = _view.ConfigureFtp(current, GetUploadDirectory());
            if (updated == null)
                return; // отмена

            _ftpStore.Save(_project.RootPath, updated);
            _view.ShowInfo("Реквизиты FTP сохранены.");
        }

        // DPAPI привязан к учётке Windows: на другой машине/учётке сохранённый пароль не расшифруется.
        // Не оставляем поле молча пустым — поясняем, почему пароль надо ввести заново.
        private void WarnIfPasswordUndecryptable(FtpConnection conn)
        {
            if (conn != null && conn.PasswordDecryptFailed)
                _view.ShowInfo(
                    "Сохранённый пароль FTP не удалось расшифровать.\n" +
                    "Вероятно, проект открыт на другой машине или под другой учётной записью Windows " +
                    "(пароль шифруется через DPAPI и привязан к учётке).\n" +
                    "Введите пароль заново.");
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

            // Папка билда не должна совпадать с проектом или быть вложенной в него (Versions/Output/…):
            // иначе сборка скопирует служебное дерево проекта внутрь самого себя — рекурсивно.
            if (FileUtils.PathsOverlap(folder, _project.RootPath))
            {
                _view.ShowError(BuildSourceOverlapMessage);
                return;
            }

            // Сменили папку — старый ручной exe в новом пути не нужен, возвращаемся к авто.
            if (!string.Equals(folder, _project.Meta.LastBuildSource, StringComparison.OrdinalIgnoreCase))
                _project.Meta.MainExecutable = "";

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

            // Источник мог быть сохранён в файле проекта раньше (или папки переехали) — проверяем снова.
            if (FileUtils.PathsOverlap(source, _project.RootPath))
            {
                _view.ShowError(BuildSourceOverlapMessage);
                return;
            }

            var settings = _service.LoadSettings(_project.RootPath);

            // Защита: BaseDownloadURL запекается в патч при сборке. Пустой — соберётся молча,
            // но клиент не сможет скачать файлы (битые ссылки). Блокируем заранее.
            if ((settings.BaseDownloadURL ?? "").Trim().Length == 0)
            {
                _view.ShowError("Не задан BaseDownloadURL.\n" +
                    "Он запекается в патч при сборке — без него клиент не скачает файлы.\n" +
                    "Укажите его в настройках проекта и соберите снова.");
                return;
            }

            // Защита: снят каждый тип патча — собирать нечего, патч бесполезен (клиент не обновится).
            if (!settings.CreateRepairPatch && !settings.CreateInstallerPatch && !settings.CreateIncrementalPatch)
            {
                _view.ShowError("Не выбран ни один тип патча (Repair / Installer / Incremental).\n" +
                    "Такой патч бесполезен — клиенту нечего применять.\n" +
                    "Включите хотя бы один тип в настройках проекта.");
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

            // Версия ниже максимальной собранной рвёт инкрементальную цепочку (движок идёт по возрастанию).
            // Равную перезаписать можно (ниже — отдельный вопрос про перезапись), меньшую — нет.
            string maxVersion;
            if (_service.IsBelowMaxVersion(_project.RootPath, version, out maxVersion))
            {
                _view.ShowError("Версия " + version + " ниже максимальной собранной (" + maxVersion + ").\n" +
                    "Движок строит инкрементальные патчи по возрастанию версий — более низкая версия " +
                    "сломает цепочку обновлений.\n" +
                    "Соберите версию не ниже " + maxVersion + " (ровно " + maxVersion + " — пересборка той же версии).");
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
            var builder = new PatchBuilder(_project.RootPath);
            _view.ShowOperation(builder);

            // Запоминаем отпечаток настроек, с которыми собран Output, — для проверки при доставке.
            if (builder.Succeeded)
            {
                _project.Meta.LastBuiltSettings = _service.BuildSettingsFingerprint(_project.RootPath);
                _project.Meta.Save(_project.RootPath);
            }

            // Обновляем проект (в Versions/ появилась новая версия) и показываем заново.
            _project = _service.Open(_project.RootPath);
            ShowProject();
        }

        private void OnBrowseMainExecutable(object sender, EventArgs e)
        {
            if (_project == null)
                return;

            var folder = _project.Meta.LastBuildSource;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                _view.ShowError("Сначала выберите папку билда.");
                return;
            }

            var fullPath = _view.BrowseForFile("Выберите главный exe", folder,
                "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*");
            if (fullPath == null)
                return;

            // Храним путь относительно папки билда — VersionDetector клеит buildFolder + это значение.
            _project.Meta.MainExecutable = MakeRelativeToBuild(folder, fullPath);
            _project.Meta.Save(_project.RootPath);

            // Переопределяем версию с выбранным главным exe.
            ApplyBuildSource(folder, persist: false);
        }

        private static string MakeRelativeToBuild(string buildFolder, string fullPath)
        {
            var baseWithSep = buildFolder.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(baseWithSep.Length);

            return Path.GetFileName(fullPath); // выбрали вне папки билда — берём только имя
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
