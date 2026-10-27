using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePatchToolCore;
using UpdateManager.Core.Common;
using UpdateManager.Core.Project;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Редактирование основных настроек проекта (движковый Settings.xml через DTO).
/// При «Сохранить» заполняет <see cref="Result"/> и закрывает окно с OK.
/// </summary>
public sealed partial class ProjectSettingsViewModel : DialogViewModel
{
    public const int BinaryDiffQualityMin = 0;
    public const int BinaryDiffQualityMax = 9;

    public ProjectSettingsViewModel(ProjectSettings settings)
    {
        _name = settings.Name;
        // BaseDownloadURL показываем двумя полями: адрес сервера + директория загрузки.
        DownloadUrl.Split(settings.BaseDownloadURL, out var server, out var directory);
        _server = server;
        _uploadDirectory = directory;
        _maintenanceCheckUrl = settings.MaintenanceCheckURL;
        _isSelfPatchingApp = settings.IsSelfPatchingApp;
        _signPatch = settings.SignPatch;
        _createRepairPatch = settings.CreateRepairPatch;
        _createInstallerPatch = settings.CreateInstallerPatch;
        _createIncrementalPatch = settings.CreateIncrementalPatch;
        _createAllIncrementalPatches = settings.CreateAllIncrementalPatches;
        _ignoredPaths = string.Join(Environment.NewLine, settings.IgnoredPaths);
        _compressionRepair = settings.CompressionFormatRepairPatch;
        _compressionInstaller = settings.CompressionFormatInstallerPatch;
        _compressionIncremental = settings.CompressionFormatIncrementalPatch;
        _binaryDiffQuality = Math.Clamp(settings.BinaryDiffQuality, BinaryDiffQualityMin, BinaryDiffQualityMax);
        _dontCreatePatchFilesForUnchangedFiles = settings.DontCreatePatchFilesForUnchangedFiles;
    }

    /// <summary>Форматы сжатия — из движкового enum (LZMA/GZIP/NONE).</summary>
    public CompressionFormat[] CompressionFormats { get; } = Enum.GetValues<CompressionFormat>();

    public ProjectSettings? Result { get; private set; }

    [ObservableProperty] private string _name;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadUrlText), nameof(HasNoServer))]
    private string _server;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadUrlText))]
    private string _uploadDirectory;

    /// <summary>Итоговый BaseDownloadURL — откуда клиенты будут качать патч.</summary>
    public string DownloadUrlText => HasNoServer
        ? "Адрес сервера не задан — патч не соберётся: клиентам неоткуда будет качать файлы."
        : "Клиенты будут качать патч с " + DownloadUrl.Join(Server, UploadDirectory);

    public bool HasNoServer => string.IsNullOrWhiteSpace(Server);

    [ObservableProperty] private string _maintenanceCheckUrl;
    [ObservableProperty] private bool _isSelfPatchingApp;
    [ObservableProperty] private bool _signPatch;
    [ObservableProperty] private bool _createRepairPatch;
    [ObservableProperty] private bool _createInstallerPatch;
    [ObservableProperty] private bool _createIncrementalPatch;
    [ObservableProperty] private bool _createAllIncrementalPatches;
    [ObservableProperty] private string _ignoredPaths;
    [ObservableProperty] private CompressionFormat _compressionRepair;
    [ObservableProperty] private CompressionFormat _compressionInstaller;
    [ObservableProperty] private CompressionFormat _compressionIncremental;
    [ObservableProperty] private decimal? _binaryDiffQuality;
    [ObservableProperty] private bool _dontCreatePatchFilesForUnchangedFiles;

    [RelayCommand]
    private void Save()
    {
        Result = new ProjectSettings
        {
            Name = Name.Trim(),
            BaseDownloadURL = DownloadUrl.Join(Server, UploadDirectory),
            MaintenanceCheckURL = MaintenanceCheckUrl.Trim(),
            IsSelfPatchingApp = IsSelfPatchingApp,
            SignPatch = SignPatch,
            CreateRepairPatch = CreateRepairPatch,
            CreateInstallerPatch = CreateInstallerPatch,
            CreateIncrementalPatch = CreateIncrementalPatch,
            CreateAllIncrementalPatches = CreateAllIncrementalPatches,
            IgnoredPaths = IgnoredPaths
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList(),
            CompressionFormatRepairPatch = CompressionRepair,
            CompressionFormatInstallerPatch = CompressionInstaller,
            CompressionFormatIncrementalPatch = CompressionIncremental,
            BinaryDiffQuality = (int)(BinaryDiffQuality ?? 3),
            DontCreatePatchFilesForUnchangedFiles = DontCreatePatchFilesForUnchangedFiles
        };
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}
