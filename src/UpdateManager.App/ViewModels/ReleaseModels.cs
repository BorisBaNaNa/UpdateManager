using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.Common;
using UpdateManager.Core.Project;
using UpdateManager.Core.Security;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Этап выпуска. Набор этапов зависит от платформы: Windows — Билд → Патч → Доставка → Проверка,
/// Android — Билд → Подпись (Files) → Доставка → Проверка.
/// </summary>
public enum StageKind { Build, Patch, Files, Delivery, Verify }

public enum StageState { Pending, Done, Failed }

/// <summary>Один блок полосы этапов: что за этап, пройден ли и короткая подпись к нему.</summary>
public sealed class StageItem
{
    public StageItem(StageKind kind, StageState state, string detail, string fullDetail = "", string reason = "")
    {
        Kind = kind;
        State = state;
        Detail = detail;
        FullDetail = fullDetail.Length > 0 ? fullDetail : detail;
        Reason = reason;
    }

    public StageKind Kind { get; }
    public StageState State { get; }

    /// <summary>Платформа полосы («Windows», «Android») — задаёт <see cref="PlatformTrack"/>.</summary>
    public string Platform { get; internal set; } = ReleaseCatalog.Windows;

    /// <summary>Причина провала целиком (пусто, если этап не провален).</summary>
    public string Reason { get; }

    /// <summary>Короткая подпись в блоке: дата или причина в одну строку.</summary>
    public string Detail { get; }

    /// <summary>Полный текст для подсказки (например, вся причина провала проверки).</summary>
    public string FullDetail { get; }

    public string Title => Kind switch
    {
        StageKind.Build => "Билд",
        StageKind.Patch => "Патч",
        StageKind.Files => "Подпись",
        StageKind.Delivery => "Доставка",
        _ => "Проверка"
    };

    public string ToolTipText => Platform + " · " + Title + ": " + FullDetail;

    /// <summary>
    /// Блок можно нажать: только в текущем выпуске платформы и только пройденный или следующий этап —
    /// повторить шаг (пересобрать, доставить снова) можно, перепрыгнуть через непройденный — нет.
    /// </summary>
    public bool IsActionable { get; internal set; }

    /// <summary>Что сделает нажатие на блок.</summary>
    public string ActionText => Kind switch
    {
        StageKind.Build => "Сменить папку билда",
        StageKind.Patch => State == StageState.Done ? "Пересобрать" : "Собрать",
        StageKind.Files => State == StageState.Done ? "Подписать заново" : "Подписать",
        StageKind.Delivery => State == StageState.Done ? "Доставить снова" : "Доставить",
        _ => State == StageState.Pending ? "Проверить" : "Проверить снова"
    };

    // Классы для стилей: цвет блока задаёт этап, заливка — состояние.
    public bool IsBuild => Kind == StageKind.Build;
    public bool IsPatch => Kind == StageKind.Patch;
    public bool IsFiles => Kind == StageKind.Files;
    public bool IsDelivery => Kind == StageKind.Delivery;
    public bool IsVerify => Kind == StageKind.Verify;
    public bool IsDone => State == StageState.Done;
    public bool IsFailed => State == StageState.Failed;
}

/// <summary>Полоса этапов одной платформы внутри выпуска (в выпуске их может быть несколько).</summary>
public sealed partial class PlatformTrack : ObservableObject
{
    /// <summary>Эта полоса сейчас на экране (переключатель платформ возле версии).</summary>
    [ObservableProperty] private bool _isSelected;

    public PlatformTrack(string platform, IReadOnlyList<StageItem> stages)
    {
        Platform = platform;
        Stages = stages;
        foreach (var stage in stages)
            stage.Platform = platform;
    }

    public string Platform { get; }
    public IReadOnlyList<StageItem> Stages { get; }

    public bool IsAndroid => Platform == ReleaseCatalog.Android;

    /// <summary>
    /// Текущий выпуск платформы (самая новая запись, где она есть): только его этапы нажимаются
    /// и только для него считается следующий шаг.
    /// </summary>
    public bool IsCurrent { get; internal set; }

    /// <summary>Подпись над полосой, когда в выпуске больше одной платформы.</summary>
    public string Caption => Platform + " · " + StateText;

    /// <summary>В выпуске больше одной платформы — над полосой подпись (ставит <see cref="ReleaseItem"/>).</summary>
    public bool ShowCaption { get; internal set; }

    /// <summary>Первый непройденный этап (null — выпуск по этой платформе завершён).</summary>
    public StageItem? NextStage => Stages.FirstOrDefault(s => s.State != StageState.Done);

    /// <summary>Состояние словами — цвет полосы никогда не единственный носитель.</summary>
    public string StateText => NextStage switch
    {
        null => "проверен",
        { Kind: StageKind.Build } => "нет билда",
        { Kind: StageKind.Patch } => "патч не собран",
        { Kind: StageKind.Files } => "не подписан",
        { Kind: StageKind.Delivery } => "не доставлен",
        { IsFailed: true } => "проверка не прошла",
        _ => "не проверялся"
    };
}

/// <summary>Запись каталога: выпуск (версия) со своим номером и полосами этапов по платформам.</summary>
public sealed class ReleaseItem
{
    /// <summary>Подпись записи «новый» Android-билда, версию которого спросят при подписи.</summary>
    public const string UnknownVersion = "новый";

    public ReleaseItem(int number, string version, bool isNew, bool isLatest, IReadOnlyList<PlatformTrack> platforms)
    {
        Number = number;
        Version = version;
        IsNew = isNew;
        IsLatest = isLatest;
        Platforms = platforms;
        foreach (var track in platforms)
            track.ShowCaption = platforms.Count > 1;
    }

    public int Number { get; }
    public string Version { get; }

    /// <summary>Билд найден в папке-источнике, но ещё не собран в патч (или не подписан).</summary>
    public bool IsNew { get; }

    /// <summary>Самая новая запись каталога.</summary>
    public bool IsLatest { get; }

    public IReadOnlyList<PlatformTrack> Platforms { get; }

    /// <summary>Полос больше одной — над каждой подпись платформы.</summary>
    public bool IsMultiPlatform => Platforms.Count > 1;

    /// <summary>Хоть одна полоса — текущий выпуск своей платформы (иначе запись архивная).</summary>
    public bool HasCurrentTrack => Platforms.Any(p => p.IsCurrent);

    public string NumberText => IsNew ? "новый" : "№ " + Number;

    /// <summary>Состояние выпуска словами; при нескольких платформах — по каждой.</summary>
    public string StateText => Platforms.Count == 1
        ? Platforms[0].StateText
        : string.Join(", ", Platforms.Select(p => p.Platform + ": " + p.StateText));

    /// <summary>Вторая строка записи каталога: «№ 5 · проверка не прошла».</summary>
    public string CatalogLine => NumberText + " · " + StateText;

    public string Kicker => string.Join(" · ", Platforms.Select(p => p.Platform)) +
        (IsNew ? " · новый билд" : " · выпуск " + Number);

    /// <summary>Хвост строки у версии, когда платформы показаны переключателем: «выпуск 5» / «новый билд».</summary>
    public string KickerTail => IsNew ? "новый билд" : "выпуск " + Number;
}

/// <summary>Карточка недавнего проекта на стартовом экране.</summary>
public sealed class RecentProjectCard
{
    public RecentProjectCard(string path, string? name, ReleaseItem? lastRelease, IRelayCommand<string> open)
    {
        Path = path;
        Name = name ?? "";
        IsMissing = name == null;
        LastRelease = lastRelease;
        OpenCommand = open;
    }

    public string Path { get; }
    public string Name { get; }
    public ReleaseItem? LastRelease { get; }
    public IRelayCommand<string> OpenCommand { get; }

    /// <summary>Папку проекта удалили или перенесли.</summary>
    public bool IsMissing { get; }

    public string Title => Name.Length > 0 ? Name : System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
    public string Summary => IsMissing ? "папка не найдена — нажмите, чтобы убрать из списка"
        : LastRelease == null ? "версий пока нет"
        : LastRelease.Version + " · " + LastRelease.StateText;
}

/// <summary>
/// Собирает каталог выпусков проекта из Versions/ (Windows) и меты (Android). Выпуск — версия; у него
/// полоса на каждую включённую платформу, по которой эта версия выпускалась.
/// </summary>
public static class ReleaseCatalog
{
    public const string Windows = "Windows";
    public const string Android = "Android";

    /// <summary>
    /// Выпуски от нового к старому. <paramref name="newBuildVersion"/> — версия Windows-билда в папке-источнике,
    /// которой ещё нет в Versions/ (null — нет такой): становится записью «новый билд». Android-билд, изменённый
    /// после последней подписи, — полоса «новый» (версию спросят при подписи).
    /// </summary>
    public static List<ReleaseItem> Build(UpdateProject project, string? newBuildVersion, string newBuildDetail = "")
    {
        var meta = project.Meta;
        var tracks = new Dictionary<string, List<PlatformTrack>>(StringComparer.OrdinalIgnoreCase);

        void Add(string version, PlatformTrack track)
        {
            if (!tracks.TryGetValue(version, out var list))
                tracks[version] = list = new List<PlatformTrack>();
            list.Add(track);
        }

        if (meta.WindowsEnabled)
        {
            var delivered = DeliveredDates(project);
            var maxVersion = project.Versions.Count > 0 ? project.Versions[^1].Version : null;
            foreach (var v in project.Versions)
                Add(v.Version, WindowsTrack(project, v, v.Version == maxVersion, delivered));
        }

        if (meta.AndroidEnabled)
            foreach (var version in AndroidVersions(meta))
                Add(version, AndroidTrack(meta, version));

        var ordered = tracks.Keys.OrderBy(VersionKey).ThenBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
        var result = ordered
            .Select((version, i) => new ReleaseItem(i + 1, version, isNew: false, isLatest: false,
                tracks[version].OrderBy(t => t.IsAndroid).ToList()))
            .ToList();

        // Новый Windows-билд: своя запись (или полоса в записи той же версии, если Android её уже выпускал).
        if (meta.WindowsEnabled && newBuildVersion != null)
        {
            var windows = new PlatformTrack(Windows, new List<StageItem>
            {
                new(StageKind.Build, StageState.Done, newBuildDetail),
                new(StageKind.Patch, StageState.Pending, "не собран"),
                new(StageKind.Delivery, StageState.Pending, "не доставлен"),
                new(StageKind.Verify, StageState.Pending, "не проверялся")
            });
            var same = result.FindIndex(r => string.Equals(r.Version, newBuildVersion, StringComparison.OrdinalIgnoreCase));
            if (same >= 0)
            {
                result[same] = new ReleaseItem(result[same].Number, result[same].Version, isNew: false, isLatest: false,
                    new[] { windows }.Concat(result[same].Platforms).ToList());
            }
            else
            {
                var platforms = new List<PlatformTrack> { windows };
                if (meta.AndroidEnabled)
                    platforms.Add(AndroidPendingTrack(meta)); // Android того же выпуска — подписать вместе
                result.Add(new ReleaseItem(result.Count + 1, newBuildVersion, isNew: true, isLatest: false, platforms));
            }
        }

        // Android без текущей полосы (ещё не выпускался или билд изменился после подписи) — полоса «новый».
        if (meta.AndroidEnabled && !result.Any(r => r.IsNew && r.Platforms.Any(p => p.IsAndroid)) && AndroidBuildIsNew(meta))
        {
            var last = result.Count > 0 ? result[^1] : null;
            if (last != null && !last.Platforms.Any(p => p.IsAndroid))
                result[^1] = new ReleaseItem(last.Number, last.Version, last.IsNew, isLatest: false,
                    last.Platforms.Append(AndroidPendingTrack(meta)).ToList());
            else
                result.Add(new ReleaseItem(result.Count + 1, ReleaseItem.UnknownVersion, isNew: true, isLatest: false,
                    new[] { AndroidPendingTrack(meta) }));
        }

        result.Reverse();
        if (result.Count > 0)
            result[0] = new ReleaseItem(result[0].Number, result[0].Version, result[0].IsNew, isLatest: true, result[0].Platforms);

        // Текущий выпуск у каждой платформы свой — самая новая запись, где она есть.
        foreach (var platform in new[] { Windows, Android })
        {
            var track = result.SelectMany(r => r.Platforms).FirstOrDefault(p => p.Platform == platform);
            if (track == null)
                continue;
            track.IsCurrent = true;
            // Пройденные этапы и первый непройденный; дальше — заперто, пока не пройден предыдущий.
            var next = track.NextStage;
            var limit = next == null ? track.Stages.Count - 1 : IndexOf(track.Stages, next);
            for (int i = 0; i <= limit; i++)
                track.Stages[i].IsActionable = true;
        }
        return result;
    }

    // --- Windows ---

    private static PlatformTrack WindowsTrack(UpdateProject project, ProjectVersion v, bool isMax,
        Dictionary<string, DateTime> delivered)
    {
        var meta = project.Meta;
        var stages = new List<StageItem>
        {
            new(StageKind.Build, StageState.Done, "скопирован " + Stamp(v.BuildDate))
        };

        // Патч: точная дата есть с 2.0; раньше — только «Output готов» для самой новой версии,
        // а более старые версии собирались до неё (иначе их не было бы в цепочке).
        DateTime? patchedAt = meta.PatchedVersions.TryGetValue(v.Version, out var p) ? p : null;
        var patched = patchedAt.HasValue || !isMax || (project.OutputReady && meta.LastBuiltSettings.Length > 0);
        stages.Add(patched
            ? new StageItem(StageKind.Patch, StageState.Done, patchedAt.HasValue ? "собран " + Stamp(patchedAt.Value) : "собран")
            : new StageItem(StageKind.Patch, StageState.Pending, "не собран"));

        DateTime? deliveredAt = delivered.TryGetValue(v.Version, out var d) ? d : null;
        AddDeliveryAndVerify(stages, meta, patched, patchedAt, deliveredAt, meta.VerifiedVersions, v.Version, "патч пересобран");
        return new PlatformTrack(Windows, stages);
    }

    // Даты доставки по версиям. Проекты без них (1.x и ранние 2.0) знают только время последней
    // доставки — относим его к самой новой версии, собранной до неё, а не ко всем сразу.
    private static Dictionary<string, DateTime> DeliveredDates(UpdateProject project)
    {
        var meta = project.Meta;
        if (meta.DeliveredVersions.Count > 0 || !meta.LastDeliveredAt.HasValue)
            return meta.DeliveredVersions;

        var legacy = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var last = project.Versions.LastOrDefault(v => v.BuildDate <= meta.LastDeliveredAt.Value);
        if (last != null)
            legacy[last.Version] = meta.LastDeliveredAt.Value;
        return legacy;
    }

    // --- Android ---

    /// <summary>Версии, по которым Android выпускался (есть хоть одна запись этапа).</summary>
    public static IEnumerable<string> AndroidVersions(ProjectMeta meta) =>
        meta.AndroidSignedVersions.Keys
            .Concat(meta.AndroidDeliveredVersions.Keys)
            .Concat(meta.AndroidVerifiedVersions.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Самая новая версия Android-выпуска (null — ещё не выпускался).</summary>
    public static string? LatestAndroidVersion(ProjectMeta meta) =>
        AndroidVersions(meta).OrderBy(VersionKey).LastOrDefault();

    private static PlatformTrack AndroidTrack(ProjectMeta meta, string version)
    {
        var stages = new List<StageItem> { new(StageKind.Build, StageState.Done, BuildFolderLabel(meta)) };

        DateTime? signedAt = meta.AndroidSignedVersions.TryGetValue(version, out var s) ? s : null;
        stages.Add(signedAt.HasValue
            ? new StageItem(StageKind.Files, StageState.Done,
                (meta.Android.SignMasks.Count > 0 ? "подписан " : "без подписи · ") + Stamp(signedAt.Value),
                (meta.Android.SignMasks.Count > 0 ? "подписан " : "без подписи · ") + Stamp(signedAt.Value) +
                (meta.Android.SignMasks.Count > 0 ? "\nмаски: " + string.Join(", ", meta.Android.SignMasks) : ""))
            : new StageItem(StageKind.Files, StageState.Pending, "не подписан"));

        DateTime? deliveredAt = meta.AndroidDeliveredVersions.TryGetValue(version, out var d) ? d : null;
        AddDeliveryAndVerify(stages, meta, signedAt.HasValue, signedAt, deliveredAt, meta.AndroidVerifiedVersions, version,
            "подписан заново");
        return new PlatformTrack(Android, stages);
    }

    private static PlatformTrack AndroidPendingTrack(ProjectMeta meta)
    {
        var folder = meta.Android.BuildSource ?? "";
        var hasBuild = folder.Length > 0 && Directory.Exists(folder);
        return new PlatformTrack(Android, new List<StageItem>
        {
            hasBuild
                ? new StageItem(StageKind.Build, StageState.Done, BuildFolderLabel(meta))
                : new StageItem(StageKind.Build, StageState.Pending, "папка не выбрана"),
            new(StageKind.Files, StageState.Pending, "не подписан"),
            new(StageKind.Delivery, StageState.Pending, "не доставлен"),
            new(StageKind.Verify, StageState.Pending, "не проверялся")
        });
    }

    // Android ещё не выпускался или в папке билда есть файлы новее последней подписи.
    private static bool AndroidBuildIsNew(ProjectMeta meta)
    {
        var latest = LatestAndroidVersion(meta);
        if (latest == null)
            return true;
        if (!meta.AndroidSignedVersions.TryGetValue(latest, out var signedAt))
            return false;

        var folder = meta.Android.BuildSource ?? "";
        if (folder.Length == 0 || !Directory.Exists(folder))
            return false;
        try
        {
            return FileMask.ListFiles(folder)
                .Where(f => !FileSigner.IsSignature(f))
                .Any(f => File.GetLastWriteTime(Path.Combine(folder, f)) > signedAt);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string BuildFolderLabel(ProjectMeta meta)
    {
        var folder = (meta.Android.BuildSource ?? "").TrimEnd('\\', '/');
        return folder.Length > 0 ? Path.GetFileName(folder) + "\\" : "папка не выбрана";
    }

    // --- общее ---

    // Этап не засчитан, если выполнен раньше предыдущего. Повтор этапа и так стирает записи следующих
    // (MainWindowViewModel.ResetStagesFrom); правила ниже держат и старые записи — например, проверку,
    // сделанную до пересборки патча.
    private static void AddDeliveryAndVerify(List<StageItem> stages, ProjectMeta meta, bool prepared, DateTime? preparedAt,
        DateTime? deliveredAt, Dictionary<string, VerificationResult> verified, string version, string redoneText)
    {
        if (!prepared)
            stages.Add(new StageItem(StageKind.Delivery, StageState.Pending, "не доставлен"));
        else if (deliveredAt.HasValue && preparedAt.HasValue && deliveredAt < preparedAt)
            stages.Add(new StageItem(StageKind.Delivery, StageState.Pending, "устарела: " + redoneText));
        else if (deliveredAt.HasValue)
            stages.Add(new StageItem(StageKind.Delivery, StageState.Done, DeliveryLabel(meta) + " · " + Stamp(deliveredAt.Value)));
        else
            stages.Add(new StageItem(StageKind.Delivery, StageState.Pending, "не доставлен"));

        // Проверка — о том, что лежит на сервере: засчитана, если сделана после последней подготовки
        // и после последней доставки (доставить могли и не нами — тогда записи доставки нет).
        verified.TryGetValue(version, out var check);
        if (check == null)
            stages.Add(new StageItem(StageKind.Verify, StageState.Pending, "не проверялся"));
        else if (!prepared || (preparedAt.HasValue && check.At < preparedAt))
            stages.Add(new StageItem(StageKind.Verify, StageState.Pending, "устарела: " + redoneText));
        else if (deliveredAt.HasValue && check.At < deliveredAt)
            stages.Add(new StageItem(StageKind.Verify, StageState.Pending, "устарела: доставлено заново"));
        else
            stages.Add(check.Succeeded
                ? new StageItem(StageKind.Verify, StageState.Done, "прошла " + Stamp(check.At))
                : new StageItem(StageKind.Verify, StageState.Failed, "не прошла " + Stamp(check.At) + "\n" + FirstLine(check.Details),
                    "не прошла " + Stamp(check.At) + "\n" + check.Details, check.Details.Trim()));
    }

    private static string DeliveryLabel(ProjectMeta meta) =>
        meta.Delivery.Method == Core.Delivery.DeliveryMethods.Ftp ? "FTP" : "папка";

    public static string Stamp(DateTime at) => at.ToString("dd.MM.yyyy HH:mm");

    // Порядок версий — по числам (1.0.10 после 1.0.9); нечисловые — в конец.
    private static Version VersionKey(string version) =>
        System.Version.TryParse(version, out var v) ? v : new Version(int.MaxValue, 0);

    private static int IndexOf(IReadOnlyList<StageItem> stages, StageItem stage)
    {
        for (int i = 0; i < stages.Count; i++)
            if (ReferenceEquals(stages[i], stage))
                return i;
        return -1;
    }

    private static string FirstLine(string text)
    {
        var line = (text ?? "").Trim().Split('\n')[0].Trim();
        return line.Length > 90 ? line.Substring(0, 90) + "…" : line;
    }
}
