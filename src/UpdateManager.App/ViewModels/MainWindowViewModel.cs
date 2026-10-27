using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.App.Services;
using UpdateManager.Core.Common;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Project;
using UpdateManager.Core.Security;
using UpdateManager.Core.SelfUpdate;
using UpdateManager.Core.Versioning;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Логика главного окна (порт MainPresenter из 1.x). Связывает команды UI с доменными сервисами
/// (Core); с окнами общается только через <see cref="IDialogService"/>.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private const string BuildSourceOverlapMessage =
        "Папка билда совпадает с папкой проекта или вложена в неё.\n" +
        "Сборка скопировала бы служебные файлы проекта (Versions/Output/…) внутрь себя — " +
        "рекурсивно и без смысла.\n" +
        "Выберите отдельную папку с билдом приложения.";

    private readonly IDialogService _dialogs;
    private readonly ProjectService _service;
    private readonly RecentProjectsStore _recent;
    private readonly VersionDetector _detector;
    private readonly FtpConnectionStore _ftpStore;
    private readonly SelfUpdateConfig _selfUpdateConfig;
    private readonly ThemeService _theme;

    // Помощники подписи RSA — без состояния/конфига, создаём на месте (как PatchBuilder и пр.).
    private readonly RsaKeyManager _keys = new();
    private readonly PatchSigner _signer = new();

    private UpdateProject? _project; // текущий открытый проект (null = не открыт)

    public MainWindowViewModel(IDialogService dialogs, ProjectService service, RecentProjectsStore recent,
        VersionDetector detector, FtpConnectionStore ftpStore, SelfUpdateConfig selfUpdateConfig,
        ThemeService theme, SelfUpdateViewModel selfUpdate)
    {
        _dialogs = dialogs;
        _service = service;
        _recent = recent;
        _detector = detector;
        _ftpStore = ftpStore;
        _selfUpdateConfig = selfUpdateConfig;
        _theme = theme;
        SelfUpdate = selfUpdate;

        RenderNoProject(); // стартовое состояние — проект не открыт
        RefreshRecent();
    }

    public SelfUpdateViewModel SelfUpdate { get; }

    /// <summary>Окно просит закрыть приложение (меню «Выход»).</summary>
    public event EventHandler? ExitRequested;

    // --- состояние для отрисовки ---

    /// <summary>Каталог выпусков открытого проекта, от нового к старому.</summary>
    public ObservableCollection<ReleaseItem> Releases { get; } = new();

    /// <summary>Пункты меню «Недавние».</summary>
    public ObservableCollection<RecentProjectItem> RecentProjects { get; } = new();

    /// <summary>Недавние проекты на стартовом экране (проект не открыт).</summary>
    public ObservableCollection<RecentProjectCard> RecentCards { get; } = new();

    [ObservableProperty] private string _windowTitle = "UpdateManager";
    [ObservableProperty] private string _projectName = "";
    [ObservableProperty] private string _projectPath = "";
    [ObservableProperty] private string _mainExecutableText = "";
    [ObservableProperty] private string _buildSource = "";
    [ObservableProperty] private string _serverText = "";
    [ObservableProperty] private string _deliveryText = "";

    /// <summary>Самый свежий из живых недавних проектов — главный элемент стартового экрана.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeaturedRecent))]
    private RecentProjectCard? _featuredRecent;

    public bool HasFeaturedRecent => FeaturedRecent != null;

    [ObservableProperty] private ReleaseItem? _selectedRelease;

    // Следующий шаг выбранного выпуска: одна главная кнопка вместо ряда равноправных.
    [ObservableProperty] private string _nextStepText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _hasNextStep;
    private NextStepKind _nextStep;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseBuildSourceCommand), nameof(OpenInExplorerCommand),
        nameof(EditSettingsCommand), nameof(DeliverCommand), nameof(VerifyCommand),
        nameof(ConfigureFtpCommand), nameof(ConfigureDeliveryCommand), nameof(GenerateKeysCommand),
        nameof(RunStageCommand))]
    [NotifyPropertyChangedFor(nameof(IsStartScreen))]
    private bool _hasProject;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreatePatchCommand), nameof(PickMainExecutableCommand))]
    private bool _hasBuildSource;

    public bool IsStartScreen => !HasProject;
    public bool HasRecentCards => RecentCards.Count > 0;
    public bool HasReleases => Releases.Count > 0;

    public bool IsLightTheme => _theme.Mode == AppTheme.Light;
    public bool IsDarkTheme => _theme.Mode == AppTheme.Dark;
    public bool IsSystemTheme => _theme.Mode == AppTheme.System;

    // --- проект: создать / открыть / недавние ---

    [RelayCommand]
    private async Task CreateProject()
    {
        var folder = await _dialogs.PickFolderAsync("Выберите папку для нового проекта обновления");
        if (folder == null)
            return;

        // Папка уже содержит проект → создание перезапишет его настройки. Лучше открыть.
        if (_service.IsProjectFolder(folder))
        {
            if (!await _dialogs.ConfirmAsync(
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
                await _dialogs.ShowErrorAsync("Не удалось очистить папку:\n" + ex.Message);
                return;
            }
        }
        // Папка не пуста (но не проект) → движок подмешает Settings.xml/Versions/Output к чужим файлам.
        else if (_service.IsFolderNonEmpty(folder))
        {
            if (!await _dialogs.ConfirmAsync(
                    "Папка не пуста. В неё будут добавлены файлы проекта обновления " +
                    "(Settings.xml, Versions/Output/SelfPatcher/Other).\n" +
                    "Продолжить?"))
                return;
        }

        var suggestedName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
        var name = await _dialogs.PromptAsync("Новый проект", "Имя проекта:", suggestedName);
        if (name == null)
            return;

        try
        {
            _project = _service.Create(folder, name);
            OnProjectOpened(folder);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Не удалось создать проект:\n" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        var folder = await _dialogs.PickFolderAsync("Выберите папку существующего проекта обновления");
        if (folder == null)
            return;

        await OpenPath(folder);
    }

    [RelayCommand]
    private async Task OpenRecent(string path)
    {
        // Сначала даём меню закрыться: открытие проекта пересобирает RecentProjects, и нажатый пункт
        // исчезал из дерева раньше, чем меню успевало закрыться по цепочке родителей — оставалось открытым.
        await Task.Yield();

        // Папку проекта удалили/перенесли → запись мёртвая. Как с ярлыком Windows:
        // предупреждаем и предлагаем убрать её из списка.
        if (!_service.IsProjectFolder(path))
        {
            if (await _dialogs.ConfirmAsync(
                    "Проект не найден:\n" + path + "\n\n" +
                    "Папку удалили или перенесли. Убрать запись из списка недавних?"))
            {
                _recent.Remove(path);
                RefreshRecent();
            }
            return;
        }

        await OpenPath(path);
    }

    // Открыть проект по известному пути (из диалога или из списка недавних).
    private async Task OpenPath(string folder)
    {
        try
        {
            _project = _service.Open(folder);
            OnProjectOpened(folder);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Не удалось открыть проект:\n" + ex.Message);
        }
    }

    // Общий хвост создания/открытия: показать проект и обновить список недавних.
    private void OnProjectOpened(string folder)
    {
        _recent.Add(folder);
        RefreshRecent();
        ShowProject();
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void OpenInExplorer()
    {
        if (_project != null)
            _dialogs.OpenInExplorer(_project.RootPath);
    }

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void SetTheme(AppTheme mode)
    {
        _theme.SetMode(mode);
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsSystemTheme));
    }

    // --- доставка ---

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task Deliver()
    {
        if (_project == null)
            return;

        // Есть ли что доставлять?
        var outputDir = _service.GetOutputPath(_project.RootPath);
        if (!Directory.Exists(outputDir) || Directory.GetFileSystemEntries(outputDir).Length == 0)
        {
            await _dialogs.ShowErrorAsync("Папка Output пуста — нечего доставлять. Сначала соберите патч.");
            return;
        }

        // Настройки менялись после сборки → в Output запечены старые (например, BaseDownloadURL).
        // Доставка выложит устаревший патч — предупреждаем (отпечаток сравнивается по значениям).
        if (_project.Meta.LastBuiltSettings.Length > 0 &&
            _project.Meta.LastBuiltSettings != _service.BuildSettingsFingerprint(_project.RootPath))
        {
            if (!await _dialogs.ConfirmAsync(
                    "Настройки проекта менялись после последней сборки патча.\n" +
                    "В Output запечены прежние настройки (например, BaseDownloadURL) — " +
                    "доставка выложит устаревший патч.\n\n" +
                    "Рекомендуется пересобрать патч.\n" +
                    "Всё равно доставить текущий Output?"))
                return;
        }

        // Окно доставки: метод (преселект сохранённого) + путь.
        var config = await _dialogs.ConfigureDeliveryAsync(_project.Meta.Delivery, deliverNow: true);
        if (config == null)
            return; // отмена

        // Запоминаем выбор.
        _project.Meta.Delivery = config;
        _project.Meta.Save(_project.RootPath);

        // FTP идёт отдельной веткой — как фоновая операция с прогрессом.
        if (config.Method == DeliveryMethods.Ftp)
        {
            await DeliverViaFtp(outputDir);
            return;
        }

        // Доставляем через выбранный обработчик (папка).
        try
        {
            DeliveryFactory.Create(config).Deliver(outputDir);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Доставка не удалась:\n" + ex.Message);
            return;
        }

        MarkDelivered();
        await _dialogs.ShowInfoAsync("Патч доставлен в:\n" + config.Path);
    }

    // Заливка на FTP: реквизиты берём из профиля пользователя; если их нет — просим сейчас.
    // Путь = корень патчей (из реквизитов) + директория загрузки проекта (хвост BaseDownloadURL).
    private async Task DeliverViaFtp(string outputDir)
    {
        var uploadDir = GetUploadDirectory();

        var conn = _ftpStore.Load(_project!.RootPath);
        await WarnIfPasswordUndecryptable(conn);

        // Просим реквизиты, если их нет, они неполны или пароль не расшифровался (иначе залив
        // пойдёт с пустым паролем и упадёт невнятной ошибкой авторизации).
        if (conn == null || !conn.IsComplete() || conn.PasswordDecryptFailed)
        {
            conn = await _dialogs.ConfigureFtpAsync(conn ?? new FtpConnection(), uploadDir);
            if (conn == null)
                return; // отмена
            _ftpStore.Save(_project.RootPath, conn);
        }

        var remotePath = DownloadUrl.CombineRemote(conn.RemotePath, uploadDir);

        // Окно операции модальное: после его закрытия op.Succeeded уже валиден.
        var op = new FtpUploadOperation(conn, outputDir, remotePath);
        await _dialogs.ShowOperationAsync(op);

        if (op.Succeeded)
        {
            MarkDelivered();
            await _dialogs.ShowInfoAsync("Патч залит на FTP:\n" + remotePath);
        }
    }

    // Директория загрузки проекта = хвост BaseDownloadURL (общий и для URL, и для пути на FTP).
    private string GetUploadDirectory()
    {
        DownloadUrl.Split(_service.LoadSettings(_project!.RootPath).BaseDownloadURL, out _, out var directory);
        return directory;
    }

    // Главная кнопка: выполняет следующий шаг выбранного выпуска.
    [RelayCommand]
    private async Task NextStep()
    {
        switch (_nextStep)
        {
            case NextStepKind.PickBuild:
                await BrowseBuildSourceCommand.ExecuteAsync(null);
                break;
            case NextStepKind.PickExecutable:
                await PickMainExecutableCommand.ExecuteAsync(null);
                break;
            case NextStepKind.CreatePatch:
                await CreatePatchCommand.ExecuteAsync(null);
                break;
            case NextStepKind.Deliver:
                await DeliverCommand.ExecuteAsync(null);
                break;
            case NextStepKind.Verify:
                await VerifyCommand.ExecuteAsync(null);
                break;
        }
    }

    // Нажатие на блок этапа последнего выпуска: выполнить (или повторить) этот шаг.
    [RelayCommand(CanExecute = nameof(CanRunStage))]
    private async Task RunStage(StageItem? stage)
    {
        switch (stage?.Kind)
        {
            case StageKind.Build:
                await BrowseBuildSourceCommand.ExecuteAsync(null);
                break;
            case StageKind.Patch:
                await CreatePatchCommand.ExecuteAsync(null);
                break;
            case StageKind.Delivery:
                await DeliverCommand.ExecuteAsync(null);
                break;
            case StageKind.Verify:
                await VerifyCommand.ExecuteAsync(null);
                break;
        }
    }

    private bool CanRunStage(StageItem? stage) => HasProject && stage is { IsActionable: true };

    // Отмечаем факт доставки за самой новой собранной версией: Output всегда содержит патч до неё.
    private void MarkDelivered()
    {
        var now = DateTime.Now;
        _project!.Meta.LastDeliveredAt = now;
        var version = _service.GetMaxVersion(_project.RootPath);
        if (version != null)
            _project.Meta.DeliveredVersions[version] = now;
        _project.Meta.Save(_project.RootPath);
        ShowProject();
    }

    // «Настроить…» в строке доставки: метод и путь без самой доставки; для FTP — сразу реквизиты.
    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task ConfigureDelivery()
    {
        if (_project == null)
            return;

        var config = await _dialogs.ConfigureDeliveryAsync(_project.Meta.Delivery, deliverNow: false);
        if (config == null)
            return; // отмена

        _project.Meta.Delivery = config;
        _project.Meta.Save(_project.RootPath);

        if (config.Method == DeliveryMethods.Ftp)
        {
            var conn = _ftpStore.Load(_project.RootPath);
            if (conn == null || !conn.IsComplete() || conn.PasswordDecryptFailed)
            {
                await WarnIfPasswordUndecryptable(conn);
                conn = await _dialogs.ConfigureFtpAsync(conn ?? new FtpConnection(), GetUploadDirectory());
                if (conn != null)
                    _ftpStore.Save(_project.RootPath, conn);
            }
        }
        ShowProject();
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task ConfigureFtp()
    {
        if (_project == null)
            return;

        var current = _ftpStore.Load(_project.RootPath) ?? new FtpConnection();
        await WarnIfPasswordUndecryptable(current);
        var updated = await _dialogs.ConfigureFtpAsync(current, GetUploadDirectory());
        if (updated == null)
            return; // отмена

        _ftpStore.Save(_project.RootPath, updated);
        await _dialogs.ShowInfoAsync("Реквизиты FTP сохранены.");
    }

    // DPAPI привязан к учётке Windows: на другой машине/учётке сохранённый пароль не расшифруется.
    // Не оставляем поле молча пустым — поясняем, почему пароль надо ввести заново.
    private async Task WarnIfPasswordUndecryptable(FtpConnection? conn)
    {
        if (conn != null && conn.PasswordDecryptFailed)
            await _dialogs.ShowInfoAsync(
                "Сохранённый пароль FTP не удалось расшифровать.\n" +
                "Вероятно, проект открыт на другой машине или под другой учётной записью Windows " +
                "(пароль шифруется через DPAPI и привязан к учётке).\n" +
                "Введите пароль заново.");
    }

    // --- RSA-подпись патча ---

    // Сгенерировать пару ключей в папке ОТКРЫТОГО проекта обновления и (по возможности)
    // залить приватный ключ в отдельную закрытую папку на FTP для коллег.
    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task GenerateKeys()
    {
        if (_project == null)
            return;

        var root = _project.RootPath;

        // Перегенерация делает НЕДЕЙСТВИТЕЛЬНЫМИ всех клиентов с прежним публичным ключом.
        if (_keys.HasPublicKey(root) || _keys.HasPrivateKey(root))
        {
            if (!await _dialogs.ConfirmAsync(
                    "В проекте уже есть RSA-ключи.\n" +
                    "Перегенерация создаст НОВУЮ пару и сделает НЕДЕЙСТВИТЕЛЬНЫМИ всех клиентов, " +
                    "у которых зашит прежний публичный ключ (их обновления перестанут проходить проверку).\n\n" +
                    "Точно перегенерировать ключи?"))
                return;
        }

        try
        {
            _keys.Generate(root);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Не удалось сгенерировать ключи:\n" + ex.Message);
            return;
        }

        // Заливаем приватный ключ коллегам (отдельная закрытая папка); заметку покажем в итоге.
        var uploadNote = await TryUploadPrivateKey(root);

        await _dialogs.ShowInfoAsync(
            "RSA-ключи сгенерированы.\n\n" +
            "Публичный ключ — передать разработчику клиента (для UseVersionInfoVerifier):\n" +
            _keys.PublicKeyPath(root) + "\n\n" +
            "Приватный ключ — СЕКРЕТ (не коммитить, не класть в папку патча):\n" +
            _keys.PrivateKeyPath(root) + "\n\n" +
            uploadNote);
    }

    // Залить приватный ключ в отдельную защищённую папку на FTP. Возвращает заметку для итога.
    private async Task<string> TryUploadPrivateKey(string projectRoot)
    {
        var conn = _ftpStore.Load(projectRoot);
        await WarnIfPasswordUndecryptable(conn);

        // Нет реквизитов/пароля — просим ввести (иначе заливка не пойдёт).
        if (conn == null || !conn.IsComplete() || conn.PasswordDecryptFailed)
        {
            conn = await _dialogs.ConfigureFtpAsync(conn ?? new FtpConnection(), GetUploadDirectory());
            if (conn == null)
                return "Приватный ключ на FTP НЕ залит (реквизиты не введены). " +
                       "Он сохранён локально — задайте FTP и повторите либо залейте вручную.";
            _ftpStore.Save(projectRoot, conn);
        }

        var keyRemote = (conn.PrivateKeyRemotePath ?? "").Trim();
        if (keyRemote.Length == 0)
            return "Приватный ключ на FTP НЕ залит: не задана «Папка для ключа» в настройках " +
                   "FTP-сервера. Укажите ОТДЕЛЬНУЮ закрытую папку (не путь патча) и повторите.";

        // БЕЗОПАСНОСТЬ: папка ключа не должна совпадать/пересекаться с публичной папкой патча —
        // иначе приватный ключ окажется там, откуда его качают клиенты.
        var patchRemote = DownloadUrl.CombineRemote(conn.RemotePath, GetUploadDirectory());
        if (DownloadUrl.RemotePathsOverlap(keyRemote, patchRemote))
            return "❌ Приватный ключ НЕ залит: «Папка для ключа» (" + keyRemote + ") совпадает " +
                   "или пересекается с папкой патча (" + patchRemote + "). Приватный ключ нельзя " +
                   "класть туда, откуда качают клиенты. Укажите ОТДЕЛЬНУЮ закрытую папку.";

        try
        {
            var uploadConn = conn;
            await Task.Run(() => new FtpKeyUploader(uploadConn).Upload(
                _keys.PrivateKeyPath(projectRoot), keyRemote, RsaKeyManager.PrivateKeyFileName));
            return "Приватный ключ залит в закрытую папку на FTP:\n" +
                   keyRemote.TrimEnd('/') + "/" + RsaKeyManager.PrivateKeyFileName;
        }
        catch (Exception ex)
        {
            return "Приватный ключ на FTP НЕ залит: " + FtpErrorText.Describe(ex) +
                   "\nОн сохранён локально — залейте вручную.";
        }
    }

    // Подписать собранный Output приватным ключом. false + показанная ошибка = провал.
    private async Task<bool> SignBuiltPatch()
    {
        var root = _project!.RootPath;
        if (!_keys.HasPrivateKey(root))
        {
            await _dialogs.ShowErrorAsync(
                "Включена подпись патча, но приватный ключ (" + RsaKeyManager.PrivateKeyFileName +
                ") не найден в папке проекта.\n" +
                "Сгенерируйте ключи (меню «Безопасность») или снимите галочку подписи в настройках.");
            return false;
        }

        try
        {
            var output = _service.GetOutputPath(root);
            var signed = _signer.Sign(output, _keys.ReadPrivateKey(root));
            if (signed.Count == 0)
            {
                await _dialogs.ShowErrorAsync(
                    "Подпись не выполнена: в Output не найден VersionInfo.info.\n" +
                    "Проверьте, что сборка патча прошла успешно.");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Не удалось подписать патч:\n" + ex.Message);
            return false;
        }
    }

    // --- проверка ---

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task Verify()
    {
        if (_project == null)
            return;

        // versionInfoURL берём из BaseDownloadURL (всё лежит в одном месте).
        var baseUrl = (_service.LoadSettings(_project.RootPath).BaseDownloadURL ?? "").Trim();
        if (baseUrl.Length == 0)
        {
            await _dialogs.ShowErrorAsync("Не задан BaseDownloadURL. Укажите его в настройках проекта.");
            return;
        }

        var versionInfoUrl = _service.BuildVersionInfoUrl(baseUrl);

        // Движок реально качает патч во временную папку; после — удаляем её.
        var tempDir = Path.Combine(Path.GetTempPath(), "UpdateManagerVerify_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var verifier = new PatchVerifier(tempDir, versionInfoUrl);
            await _dialogs.ShowOperationAsync(verifier);
            MarkVerified(verifier);
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

    // Итог проверки — за самой новой собранной версией: на сервере лежит патч до неё.
    private void MarkVerified(IEngineOperation verifier)
    {
        var version = _service.GetMaxVersion(_project!.RootPath);
        if (version == null)
            return;

        var details = verifier.Succeeded ? "" : verifier.ResultDetails;
        if (!verifier.Succeeded && string.IsNullOrWhiteSpace(details))
            details = "проверка прервана";
        _project.Meta.VerifiedVersions[version] = new VerificationResult(DateTime.Now, verifier.Succeeded, details);
        _project.Meta.Save(_project.RootPath);
        ShowProject();
    }

    // --- настройки ---

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task EditSettings()
    {
        if (_project == null)
            return;

        var current = _service.LoadSettings(_project.RootPath);
        var updated = await _dialogs.EditSettingsAsync(current);
        if (updated == null)
            return; // отмена

        try
        {
            _service.SaveSettings(_project.RootPath, updated);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Не удалось сохранить настройки:\n" + ex.Message);
            return;
        }

        // Имя проекта могло измениться — перечитываем и показываем заново.
        _project = _service.Open(_project.RootPath);
        ShowProject();
    }

    // --- билд и сборка патча ---

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task BrowseBuildSource()
    {
        if (_project == null)
            return;

        var folder = await _dialogs.PickFolderAsync("Выберите папку с билдом приложения");
        if (folder == null)
            return;

        // Папка билда не должна совпадать с проектом или быть вложенной в него (Versions/Output/…):
        // иначе сборка скопирует служебное дерево проекта внутрь самого себя — рекурсивно.
        if (FileUtils.PathsOverlap(folder, _project.RootPath))
        {
            await _dialogs.ShowErrorAsync(BuildSourceOverlapMessage);
            return;
        }

        // Сменили папку — старый ручной exe в новом пути не нужен, возвращаемся к авто.
        if (!string.Equals(folder, _project.Meta.LastBuildSource, StringComparison.OrdinalIgnoreCase))
            _project.Meta.MainExecutable = "";

        ApplyBuildSource(folder, persist: true);
        await OfferSelfPreset(folder);
    }

    [RelayCommand(CanExecute = nameof(HasBuildSource))]
    private async Task CreatePatch()
    {
        if (_project == null)
            return;

        var source = _project.Meta.LastBuildSource;
        if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
        {
            await _dialogs.ShowErrorAsync("Папка билда не выбрана или не существует.");
            return;
        }

        // Источник мог быть сохранён в файле проекта раньше (или папки переехали) — проверяем снова.
        if (FileUtils.PathsOverlap(source, _project.RootPath))
        {
            await _dialogs.ShowErrorAsync(BuildSourceOverlapMessage);
            return;
        }

        var settings = _service.LoadSettings(_project.RootPath);

        // Защита: BaseDownloadURL запекается в патч при сборке. Пустой — соберётся молча,
        // но клиент не сможет скачать файлы (битые ссылки). Блокируем заранее.
        if ((settings.BaseDownloadURL ?? "").Trim().Length == 0)
        {
            await _dialogs.ShowErrorAsync("Не задан BaseDownloadURL.\n" +
                "Он запекается в патч при сборке — без него клиент не скачает файлы.\n" +
                "Укажите его в настройках проекта и соберите снова.");
            return;
        }

        // Защита: билд самого UpdateManager без self-patching-режима — клиент попытается
        // перезаписать собственный запущенный exe, и самообновление сломается.
        if (!settings.IsSelfPatchingApp && IsSelfBuild(source) &&
            !await _dialogs.ConfirmAsync("Это билд самого UpdateManager, но IsSelfPatchingApp выключен.\n" +
                "Без него клиент попытается перезаписать запущенный exe — самообновление не сработает.\n" +
                "Включите его в настройках проекта.\n\n" +
                "Всё равно собрать?"))
            return;

        // Защита: снят каждый тип патча — собирать нечего, патч бесполезен (клиент не обновится).
        if (!settings.CreateRepairPatch && !settings.CreateInstallerPatch && !settings.CreateIncrementalPatch)
        {
            await _dialogs.ShowErrorAsync("Не выбран ни один тип патча (Repair / Installer / Incremental).\n" +
                "Такой патч бесполезен — клиенту нечего применять.\n" +
                "Включите хотя бы один тип в настройках проекта.");
            return;
        }

        // Защита: installer-патч пакует всё в один TAR, а вшитый в движок SharpZipLib читает
        // имена записей не в UTF-8 — файлы с не-ASCII (напр. кириллицей) именами рвут ПРИМЕНЕНИЕ
        // installer-патча на клиенте ("Illegal characters in path"). Repair/incremental целы.
        if (settings.CreateInstallerPatch)
        {
            var nonAscii = FileUtils.FindNonAsciiNames(source, 6);
            if (nonAscii.Count > 0)
            {
                var sample = "";
                for (int i = 0; i < nonAscii.Count && i < 5; i++)
                    sample += "  • " + nonAscii[i] + "\n";
                if (nonAscii.Count > 5)
                    sample += "  • …и другие\n";

                if (!await _dialogs.ConfirmAsync(
                        "В билде есть файлы или папки с не-ASCII именами (например, кириллицей):\n" +
                        sample + "\n" +
                        "Installer-патч пакует всё в один TAR-архив, и при установке на клиенте такие " +
                        "имена вызовут ошибку \"Illegal characters in path\" — installer-патч НЕ " +
                        "применится (repair и incremental работают как обычно).\n\n" +
                        "Переименуйте файлы в латиницу или отключите Installer-патч в настройках.\n\n" +
                        "Всё равно собрать?"))
                    return;
            }
        }

        // Подтверждение версии (всегда, даже если определили сами).
        var detected = _detector.Detect(source, _project.Name, _project.Meta.MainExecutable);
        var prompt = detected.Version != null
            ? "Обнаружена версия " + detected.Version + ".\nПодтвердите или измените версию собираемого патча:"
            : "Версию определить не удалось.\nВведите версию собираемого патча:";
        var version = await _dialogs.PromptAsync("Версия патча", prompt, detected.Version ?? "");
        if (version == null)
            return; // отмена

        // Версия должна быть принимаемой движком (только числа через точку).
        if (!VersionRules.IsValid(version))
        {
            await _dialogs.ShowErrorAsync("Версия \"" + version + "\" недопустима.\n" +
                "Движок принимает только числа через точку, например 1.0.0.0\n" +
                "(буквы и суффиксы вроде 1.2-b1 или v1.0 нельзя).");
            return;
        }

        // Версия ниже максимальной собранной рвёт инкрементальную цепочку (движок идёт по возрастанию).
        // Равную перезаписать можно (ниже — отдельный вопрос про перезапись), меньшую — нет.
        if (_service.IsBelowMaxVersion(_project.RootPath, version, out var maxVersion))
        {
            await _dialogs.ShowErrorAsync("Версия " + version + " ниже максимальной собранной (" + maxVersion + ").\n" +
                "Движок строит инкрементальные патчи по возрастанию версий — более низкая версия " +
                "сломает цепочку обновлений.\n" +
                "Соберите версию не ниже " + maxVersion + " (ровно " + maxVersion + " — пересборка той же версии).");
            return;
        }

        // Версия уже есть? Спросить про перезапись.
        if (_service.VersionExists(_project.RootPath, version) &&
            !await _dialogs.ConfirmAsync("Версия " + version + " уже есть в Versions/. Перезаписать её?"))
            return;

        // Копируем билд в Versions/<версия>.
        try
        {
            _service.StageBuild(_project.RootPath, source, version);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Не удалось подготовить версию:\n" + ex.Message);
            return;
        }

        // Сборка патча в модальном окне с логом.
        var builder = new PatchBuilder(_project.RootPath);
        await _dialogs.ShowOperationAsync(builder);

        // Запоминаем отпечаток настроек, с которыми собран Output, — для проверки при доставке.
        if (builder.Succeeded)
        {
            // Подпись RSA (если включена): ПОСЛЕ сборки (BaseDownloadURL уже запечён в манифест)
            // и ДО доставки. При провале отпечаток не сохраняем — Output не считается готовым.
            if (settings.SignPatch && !await SignBuiltPatch())
            {
                _project = _service.Open(_project.RootPath);
                ShowProject();
                return;
            }

            _project.Meta.LastBuiltSettings = _service.BuildSettingsFingerprint(_project.RootPath);
            _project.Meta.PatchedVersions[version] = DateTime.Now;
            _project.Meta.Save(_project.RootPath);
        }

        // Обновляем проект (в Versions/ появилась новая версия) и показываем заново.
        _project = _service.Open(_project.RootPath);
        ShowProject();
    }

    [RelayCommand(CanExecute = nameof(HasBuildSource))]
    private async Task PickMainExecutable()
    {
        if (_project == null)
            return;

        var folder = _project.Meta.LastBuildSource;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            await _dialogs.ShowErrorAsync("Сначала выберите папку билда.");
            return;
        }

        var fullPath = await _dialogs.PickExecutableAsync("Выберите главный exe", folder);
        if (fullPath == null)
            return;

        // Храним путь относительно папки билда — VersionDetector клеит buildFolder + это значение.
        _project.Meta.MainExecutable = MakeRelativeToBuild(folder, fullPath);
        _project.Meta.Save(_project.RootPath);

        // Переопределяем версию с выбранным главным exe.
        ApplyBuildSource(folder, persist: false);
        await OfferSelfPreset(folder);
    }

    // Билд — это сам UpdateManager, а проект ещё не настроен на самообновление? Предложить пресет.
    // Только с согласия: тихо настройки не меняем.
    private async Task OfferSelfPreset(string buildFolder)
    {
        if (!IsSelfBuild(buildFolder))
            return;

        var settings = _service.LoadSettings(_project!.RootPath);
        if (SelfProjectPreset.IsApplied(settings))
            return;

        var baseUrl = _selfUpdateConfig.BaseDownloadUrl.Length > 0 ? _selfUpdateConfig.BaseDownloadUrl : "(не меняется)";
        if (!await _dialogs.ConfirmAsync("Похоже, это билд самого UpdateManager.\n" +
                "Применить настройки самообновления?\n\n" +
                "• IsSelfPatchingApp — включить\n" +
                "• BaseDownloadURL — " + baseUrl + "\n" +
                "• IgnoredPaths — добавить *.pdb, *.vshost.*, логи движка"))
            return;

        SelfProjectPreset.Apply(settings, _selfUpdateConfig);
        try
        {
            _service.SaveSettings(_project.RootPath, settings);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Не удалось сохранить настройки:\n" + ex.Message);
        }
    }

    private bool IsSelfBuild(string buildFolder)
    {
        var build = _detector.Detect(buildFolder, _project!.Name, _project.Meta.MainExecutable);
        return build.ExecutableName != null &&
            SelfIdentity.IsSelf(Path.Combine(buildFolder, build.ExecutableName));
    }

    private static string MakeRelativeToBuild(string buildFolder, string fullPath)
    {
        var baseWithSep = buildFolder.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase))
            return fullPath.Substring(baseWithSep.Length);

        return Path.GetFileName(fullPath); // выбрали вне папки билда — берём только имя
    }

    // Запомнить папку-источник (опц. сохранить в файл проекта) и показать проект заново.
    private void ApplyBuildSource(string folder, bool persist)
    {
        _project!.Meta.LastBuildSource = folder;
        if (persist)
            _project.Meta.Save(_project.RootPath);

        ShowProject();
    }

    private void RefreshRecent()
    {
        RecentProjects.Clear();
        RecentCards.Clear();
        var paths = _recent.Load();
        if (paths.Count == 0)
            RecentProjects.Add(RecentProjectItem.Empty);

        foreach (var path in paths)
        {
            RecentProjects.Add(new RecentProjectItem(path, OpenRecentCommand));
            RecentCards.Add(LoadRecentCard(path));
        }
        OnPropertyChanged(nameof(HasRecentCards));
        FeaturedRecent = RecentCards.FirstOrDefault(c => !c.IsMissing);
    }

    // Карточка для стартового экрана: имя и последний выпуск. Битый/пропавший проект — не повод падать.
    private RecentProjectCard LoadRecentCard(string path)
    {
        if (!_service.IsProjectFolder(path))
            return new RecentProjectCard(path, null, null, OpenRecentCommand);

        try
        {
            var project = _service.Open(path);
            var releases = ReleaseCatalog.Build(project, null);
            return new RecentProjectCard(path, project.Name, releases.FirstOrDefault(), OpenRecentCommand);
        }
        catch
        {
            return new RecentProjectCard(path, null, null, OpenRecentCommand);
        }
    }

    // --- отрисовка ---

    // Показать текущий проект: шапка, сведения, каталог выпусков (с новым билдом из папки-источника).
    private void ShowProject()
    {
        var project = _project!;
        var settings = _service.LoadSettings(project.RootPath);

        ProjectName = project.Name;
        ProjectPath = project.RootPath;
        WindowTitle = "UpdateManager — " + project.Name;
        HasProject = true;

        var baseUrl = (settings.BaseDownloadURL ?? "").Trim();
        ServerText = baseUrl.Length > 0 ? baseUrl : "не задан — укажите в настройках проекта";
        DeliveryText = DescribeDelivery(project);

        string? newVersion = null;
        var newDetail = "";
        var source = project.Meta.LastBuildSource ?? "";
        HasBuildSource = source.Length > 0;
        if (HasBuildSource)
        {
            BuildSource = source;
            var build = _detector.Detect(source, project.Name, project.Meta.MainExecutable);
            MainExecutableText = build.ExecutableName == null
                ? "не найден — выберите вручную"
                : build.ExecutableName + (build.Version != null ? " · " + build.Version : "");

            // В папке билда версия новее собранных → это следующий выпуск, ещё без патча.
            if (build.Version != null && VersionRules.IsValid(build.Version) &&
                !_service.VersionExists(project.RootPath, build.Version) &&
                !_service.IsBelowMaxVersion(project.RootPath, build.Version, out _))
            {
                newVersion = build.Version;
                newDetail = Path.GetFileName(source.TrimEnd('\\', '/')) + "\\ · " + (build.ExecutableName ?? "");
            }
        }
        else
        {
            BuildSource = "не выбрана";
            MainExecutableText = "—";
        }

        // Выбран архивный выпуск — остаёмся на нём; выбран последний — следуем за новым последним.
        var keep = SelectedRelease is { IsLatest: false } ? SelectedRelease.Version : null;
        Releases.Clear();
        foreach (var release in ReleaseCatalog.Build(project, newVersion, newDetail))
            Releases.Add(release);
        OnPropertyChanged(nameof(HasReleases));

        SelectedRelease = Releases.FirstOrDefault(r => r.Version == keep) ?? Releases.FirstOrDefault();
        UpdateNextStep();
    }

    private string DescribeDelivery(UpdateProject project)
    {
        var delivery = project.Meta.Delivery;
        if (delivery.Method != DeliveryMethods.Ftp)
            return delivery.Path.Length > 0 ? "папка · " + delivery.Path : "не настроена";

        var conn = _ftpStore.Load(project.RootPath);
        if (conn == null || conn.Host.Length == 0)
            return "FTP · сервер не задан";

        return "FTP · " + conn.Host + DownloadUrl.CombineRemote(conn.RemotePath, GetUploadDirectory());
    }

    partial void OnSelectedReleaseChanged(ReleaseItem? value) => UpdateNextStep();

    // Главная кнопка и строка состояния для выбранного выпуска.
    private void UpdateNextStep()
    {
        var release = SelectedRelease;
        _nextStep = NextStepKind.None;
        StatusText = "";

        if (!HasProject)
        {
        }
        else if (release == null)
        {
            _nextStep = HasBuildSource ? NextStepKind.PickExecutable : NextStepKind.PickBuild;
            StatusText = HasBuildSource
                ? "Версия билда не определилась — укажите главный exe."
                : "Версий пока нет. Начните с папки билда приложения.";
        }
        else if (!release.IsLatest)
        {
            StatusText = "Архивный выпуск: клиенты получают патч до более новой версии.";
        }
        else
        {
            var next = release.Platforms[0].NextStage;
            if (next == null)
            {
                StatusText = "Выпуск собран, доставлен и проверен.";
            }
            else
            {
                (_nextStep, StatusText) = next.Kind switch
                {
                    StageKind.Build => (NextStepKind.PickBuild, "Выберите папку билда."),
                    StageKind.Patch => (NextStepKind.CreatePatch, "Билд готов — соберите патч."),
                    StageKind.Delivery => (NextStepKind.Deliver, "Патч собран — доставьте его на сервер."),
                    _ => (NextStepKind.Verify, next.IsFailed
                        ? "Проверка не прошла: " + string.Join(" ", next.Reason.Split('\n',
                              StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).TrimEnd('.') + ".\n" +
                          "Проверьте адрес сервера в настройках и что патч доставлен, затем проверьте снова."
                        : "Патч на сервере — проверьте, что клиент его скачает.")
                };
            }
        }

        NextStepText = _nextStep switch
        {
            NextStepKind.PickBuild => "Выбрать папку билда…",
            NextStepKind.PickExecutable => "Выбрать главный exe…",
            NextStepKind.CreatePatch => "Создать патч",
            NextStepKind.Deliver => "Доставить",
            NextStepKind.Verify => release?.Platforms[0].NextStage?.IsFailed == true ? "Проверить снова" : "Проверить",
            _ => ""
        };
        HasNextStep = _nextStep != NextStepKind.None;
    }

    private void RenderNoProject()
    {
        WindowTitle = "UpdateManager";
        ProjectName = "";
        ProjectPath = "";
        MainExecutableText = "";
        BuildSource = "";
        ServerText = "";
        DeliveryText = "";
        Releases.Clear();
        OnPropertyChanged(nameof(HasReleases));
        SelectedRelease = null;
        HasProject = false;
        HasBuildSource = false;
        UpdateNextStep();
    }
}

/// <summary>Что делает главная кнопка.</summary>
public enum NextStepKind { None, PickBuild, PickExecutable, CreatePatch, Deliver, Verify }

/// <summary>Пункт меню «Недавние». Пустой список — один неактивный пункт «(пусто)».</summary>
public sealed class RecentProjectItem
{
    public static readonly RecentProjectItem Empty = new("(пусто)", null);

    public RecentProjectItem(string path, IRelayCommand<string>? open)
    {
        Path = path;
        OpenCommand = open;
    }

    public string Path { get; }
    public IRelayCommand<string>? OpenCommand { get; }
    public bool IsEnabled => OpenCommand != null;
}
