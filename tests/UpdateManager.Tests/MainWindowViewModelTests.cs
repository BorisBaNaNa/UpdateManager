using Avalonia.Headless;
using UpdateManager.App.Services;
using UpdateManager.App.ViewModels;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Project;
using UpdateManager.Core.Security;
using UpdateManager.Core.SelfUpdate;
using UpdateManager.Core.Versioning;

namespace UpdateManager.Tests;

/// <summary>
/// Логика главного окна с фейковыми диалогами и настоящим Core: защиты чек-листа
/// (они блокируют сборку ДО копирования билда) и сквозной сценарий через VM.
/// </summary>
public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("UpdateManagerTests_").FullName;
    private readonly FakeDialogService _dialogs = new();
    private readonly ProjectService _service = new();
    private readonly RecentProjectsStore _recent;
    private readonly MainWindowViewModel _vm;

    private string ProjectDir => Path.Combine(_root, "project");
    private string BuildDir => Path.Combine(_root, "build");

    public MainWindowViewModelTests()
    {
        _recent = new RecentProjectsStore(Path.Combine(_root, "recent.xml"));
        var config = new SelfUpdateConfig("", "");
        _vm = new MainWindowViewModel(_dialogs, _service, _recent, new VersionDetector(),
            new FtpConnectionStore(Path.Combine(_root, "ftp.xml")), config,
            new ThemeService(Path.Combine(_root, "theme.txt")), NewSelfUpdate(config));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* временная папка */ }
    }

    // Проект через VM («Создать проект» → папка → имя) + папка билда.
    private async Task CreateProjectWithBuild(Action<ProjectSettings>? configure = null)
    {
        _dialogs.Folders.Enqueue(ProjectDir);
        _dialogs.Prompts.Enqueue("TestApp");
        await _vm.CreateProjectCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Errors);

        var settings = _service.LoadSettings(ProjectDir);
        settings.BaseDownloadURL = new Uri(_service.GetOutputPath(ProjectDir)).AbsoluteUri;
        settings.CreateRepairPatch = true;
        configure?.Invoke(settings);
        _service.SaveSettings(ProjectDir, settings);

        Directory.CreateDirectory(BuildDir);
        File.WriteAllText(Path.Combine(BuildDir, "app.txt"), "v1");
        _dialogs.Folders.Enqueue(BuildDir);
        await _vm.BrowseBuildSourceCommand.ExecuteAsync(null);
    }

    private bool VersionStaged(string version) => _service.VersionExists(ProjectDir, version);

    [Fact]
    public async Task StartsWithoutProject()
    {
        Assert.False(_vm.HasProject);
        Assert.True(_vm.IsStartScreen);
        Assert.False(_vm.CreatePatchCommand.CanExecute(null));
        Assert.Equal("UpdateManager", _vm.WindowTitle);
        Assert.Single(_vm.RecentProjects);
        Assert.False(_vm.RecentProjects[0].IsEnabled);
        Assert.Empty(_vm.RecentCards);
        await Task.CompletedTask;
    }

    // Стадия выпуска по типу (полоса Windows: Билд, Патч, Доставка, Проверка).
    private SelfUpdateViewModel NewSelfUpdate(SelfUpdateConfig config) =>
        new(_dialogs, config, new ReleaseNotesService(Path.Combine(_root, "whatsnew.txt"), () => Array.Empty<ReleaseNote>()));

    private StageItem Stage(string version, StageKind kind) =>
        _vm.Releases.Single(r => r.Version == version).Platforms[0].Stages.Single(s => s.Kind == kind);

    [Fact]
    public async Task CreateProject_OpensAndRemembersIt()
    {
        await CreateProjectWithBuild();

        Assert.True(_vm.HasProject);
        Assert.True(_vm.HasBuildSource);
        Assert.Equal("TestApp", _vm.ProjectName);
        Assert.Equal(BuildDir, _vm.BuildSource);
        Assert.Equal(ProjectDir, _recent.Load()[0]);
        Assert.Equal(ProjectDir, _vm.RecentProjects[0].Path);
    }

    [Fact]
    public async Task CreatePatch_BuildsAndDeliversToFolder()
    {
        await CreateProjectWithBuild();
        _dialogs.Prompts.Enqueue("1.0.0");

        await _vm.CreatePatchCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Errors);
        Assert.True(_dialogs.Operations.Single().Succeeded);
        Assert.Equal("1.0.0", _vm.Releases.Single().Version);
        Assert.Equal(StageState.Done, Stage("1.0.0", StageKind.Patch).State);
        Assert.Equal("Доставить", _vm.NextStepText);
        Assert.NotEmpty(ProjectMeta.Load(ProjectDir).LastBuiltSettings);

        var target = Path.Combine(_root, "server");
        _dialogs.DeliveryAnswer = _ => new DeliveryConfig { Method = DeliveryMethods.Folder, Path = target };
        await _vm.DeliverCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Errors);
        Assert.True(File.Exists(Path.Combine(target, "VersionInfo.info")));
        Assert.Equal(StageState.Done, Stage("1.0.0", StageKind.Delivery).State);
        Assert.Equal(target, ProjectMeta.Load(ProjectDir).Delivery.Path);
        Assert.Equal("Проверить", _vm.NextStepText);
    }

    [Fact]
    public async Task Verify_RecordsResultAndCompletesRelease()
    {
        // BaseDownloadURL указывает на Output (file:///) — проверка качает патч прямо оттуда.
        await CreateProjectWithBuild();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        _dialogs.DeliveryAnswer = _ => new DeliveryConfig { Method = DeliveryMethods.Folder, Path = Path.Combine(_root, "server") };
        await _vm.DeliverCommand.ExecuteAsync(null);

        await _vm.NextStepCommand.ExecuteAsync(null); // «Проверить»

        Assert.True(ProjectMeta.Load(ProjectDir).VerifiedVersions["1.0.0"].Succeeded);
        Assert.Equal(StageState.Done, Stage("1.0.0", StageKind.Verify).State);
        Assert.False(_vm.HasNextStep);
    }

    [Fact]
    public async Task Verify_FailureIsRecordedWithReason()
    {
        await CreateProjectWithBuild();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);

        // Сервер «переехал»: по новому адресу патча нет.
        var settings = _service.LoadSettings(ProjectDir);
        settings.BaseDownloadURL = new Uri(Path.Combine(_root, "nowhere")).AbsoluteUri + "/";
        _service.SaveSettings(ProjectDir, settings);
        await _vm.VerifyCommand.ExecuteAsync(null);

        var result = ProjectMeta.Load(ProjectDir).VerifiedVersions["1.0.0"];
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Details);
        Assert.Equal(StageState.Failed, Stage("1.0.0", StageKind.Verify).State);
    }

    [Fact]
    public async Task NewBuildInSource_IsNextReleaseWithCreatePatchStep()
    {
        await CreateProjectWithBuild();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);

        // В папке билда появилась версия новее собранной (version holder + главный exe).
        File.WriteAllText(Path.Combine(BuildDir, "TestApp.exe"), "");
        File.WriteAllText(Path.Combine(BuildDir, "TestApp_vers.sptv"), "1.0.1");
        _dialogs.Folders.Enqueue(BuildDir);
        await _vm.BrowseBuildSourceCommand.ExecuteAsync(null);

        var latest = _vm.Releases[0];
        Assert.True(latest.IsNew);
        Assert.Equal("1.0.1", latest.Version);
        Assert.Equal(2, latest.Number);
        Assert.Same(latest, _vm.SelectedRelease);
        Assert.Equal("Создать патч", _vm.NextStepText);

        // Старый выпуск — архивный: следующего шага у него нет.
        _vm.SelectedRelease = _vm.Releases[1];
        Assert.False(_vm.HasNextStep);
    }

    [Fact]
    public async Task CreatePatch_BlockedWithoutBaseDownloadUrl()
    {
        await CreateProjectWithBuild(s => s.BaseDownloadURL = "");
        _dialogs.Prompts.Enqueue("1.0.0");

        await _vm.CreatePatchCommand.ExecuteAsync(null);

        Assert.Contains("Не задан BaseDownloadURL", _dialogs.Errors.Single());
        Assert.False(VersionStaged("1.0.0"));
    }

    [Fact]
    public async Task CreatePatch_BlockedWithoutPatchTypes()
    {
        await CreateProjectWithBuild(s =>
        {
            s.CreateRepairPatch = false;
            s.CreateInstallerPatch = false;
            s.CreateIncrementalPatch = false;
        });

        await _vm.CreatePatchCommand.ExecuteAsync(null);

        Assert.Contains("ни один тип патча", _dialogs.Errors.Single());
    }

    [Theory]
    [InlineData("1.2-b1")]
    [InlineData("v1.0")]
    public async Task CreatePatch_RejectsInvalidVersion(string version)
    {
        await CreateProjectWithBuild();
        _dialogs.Prompts.Enqueue(version);

        await _vm.CreatePatchCommand.ExecuteAsync(null);

        Assert.Contains("недопустима", _dialogs.Errors.Single());
        Assert.False(VersionStaged(version));
    }

    [Fact]
    public async Task CreatePatch_RejectsVersionBelowMax()
    {
        await CreateProjectWithBuild();
        _service.StageBuild(ProjectDir, BuildDir, "2.0.0");
        _dialogs.Prompts.Enqueue("1.5.0");

        await _vm.CreatePatchCommand.ExecuteAsync(null);

        Assert.Contains("ниже максимальной собранной (2.0.0)", _dialogs.Errors.Single());
        Assert.False(VersionStaged("1.5.0"));
    }

    [Fact]
    public async Task CreatePatch_AsksBeforeInstallerWithNonAsciiNames()
    {
        await CreateProjectWithBuild(s => s.CreateInstallerPatch = true);
        File.WriteAllText(Path.Combine(BuildDir, "справка.txt"), "");
        _dialogs.ConfirmAnswer = m => !m.Contains("не-ASCII");

        await _vm.CreatePatchCommand.ExecuteAsync(null);

        Assert.Contains(_dialogs.Confirms, m => m.Contains("справка.txt"));
        Assert.Empty(_dialogs.Operations);
    }

    [Fact]
    public async Task CreatePatch_BuildSourceInsideProjectIsRejected()
    {
        await CreateProjectWithBuild();
        _dialogs.Folders.Enqueue(Path.Combine(ProjectDir, "Other"));

        await _vm.BrowseBuildSourceCommand.ExecuteAsync(null);

        Assert.Contains("вложена", _dialogs.Errors.Single());
        Assert.Equal(BuildDir, _vm.BuildSource);
    }

    [Fact]
    public async Task Deliver_WarnsWhenSettingsChangedAfterBuild()
    {
        await CreateProjectWithBuild();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);

        var settings = _service.LoadSettings(ProjectDir);
        settings.BaseDownloadURL = "http://other.example.com/";
        _service.SaveSettings(ProjectDir, settings);

        _dialogs.ConfirmAnswer = _ => false;
        _dialogs.DeliveryAnswer = _ => throw new InvalidOperationException("окно доставки не должно открываться");
        await _vm.DeliverCommand.ExecuteAsync(null);

        Assert.Contains(_dialogs.Confirms, m => m.Contains("менялись после последней сборки"));
    }

    [Fact]
    public async Task BrowseBuildSource_OffersSelfPresetForOwnBuildInFreshProject()
    {
        // Свежий проект «UpdateManager» (движок уже включил IsSelfPatchingApp) + папка с exe самого
        // UpdateManager (apphost App копируется в bin тестов, Product/Company — из Directory.Build.props).
        var config = new SelfUpdateConfig("http://updates.example.com/UpdateManager/VersionInfo.info", "");
        var vm = new MainWindowViewModel(_dialogs, _service, _recent, new VersionDetector(),
            new FtpConnectionStore(Path.Combine(_root, "ftp.xml")), config,
            new ThemeService(Path.Combine(_root, "theme.txt")), NewSelfUpdate(config));

        _dialogs.Folders.Enqueue(ProjectDir);
        _dialogs.Prompts.Enqueue("UpdateManager");
        await vm.CreateProjectCommand.ExecuteAsync(null);
        Assert.True(_service.LoadSettings(ProjectDir).IsSelfPatchingApp);

        Directory.CreateDirectory(BuildDir);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "UpdateManager.exe"), Path.Combine(BuildDir, "UpdateManager.exe"));
        _dialogs.Folders.Enqueue(BuildDir);
        await vm.BrowseBuildSourceCommand.ExecuteAsync(null);

        Assert.Contains(_dialogs.Confirms, m => m.Contains("билд самого UpdateManager"));
        var settings = _service.LoadSettings(ProjectDir);
        Assert.Equal("http://updates.example.com/UpdateManager/", settings.BaseDownloadURL);
        Assert.Contains("*.pdb", settings.IgnoredPaths);

        // Пресет применён — повторный выбор той же папки больше не спрашивает.
        _dialogs.Confirms.Clear();
        _dialogs.Folders.Enqueue(BuildDir);
        await vm.BrowseBuildSourceCommand.ExecuteAsync(null);
        Assert.DoesNotContain(_dialogs.Confirms, m => m.Contains("билд самого UpdateManager"));
    }

    [Fact]
    public async Task Deliver_RecordsTimePerVersion()
    {
        await CreateProjectWithBuild();
        var target = Path.Combine(_root, "server");
        _dialogs.DeliveryAnswer = _ => new DeliveryConfig { Method = DeliveryMethods.Folder, Path = target };

        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        await _vm.DeliverCommand.ExecuteAsync(null);
        var first = ProjectMeta.Load(ProjectDir).DeliveredVersions["1.0.0"];

        Thread.Sleep(20);
        File.WriteAllText(Path.Combine(BuildDir, "app.txt"), "v2");
        _dialogs.Prompts.Enqueue("1.0.1");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        Assert.Equal(StageState.Pending, Stage("1.0.1", StageKind.Delivery).State); // собрана, но не доставлена

        await _vm.DeliverCommand.ExecuteAsync(null);

        var delivered = ProjectMeta.Load(ProjectDir).DeliveredVersions;
        Assert.Equal(first, delivered["1.0.0"]); // доставка 1.0.1 не переписала дату 1.0.0
        Assert.True(delivered["1.0.1"] > first);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task RunStage_RebuildsAndRedeliversLatestRelease()
    {
        await CreateProjectWithBuild();
        var target = Path.Combine(_root, "server");
        _dialogs.DeliveryAnswer = _ => new DeliveryConfig { Method = DeliveryMethods.Folder, Path = target };
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        await _vm.DeliverCommand.ExecuteAsync(null);

        // Блок «Патч» пройденного выпуска: пересобрать ту же версию (с подтверждением перезаписи).
        var patch = Stage("1.0.0", StageKind.Patch);
        Assert.True(_vm.RunStageCommand.CanExecute(patch));
        Assert.Equal("Пересобрать", patch.ActionText);
        Thread.Sleep(20);
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.RunStageCommand.ExecuteAsync(patch);

        Assert.Contains(_dialogs.Confirms, m => m.Contains("Перезаписать"));
        var delivery = Stage("1.0.0", StageKind.Delivery);
        Assert.Equal(StageState.Pending, delivery.State); // доставка устарела после пересборки
        Assert.Equal("Доставить", _vm.NextStepText);

        // Блок «Доставка» — доставить заново.
        await _vm.RunStageCommand.ExecuteAsync(delivery);
        Assert.Equal(StageState.Done, Stage("1.0.0", StageKind.Delivery).State);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task RunStage_RerunResetsLaterStages()
    {
        // Выпуск пройден целиком: собран, доставлен (BaseDownloadURL = Output), проверен.
        await CreateProjectWithBuild();
        _dialogs.DeliveryAnswer = _ => new DeliveryConfig { Method = DeliveryMethods.Folder, Path = Path.Combine(_root, "server") };
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        await _vm.DeliverCommand.ExecuteAsync(null);
        await _vm.VerifyCommand.ExecuteAsync(null);
        Assert.Equal(StageState.Done, Stage("1.0.0", StageKind.Verify).State);

        // Доставили заново — прежняя проверка к новой доставке не относится.
        await _vm.RunStageCommand.ExecuteAsync(Stage("1.0.0", StageKind.Delivery));
        Assert.Equal(StageState.Done, Stage("1.0.0", StageKind.Delivery).State);
        Assert.Equal(StageState.Pending, Stage("1.0.0", StageKind.Verify).State);
        Assert.False(ProjectMeta.Load(ProjectDir).VerifiedVersions.ContainsKey("1.0.0"));

        // Проверили и пересобрали патч — сбрасываются и доставка, и проверка.
        await _vm.VerifyCommand.ExecuteAsync(null);
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.RunStageCommand.ExecuteAsync(Stage("1.0.0", StageKind.Patch));
        Assert.Equal(StageState.Done, Stage("1.0.0", StageKind.Patch).State);
        Assert.Equal(StageState.Pending, Stage("1.0.0", StageKind.Delivery).State);
        Assert.Equal(StageState.Pending, Stage("1.0.0", StageKind.Verify).State);
        var meta = ProjectMeta.Load(ProjectDir);
        Assert.False(meta.DeliveredVersions.ContainsKey("1.0.0"));
        Assert.False(meta.VerifiedVersions.ContainsKey("1.0.0"));
        Assert.Equal("Доставить", _vm.NextStepText);
    }

    [Fact]
    public void Catalog_VerificationBeforeRebuildIsNotCounted()
    {
        // Записи до фикса: проверка прошла, потом пересобрали патч (доставка устарела, проверка осталась).
        _service.Create(ProjectDir, "TestApp");
        Directory.CreateDirectory(BuildDir);
        File.WriteAllText(Path.Combine(BuildDir, "app.txt"), "v1");
        _service.StageBuild(ProjectDir, BuildDir, "1.0.0");
        var meta = ProjectMeta.Load(ProjectDir);
        var t = DateTime.Now;
        meta.DeliveredVersions["1.0.0"] = t.AddMinutes(-20);
        meta.VerifiedVersions["1.0.0"] = new VerificationResult(t.AddMinutes(-10), true, "");
        meta.PatchedVersions["1.0.0"] = t;
        meta.Save(ProjectDir);

        var stages = ReleaseCatalog.Build(_service.Open(ProjectDir), null)[0].Platforms[0].Stages;

        Assert.Equal(StageState.Pending, stages[2].State);
        Assert.Equal(StageState.Pending, stages[3].State);
        Assert.Equal("устарела: патч пересобран", stages[3].Detail);
    }

    private StageItem AndroidStage(string version, StageKind kind) =>
        _vm.Releases.First(r => r.Version == version).Platforms.Single(p => p.IsAndroid).Stages.Single(s => s.Kind == kind);

    // Проект с Android: сервер — папка (доставка и BaseDownloadURL через file:///), Android-билд рядом.
    // windows: true — обе платформы, Android привязан к версии Windows; иначе проект только Android (версию спрашивает).
    private async Task<(string Server, string Android)> CreateProjectWithAndroid(bool windows = false)
    {
        await CreateProjectWithBuild();
        var server = Path.Combine(_root, "server");
        var android = Path.Combine(_root, "android");
        Directory.CreateDirectory(android);
        File.WriteAllBytes(Path.Combine(android, "App.apk"), new byte[] { 0x50, 0x4B, 3, 4 });
        File.WriteAllText(Path.Combine(android, "App-x86_64.apk"), "emu");
        File.WriteAllText(Path.Combine(android, "latest.json"), "{\"version\":\"1.0.0\"}");
        new RsaKeyManager().Generate(ProjectDir);

        _dialogs.SettingsAnswer = s =>
        {
            s.BaseDownloadURL = new Uri(server + Path.DirectorySeparatorChar).AbsoluteUri;
            s.WindowsEnabled = windows;
            s.AndroidEnabled = true;
            s.Android.SignMasks.Add("latest.json");
            s.Android.ExcludeMasks.Add("*-x86_64.apk");
            return s;
        };
        await _vm.EditSettingsCommand.ExecuteAsync(null);
        _dialogs.DeliveryAnswer = _ => new DeliveryConfig { Method = DeliveryMethods.Folder, Path = server };
        await _vm.ConfigureDeliveryCommand.ExecuteAsync(null);
        _dialogs.Folders.Enqueue(android);
        await _vm.BrowseAndroidBuildCommand.ExecuteAsync(null);
        return (server, android);
    }

    [Fact]
    public async Task Android_SignDeliverVerify_WindowsDeliveryKeepsChannel()
    {
        var (server, android) = await CreateProjectWithAndroid();
        Assert.Equal(NextStepKind.AndroidSign.ToString(), NextStepFor());

        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        Assert.True(File.Exists(Path.Combine(android, "latest.json.sig")));
        Assert.Equal(StageState.Done, AndroidStage("1.0.0", StageKind.Files).State);

        await _vm.NextStepCommand.ExecuteAsync(null); // «Доставить»
        var channel = Path.Combine(server, "AndroidPatch");
        Assert.Equal(new[] { "App.apk", "latest.json", "latest.json.sig" },
            Directory.GetFiles(channel).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));

        await _vm.NextStepCommand.ExecuteAsync(null); // «Проверить»
        Assert.Empty(_dialogs.Errors);
        Assert.True(ProjectMeta.Load(ProjectDir).AndroidVerifiedVersions["1.0.0"].Succeeded);
        Assert.Equal(StageState.Done, AndroidStage("1.0.0", StageKind.Verify).State);

        // Включили Windows — той же версии: одна запись с двумя полосами; доставка патча не трогает канал Android.
        _dialogs.SettingsAnswer = s =>
        {
            s.WindowsEnabled = true;
            return s;
        };
        await _vm.EditSettingsCommand.ExecuteAsync(null);
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        File.WriteAllText(Path.Combine(server, "old-junk.txt"), "старое");
        await _vm.DeliverCommand.ExecuteAsync(null);
        Assert.True(File.Exists(Path.Combine(server, "VersionInfo.info")));
        Assert.False(File.Exists(Path.Combine(server, "old-junk.txt")));
        Assert.True(File.Exists(Path.Combine(channel, "latest.json.sig")));
        var release = _vm.Releases.Single(r => r.Version == "1.0.0");
        Assert.Equal(new[] { "Windows", "Android" }, release.Platforms.Select(p => p.Platform));
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task AndroidBoundToWindows_TakesWindowsVersion_NoPrompt_NoStandaloneNew()
    {
        var (_, android) = await CreateProjectWithAndroid(windows: true);
        Assert.DoesNotContain(_vm.Releases, r => r.Version == ReleaseItem.UnknownVersion); // без Windows Android ждёт

        // Windows ещё нет — Android записать нельзя: его версия берётся из Windows.
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Errors, e => e.Contains("вместе с Windows"));
        _dialogs.Errors.Clear();

        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        var prompts = _dialogs.Prompts.Count;
        await _vm.SignAndroidCommand.ExecuteAsync(null); // версию не спрашивает
        Assert.Empty(_dialogs.Errors);
        Assert.Equal(prompts, _dialogs.Prompts.Count);
        Assert.True(ProjectMeta.Load(ProjectDir).AndroidSignedVersions.ContainsKey("1.0.0"));
        Assert.Equal(new[] { "Windows", "Android" }, _vm.Releases.Single().Platforms.Select(p => p.Platform));

        // Билд Android поменялся — отдельной записи «новый» нет: Android следующего выпуска пойдёт с Windows.
        File.SetLastWriteTime(Path.Combine(android, "App.apk"), DateTime.Now.AddMinutes(1));
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        Assert.DoesNotContain(_vm.Releases, r => r.Version == ReleaseItem.UnknownVersion);

        File.WriteAllText(Path.Combine(BuildDir, "app.txt"), "v2");
        _dialogs.Prompts.Enqueue("1.0.1");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        Assert.Equal(StageState.Pending, AndroidStage("1.0.1", StageKind.Files).State); // в новом выпуске Windows
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        Assert.True(ProjectMeta.Load(ProjectDir).AndroidSignedVersions.ContainsKey("1.0.1"));
    }

    [Fact]
    public async Task Android_ResignResetsDeliveryAndVerify_BuildChangeDoesNotTouchStoredRelease()
    {
        var (server, android) = await CreateProjectWithAndroid();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        await _vm.VerifyAndroidCommand.ExecuteAsync(null);

        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.RunStageCommand.ExecuteAsync(AndroidStage("1.0.0", StageKind.Files)); // «Подписать заново»
        Assert.Equal(StageState.Done, AndroidStage("1.0.0", StageKind.Files).State);
        Assert.Equal(StageState.Pending, AndroidStage("1.0.0", StageKind.Delivery).State);
        Assert.Equal(StageState.Pending, AndroidStage("1.0.0", StageKind.Verify).State);

        // Манифест в папке билда правили после подписи — это уже следующий билд («новый» в каталоге),
        // а выкладывается сохранённая при подписи копия 1.0.0: подписанная и неизменная.
        File.WriteAllText(Path.Combine(android, "latest.json"), "{\"version\":\"1.0.1\"}");
        // Метка времени файла грубее DateTime.Now (~16 мс) — сдвигаем явно, как было бы в жизни.
        File.SetLastWriteTime(Path.Combine(android, "latest.json"), DateTime.Now.AddMinutes(1));
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Errors);
        Assert.Equal(ReleaseItem.UnknownVersion, _vm.Releases[0].Version); // перерисованный каталог видит новый билд
        Assert.Equal("{\"version\":\"1.0.0\"}", File.ReadAllText(Path.Combine(server, "AndroidPatch", "latest.json")));
        Assert.True(File.Exists(Path.Combine(ProjectService.AndroidVersionPath(ProjectDir, "1.0.0"), "latest.json.sig")));

        // Испортили саму сохранённую копию — выкладывать нельзя: клиент отверг бы подпись.
        File.WriteAllText(Path.Combine(ProjectService.AndroidVersionPath(ProjectDir, "1.0.0"), "latest.json"), "подмена");
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Errors, e => e.Contains("изменился после подписи"));
    }

    [Fact]
    public async Task Android_SignOff_RecordsVersionWithoutKey_DeliversWithoutSig()
    {
        var (server, android) = await CreateProjectWithAndroid();
        File.WriteAllText(Path.Combine(android, "latest.json.sig"), "старая подпись");
        File.Delete(Path.Combine(ProjectDir, RsaKeyManager.PrivateKeyFileName)); // без подписи ключ не нужен
        _dialogs.SettingsAnswer = s =>
        {
            s.Android.Sign = false;
            return s;
        };
        await _vm.EditSettingsCommand.ExecuteAsync(null);
        Assert.Equal("Записать версию", _vm.NextStepText);

        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        var files = AndroidStage("1.0.0", StageKind.Files);
        Assert.Equal(StageState.Done, files.State);
        Assert.Equal("Версия", files.Title);
        Assert.Equal("старая подпись", File.ReadAllText(Path.Combine(android, "latest.json.sig"))); // не подписывали
        Assert.DoesNotContain(_dialogs.Confirms, m => m.Contains("подпис"));

        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        var channel = Path.Combine(server, "AndroidPatch");
        Assert.Equal(new[] { "App.apk", "latest.json" },
            Directory.GetFiles(channel).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));

        await _vm.VerifyAndroidCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Errors);
        Assert.True(ProjectMeta.Load(ProjectDir).AndroidVerifiedVersions["1.0.0"].Succeeded);
    }

    [Fact]
    public async Task Android_StageShowsHowVersionWasRecorded_NotCurrentSettings()
    {
        await CreateProjectWithAndroid();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);

        // Подпись выключили после — подписанная версия так и остаётся подписанной; повтор уже без подписи.
        _dialogs.SettingsAnswer = s =>
        {
            s.Android.Sign = false;
            return s;
        };
        await _vm.EditSettingsCommand.ExecuteAsync(null);
        var stage = AndroidStage("1.0.0", StageKind.Files);
        Assert.Equal("Подпись", stage.Title);
        Assert.StartsWith("подписан", stage.Detail);
        Assert.Equal("Записать заново", stage.ActionText);

        // И выкладывается как записано: с .sig по маскам записи.
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Errors);
        Assert.True(File.Exists(Path.Combine(_root, "server", "AndroidPatch", "latest.json.sig")));
    }

    [Fact]
    public async Task Android_Unsigned_StoredCopyDelivered_TamperedCopyBlocked()
    {
        var (server, android) = await CreateProjectWithAndroid();
        _dialogs.SettingsAnswer = s =>
        {
            s.Android.Sign = false;
            return s;
        };
        await _vm.EditSettingsCommand.ExecuteAsync(null);
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        var stored = ProjectService.AndroidVersionPath(ProjectDir, "1.0.0");
        Assert.Equal(new[] { "App.apk", "latest.json" },
            Directory.GetFiles(stored).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));

        // Папку билда меняют дальше — выкладывается записанное.
        File.WriteAllText(Path.Combine(android, "App.apk"), "другой apk");
        File.WriteAllText(Path.Combine(android, "notes.txt"), "лишнее");
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Errors);
        Assert.Equal(new byte[] { 0x50, 0x4B, 3, 4 }, File.ReadAllBytes(Path.Combine(server, "AndroidPatch", "App.apk")));
        Assert.False(File.Exists(Path.Combine(server, "AndroidPatch", "notes.txt")));

        // Сохранённую копию испортили или подложили файл — отпечатки не сходятся, выкладка отказывает.
        File.WriteAllText(Path.Combine(stored, "App.apk"), "подмена");
        File.WriteAllText(Path.Combine(stored, "extra.txt"), "подложили");
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Errors, e => e.Contains("изменён: App.apk") && e.Contains("новый: extra.txt") &&
            e.Contains("Запишите версию заново"));
    }

    [Fact]
    public async Task Android_Rollback_StoredOlderReleaseBecomesCurrent_NumberNotReused()
    {
        var (server, android) = await CreateProjectWithAndroid();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);

        File.WriteAllText(Path.Combine(android, "App.apk"), "плохой apk");
        File.SetLastWriteTime(Path.Combine(android, "App.apk"), DateTime.Now.AddMinutes(1));
        _dialogs.Prompts.Enqueue("1.0.1");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        var channelApk = Path.Combine(server, "AndroidPatch", "App.apk");
        Assert.Equal("плохой apk", File.ReadAllText(channelApk));

        _vm.SelectedRelease = _vm.Releases.Single(r => r.Version == "1.0.0");
        Assert.True(_vm.SelectedRelease.CanRollback);
        await _vm.RollbackReleaseCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Confirms, m => m.Contains("Откатить сервер на версию 1.0.0?") && m.Contains("Android: выкладка 1.0.0 снова текущая"));
        Assert.Empty(_dialogs.Errors);
        Assert.False(Directory.Exists(ProjectService.AndroidVersionPath(ProjectDir, "1.0.1")));
        Assert.Equal("1.0.0", ReleaseCatalog.LatestAndroidVersion(ProjectMeta.Load(ProjectDir)));
        Assert.Equal(StageState.Pending, AndroidStage("1.0.0", StageKind.Delivery).State);

        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Errors);
        Assert.Equal(new byte[] { 0x50, 0x4B, 3, 4 }, File.ReadAllBytes(channelApk)); // в канале снова 1.0.0

        _dialogs.Prompts.Enqueue("1.0.1");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Errors, e => e.Contains("уже выпускалась"));
    }

    [Fact]
    public async Task DeleteAndroidOnlyRelease_WindowsCanStillBuildThatNumber()
    {
        // Случай из жизни: Android выпустил 1.0.0 и 1.0.1, Windows ещё ничего; 1.0.0 удалили —
        // Windows-билд 1.0.0 после этого должен собираться (Windows этот номер не выпускал).
        var (_, android) = await CreateProjectWithAndroid();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        File.SetLastWriteTime(Path.Combine(android, "App.apk"), DateTime.Now.AddMinutes(1));
        _dialogs.Prompts.Enqueue("1.0.1");
        await _vm.SignAndroidCommand.ExecuteAsync(null);

        _vm.SelectedRelease = _vm.Releases.Single(r => r.Version == "1.0.0");
        await _vm.DeleteReleaseCommand.ExecuteAsync(null);
        Assert.DoesNotContain(_vm.Releases, r => r.Version == "1.0.0");
        var meta = ProjectMeta.Load(ProjectDir);
        Assert.Contains("1.0.0", meta.RemovedAndroidVersions); // Android этот номер повторно не запишет
        Assert.DoesNotContain("1.0.0", meta.RemovedWindowsVersions);

        _dialogs.SettingsAnswer = s =>
        {
            s.WindowsEnabled = true;
            return s;
        };
        await _vm.EditSettingsCommand.ExecuteAsync(null);
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Errors);
        Assert.True(VersionStaged("1.0.0"));
        Assert.Contains(_vm.Releases, r => r.Version == "1.0.0" && r.Platforms.Any(p => p.Platform == "Windows"));
    }

    [Fact]
    public async Task OpenProject_From21_AndroidReleaseSavedIntoProject()
    {
        var (_, android) = await CreateProjectWithAndroid();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);

        // Проект как из 2.1: копии выкладки нет, записи с отпечатками нет, схема 1.
        Directory.Delete(ProjectService.AndroidVersionPath(ProjectDir, "1.0.0"), recursive: true);
        var meta = ProjectMeta.Load(ProjectDir);
        meta.AndroidRecords.Clear();
        meta.SchemaVersion = 1;
        meta.Save(ProjectDir);

        _dialogs.Folders.Enqueue(ProjectDir);
        await _vm.OpenProjectCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Infos, m => m.Contains("донастроен") && m.Contains("Android 1.0.0 сохранена"));
        Assert.True(File.Exists(Path.Combine(ProjectService.AndroidVersionPath(ProjectDir, "1.0.0"), "latest.json.sig")));
        var upgraded = ProjectMeta.Load(ProjectDir);
        Assert.True(upgraded.AndroidRecords["1.0.0"].Signed);
        Assert.NotEmpty(upgraded.AndroidRecords["1.0.0"].Files);
        Assert.Equal(ProjectMeta.CurrentSchemaVersion, upgraded.SchemaVersion);

        _dialogs.Infos.Clear();
        _dialogs.Folders.Enqueue(ProjectDir);
        await _vm.OpenProjectCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Infos); // второй раз донастраивать нечего
    }

    [Fact]
    public async Task Android_SignOnWithoutMasks_Error()
    {
        var (_, android) = await CreateProjectWithAndroid();
        _dialogs.SettingsAnswer = s =>
        {
            s.Android.SignMasks.Clear();
            return s;
        };
        await _vm.EditSettingsCommand.ExecuteAsync(null);

        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.SignAndroidCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Errors, e => e.Contains("маски не заданы"));
        Assert.False(ProjectMeta.Load(ProjectDir).AndroidSignedVersions.ContainsKey("1.0.0"));
        Assert.False(File.Exists(Path.Combine(android, "latest.json.sig")));
    }

    [Fact]
    public Task PlatformSwitch_InWindow_SurvivesStageAction() => HeadlessUi.Run(async () =>
    {
        // Carousel при смене полос сам выбирает первую — выбор Android не должен от этого слетать на Windows.
        await CreateProjectWithAndroid(windows: true);
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        await _vm.SignAndroidCommand.ExecuteAsync(null); // версия — от Windows

        var window = new UpdateManager.App.Views.MainWindow { DataContext = _vm };
        window.Show();
        HeadlessUi.Flush();
        var carousel = Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Carousel>(window, "TrackCarousel")!;

        _vm.ShiftTrack(1);
        HeadlessUi.Flush();
        Assert.Equal("Android", ((PlatformTrack)carousel.SelectedItem!).Platform);

        // Настоящий клик мышью по блоку «Билд» Android — «Сменить папку билда».
        _dialogs.Folders.Enqueue(Path.Combine(_root, "android"));
        await HeadlessUi.Click(window, _vm.SelectedTrack!.Stages[0]);
        Assert.Empty(_dialogs.Folders); // клик дошёл: папку спросили
        Assert.Equal("Android", _vm.SelectedTrack!.Platform);
        Assert.Equal("Android", ((PlatformTrack)carousel.SelectedItem!).Platform);

        // Билд Android обновился — каталог перерисован с записью «новый»; выбор платформы тот же.
        File.SetLastWriteTime(Path.Combine(_root, "android", "App.apk"), DateTime.Now.AddMinutes(1));
        _dialogs.Folders.Enqueue(Path.Combine(_root, "android"));
        await _vm.BrowseAndroidBuildCommand.ExecuteAsync(null);
        HeadlessUi.Flush();
        var log = string.Join(" | ", _vm.Releases.Select(r => r.Version + ":" + string.Join(",", r.Platforms.Select(p => p.Platform))));
        Assert.True(_vm.SelectedTrack!.Platform == "Android", "выбран " + _vm.SelectedRelease!.Version + "/" + _vm.SelectedTrack.Platform + "; каталог " + log);
        Assert.Equal("Android", ((PlatformTrack)carousel.SelectedItem!).Platform);
        Assert.Equal(NextStepKind.AndroidDeliver.ToString(), NextStepFor());
        window.Close();
    });

    // Проект с версиями 1.0.0 … 1.0.<count-1>, собранными по очереди; папки удаляются насовсем, не в Корзину.
    private async Task CreateProjectWithVersions(int count)
    {
        await CreateProjectWithBuild();
        _service.DeleteFolder = folder => Directory.Delete(folder, recursive: true);
        for (int i = 0; i < count; i++)
        {
            File.WriteAllText(Path.Combine(BuildDir, "app.txt"), "v" + i);
            _dialogs.Prompts.Enqueue("1.0." + i);
            await _vm.CreatePatchCommand.ExecuteAsync(null);
        }
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task DeleteRelease_OldGoes_NumbersStay_CurrentRefused()
    {
        await CreateProjectWithVersions(3);
        Assert.Equal(3, _vm.Releases.Single(r => r.Version == "1.0.2").Number);

        _vm.SelectedRelease = _vm.Releases.Single(r => r.Version == "1.0.0");
        await _vm.DeleteReleaseCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Confirms, m => m.Contains("Удалить версию 1.0.0?"));
        Assert.False(VersionStaged("1.0.0"));
        Assert.False(ProjectMeta.Load(ProjectDir).PatchedVersions.ContainsKey("1.0.0"));
        Assert.DoesNotContain(_vm.Releases, r => r.Version == "1.0.0");
        Assert.Equal(2, _vm.Releases.Single(r => r.Version == "1.0.1").Number); // номера не сдвинулись
        Assert.Equal(3, _vm.Releases.Single(r => r.Version == "1.0.2").Number);

        // Текущий выпуск — основа следующего патча и то, что на сервере: не удаляется.
        _vm.SelectedRelease = _vm.Releases.Single(r => r.Version == "1.0.2");
        await _vm.DeleteReleaseCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Errors, e => e.Contains("текущий для Windows"));
        Assert.True(VersionStaged("1.0.2"));

        // Следующий выпуск получает следующий номер, а не занятый удалённым.
        File.WriteAllText(Path.Combine(BuildDir, "app.txt"), "v3");
        _dialogs.Prompts.Enqueue("1.0.3");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        Assert.Equal(4, _vm.Releases.Single(r => r.Version == "1.0.3").Number);
    }

    [Fact]
    public async Task Rollback_NewerGone_PatchRebuilt_NumberNotReused()
    {
        await CreateProjectWithVersions(3);
        _vm.SelectedRelease = _vm.Releases.Single(r => r.Version == "1.0.1");
        Assert.True(_vm.SelectedRelease.CanRollback);
        Assert.False(_vm.Releases.Single(r => r.Version == "1.0.2").CanRollback); // текущий

        await _vm.RollbackReleaseCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Confirms, m => m.Contains("Откатить сервер на версию 1.0.1?") && m.Contains("1.0.2 уйдут в Корзину"));
        Assert.Empty(_dialogs.Errors);
        Assert.False(VersionStaged("1.0.2"));
        Assert.Contains("<Version>1.0.1</Version>", File.ReadAllText(Path.Combine(_service.GetOutputPath(ProjectDir), "VersionInfo.info")));

        var current = _vm.Releases[0];
        Assert.Equal("1.0.1", current.Version);
        Assert.Equal(2, current.Number);
        Assert.Equal(StageState.Done, Stage("1.0.1", StageKind.Patch).State); // пересобран — дальше доставка
        Assert.Equal(NextStepKind.Deliver.ToString(), NextStepFor());

        // Номер откатанной версии не повторяется: клиенты, получившие 1.0.2, не увидели бы обновления.
        _dialogs.Prompts.Enqueue("1.0.2");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        Assert.Contains(_dialogs.Errors, e => e.Contains("уже выпускалась"));
        _dialogs.Prompts.Enqueue("1.0.3");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        Assert.Equal(4, _vm.Releases.Single(r => r.Version == "1.0.3").Number);
    }

    [Fact]
    public Task DeleteReleases_AreaSelection_ShrinksBack_MenuAfterRelease_DismissClears() => HeadlessUi.Run(async () =>
    {
        await CreateProjectWithVersions(4); // 1.0.3 текущий, 1.0.2 … 1.0.0 — старые
        var window = new UpdateManager.App.Views.MainWindow { DataContext = _vm };
        window.Show();
        HeadlessUi.Flush();
        var list = Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.ListBox>(window, "CatalogList")!;
        var menu = (Avalonia.Controls.ContextMenu)list.Resources["MarkedMenu"]!;
        Avalonia.Point At(string version) => HeadlessUi.CenterOf(window, _vm.Releases.Single(r => r.Version == version));
        string[] Marked() => _vm.Releases.Where(r => r.IsChecked).Select(r => r.Version).ToArray();

        // Удержание на 1.0.2 секунду — точка начала; тянем вниз до 1.0.0: выделено всё между.
        window.MouseDown(At("1.0.2"), Avalonia.Input.MouseButton.Left);
        await HeadlessUi.Wait(TimeSpan.FromSeconds(1.2));
        Assert.Equal(new[] { "1.0.2" }, Marked());
        window.MouseMove(At("1.0.1"));
        window.MouseMove(At("1.0.0"));
        Assert.Equal(new[] { "1.0.2", "1.0.1", "1.0.0" }, Marked());
        Assert.False(menu.IsOpen); // меню — только после отпускания

        // Обратно к точке начала — выделение сжимается; выше точки — текущий 1.0.3 не выделяется.
        window.MouseMove(At("1.0.1"));
        Assert.Equal(new[] { "1.0.2", "1.0.1" }, Marked());
        window.MouseMove(At("1.0.3"));
        Assert.Equal(new[] { "1.0.2" }, Marked());
        window.MouseMove(At("1.0.1"));
        window.MouseUp(At("1.0.1"), Avalonia.Input.MouseButton.Left);
        HeadlessUi.Flush();
        Assert.True(menu.IsOpen);
        Assert.Equal(2, _vm.MarkedCount);

        // Меню закрыли мимо — выделение спало.
        menu.Close();
        HeadlessUi.Flush();
        Assert.Empty(Marked());
        Assert.False(_vm.IsMarkingReleases);

        // Ещё раз — и «Удалить выделенные».
        window.MouseDown(At("1.0.1"), Avalonia.Input.MouseButton.Left);
        await HeadlessUi.Wait(TimeSpan.FromSeconds(1.2));
        window.MouseMove(At("1.0.0"));
        window.MouseUp(At("1.0.0"), Avalonia.Input.MouseButton.Left);
        HeadlessUi.Flush();
        await _vm.DeleteMarkedReleasesCommand.ExecuteAsync(null);
        menu.Close();
        HeadlessUi.Flush();
        Assert.Equal(new[] { "1.0.3", "1.0.2" }, _vm.Releases.Select(r => r.Version));
        Assert.False(_vm.IsMarkingReleases);
        window.Close();
    });

    [Fact]
    public Task DeleteReleases_DragPastEdge_ScrollsAndExtends() => HeadlessUi.Run(async () =>
    {
        await CreateProjectWithVersions(14);
        var window = new UpdateManager.App.Views.MainWindow { DataContext = _vm, Width = 1000, Height = 420 };
        window.Show();
        HeadlessUi.Flush();
        var list = Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.ListBox>(window, "CatalogList")!;
        var scroll = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(list).OfType<Avalonia.Controls.ScrollViewer>().First();
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, "каталог должен не помещаться");

        window.MouseDown(HeadlessUi.CenterOf(window, _vm.Releases[1]), Avalonia.Input.MouseButton.Left);
        await HeadlessUi.Wait(TimeSpan.FromSeconds(1.2));
        // Указатель ниже списка: список едет вниз, выделение тянется до самой старой записи.
        var below = Avalonia.VisualExtensions.TranslatePoint(list, new Avalonia.Point(list.Bounds.Width / 2, list.Bounds.Height + 30), window)!.Value;
        window.MouseMove(below);
        await HeadlessUi.Wait(TimeSpan.FromSeconds(2.5));
        Assert.True(scroll.Offset.Y > 0);
        Assert.True(_vm.Releases[^1].IsChecked, "до последней записи: " + _vm.MarkedCount);
        Assert.Equal(_vm.Releases.Count - 1, _vm.MarkedCount); // все, кроме текущего 1.0.13
        window.MouseUp(below, Avalonia.Input.MouseButton.Left);
        window.Close();
    });

    [Fact]
    public Task DeleteReleases_ShortPressDoesNotMark() => HeadlessUi.Run(async () =>
    {
        await CreateProjectWithVersions(2);
        var window = new UpdateManager.App.Views.MainWindow { DataContext = _vm };
        window.Show();
        HeadlessUi.Flush();

        var at = HeadlessUi.CenterOf(window, _vm.Releases.Single(r => r.Version == "1.0.0"));
        window.MouseDown(at, Avalonia.Input.MouseButton.Left);
        await HeadlessUi.Wait(TimeSpan.FromSeconds(0.5));
        window.MouseUp(at, Avalonia.Input.MouseButton.Left);
        await HeadlessUi.Wait(TimeSpan.FromSeconds(2));
        Assert.False(_vm.IsMarkingReleases);
        Assert.Equal("1.0.0", _vm.SelectedRelease!.Version); // обычный клик — выбор выпуска
        window.Close();
    });

    [Fact]
    public async Task PlatformSwitch_OneTrackOnScreen_ButtonFollowsIt()
    {
        await CreateProjectWithAndroid(windows: true);
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);
        await _vm.SignAndroidCommand.ExecuteAsync(null); // версия — от Windows

        // Выпуск 1.0.0 с двумя полосами: на экране одна, кнопка — её следующий шаг.
        var release = _vm.SelectedRelease!;
        Assert.True(release.IsMultiPlatform);
        Assert.Equal("Windows", _vm.SelectedTrack!.Platform);
        Assert.True(_vm.SelectedTrack.IsSelected);
        Assert.Equal(NextStepKind.Deliver.ToString(), NextStepFor());

        _vm.ShiftTrack(1); // свайп влево — Android
        Assert.Equal("Android", _vm.SelectedTrack!.Platform);
        Assert.False(release.Platforms[0].IsSelected);
        Assert.Equal(NextStepKind.AndroidDeliver.ToString(), NextStepFor());
        _vm.ShiftTrack(1); // дальше некуда
        Assert.Equal("Android", _vm.SelectedTrack!.Platform);

        // Выбор платформы переживает перерисовку каталога.
        await _vm.DeliverAndroidCommand.ExecuteAsync(null);
        Assert.Equal("Android", _vm.SelectedTrack!.Platform);
        Assert.Equal(NextStepKind.AndroidVerify.ToString(), NextStepFor());

        _vm.SelectTrackCommand.Execute(_vm.SelectedRelease!.Platforms[0]); // клик по «Windows»
        Assert.Equal("Windows", _vm.SelectedTrack!.Platform);
    }

    [Fact]
    public async Task RemoveFromRecent_KeepsFolder_ClosesOpenProject()
    {
        await CreateProjectWithBuild();

        _vm.RemoveFromRecentCommand.Execute(ProjectDir); // контекстное меню карточки

        Assert.False(_vm.HasProject);
        Assert.Empty(_recent.Load());
        Assert.True(_service.IsProjectFolder(ProjectDir));
    }

    [Fact]
    public async Task DeleteProject_AfterConfirm_FolderGone()
    {
        await CreateProjectWithBuild();
        new RsaKeyManager().Generate(ProjectDir);
        _service.DeleteFolder = p => Directory.Delete(p, recursive: true); // без Корзины в тестах

        _dialogs.ConfirmAnswer = _ => false;
        await _vm.DeleteProjectCommand.ExecuteAsync(ProjectDir);
        Assert.True(Directory.Exists(ProjectDir));
        Assert.True(_vm.HasProject);

        _dialogs.ConfirmAnswer = _ => true;
        await _vm.DeleteProjectCommand.ExecuteAsync(ProjectDir);
        Assert.Contains(_dialogs.Confirms, m => m.Contains("private.key"));
        Assert.False(Directory.Exists(ProjectDir));
        Assert.False(_vm.HasProject);
        Assert.Empty(_recent.Load());
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public void DeleteProject_RefusesNonProjectFolder()
    {
        var folder = Path.Combine(_root, "not-a-project");
        Directory.CreateDirectory(folder);
        _service.DeleteFolder = p => Directory.Delete(p, recursive: true);

        Assert.Throws<InvalidOperationException>(() => _service.DeleteProject(folder));
        Assert.True(Directory.Exists(folder));
    }

    private string NextStepFor() =>
        typeof(MainWindowViewModel).GetField("_nextStep",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_vm)!.ToString()!;

    [Fact]
    public async Task RunStage_LockedBeyondNextStepAndInArchive()
    {
        await CreateProjectWithBuild();
        _dialogs.Prompts.Enqueue("1.0.0");
        await _vm.CreatePatchCommand.ExecuteAsync(null);

        // Патч собран, не доставлен: «Проверка» заперта (сначала доставка).
        Assert.True(_vm.RunStageCommand.CanExecute(Stage("1.0.0", StageKind.Delivery)));
        Assert.False(_vm.RunStageCommand.CanExecute(Stage("1.0.0", StageKind.Verify)));

        File.WriteAllText(Path.Combine(BuildDir, "app.txt"), "v2");
        _dialogs.Prompts.Enqueue("1.0.1");
        await _vm.CreatePatchCommand.ExecuteAsync(null);

        // 1.0.0 теперь архивный: его блоки не нажимаются.
        Assert.All(_vm.Releases.Single(r => r.Version == "1.0.0").Platforms[0].Stages,
            s => Assert.False(_vm.RunStageCommand.CanExecute(s)));
    }

    [Fact]
    public async Task ConfigureDelivery_SavesMethodWithoutDelivering()
    {
        await CreateProjectWithBuild();
        var target = Path.Combine(_root, "server");
        _dialogs.DeliveryAnswer = _ => new DeliveryConfig { Method = DeliveryMethods.Folder, Path = target };

        await _vm.ConfigureDeliveryCommand.ExecuteAsync(null);

        Assert.Equal(target, ProjectMeta.Load(ProjectDir).Delivery.Path);
        Assert.False(Directory.Exists(target)); // только настройка, без доставки
        Assert.Equal("папка · " + target, _vm.DeliveryText);
    }

    [Fact]
    public async Task OpenRecent_MissingFolderIsOfferedForRemoval()
    {
        var missing = Path.Combine(_root, "gone");
        _recent.Add(missing);

        await _vm.OpenRecentCommand.ExecuteAsync(missing);

        Assert.Contains(_dialogs.Confirms, m => m.Contains("Проект не найден"));
        Assert.Empty(_recent.Load());
        Assert.False(_vm.HasProject);
    }

    [Fact]
    public async Task EditSettings_SavesAndRefreshesName()
    {
        await CreateProjectWithBuild();
        _dialogs.SettingsAnswer = s =>
        {
            s.Name = "Renamed";
            s.BaseDownloadURL = "http://host/dir";
            return s;
        };

        await _vm.EditSettingsCommand.ExecuteAsync(null);

        Assert.Equal("Renamed", _vm.ProjectName);
        Assert.Equal("http://host/dir/", _service.LoadSettings(ProjectDir).BaseDownloadURL);
    }
}
