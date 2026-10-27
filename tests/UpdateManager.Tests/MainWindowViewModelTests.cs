using UpdateManager.App.Services;
using UpdateManager.App.ViewModels;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Project;
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
            new ThemeService(Path.Combine(_root, "theme.txt")), new SelfUpdateViewModel(_dialogs, config));
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
            new ThemeService(Path.Combine(_root, "theme.txt")), new SelfUpdateViewModel(_dialogs, config));

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
