using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.Project;

namespace UpdateManager.App.ViewModels;

/// <summary>Этап выпуска. Набор этапов зависит от платформы (Android — без патча, с файлами версии).</summary>
public enum StageKind { Build, Patch, Files, Delivery, Verify }

public enum StageState { Pending, Done, Failed }

/// <summary>Один блок полосы этапов: что за этап, пройден ли и короткая подпись к нему.</summary>
public sealed class StageItem
{
    public StageItem(StageKind kind, StageState state, string detail, string fullDetail = "")
    {
        Kind = kind;
        State = state;
        Detail = detail;
        FullDetail = fullDetail.Length > 0 ? fullDetail : detail;
    }

    public StageKind Kind { get; }
    public StageState State { get; }

    /// <summary>Короткая подпись в блоке: дата или причина в одну строку.</summary>
    public string Detail { get; }

    /// <summary>Полный текст для подсказки (например, вся причина провала проверки).</summary>
    public string FullDetail { get; }

    public string Title => Kind switch
    {
        StageKind.Build => "Билд",
        StageKind.Patch => "Патч",
        StageKind.Files => "Файлы",
        StageKind.Delivery => "Доставка",
        _ => "Проверка"
    };

    public string ToolTipText => Title + ": " + FullDetail;

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
public sealed class PlatformTrack
{
    public PlatformTrack(string platform, IReadOnlyList<StageItem> stages)
    {
        Platform = platform;
        Stages = stages;
    }

    public string Platform { get; }
    public IReadOnlyList<StageItem> Stages { get; }

    /// <summary>Первый непройденный этап (null — выпуск по этой платформе завершён).</summary>
    public StageItem? NextStage => Stages.FirstOrDefault(s => s.State != StageState.Done);
}

/// <summary>Запись каталога: выпуск (версия) со своим номером и полосами этапов по платформам.</summary>
public sealed class ReleaseItem
{
    public ReleaseItem(int number, string version, bool isNew, bool isLatest, IReadOnlyList<PlatformTrack> platforms)
    {
        Number = number;
        Version = version;
        IsNew = isNew;
        IsLatest = isLatest;
        Platforms = platforms;
    }

    public int Number { get; }
    public string Version { get; }

    /// <summary>Билд найден в папке-источнике, но ещё не собран в патч.</summary>
    public bool IsNew { get; }

    /// <summary>Самый новый выпуск — следующий шаг считается только для него.</summary>
    public bool IsLatest { get; }

    public IReadOnlyList<PlatformTrack> Platforms { get; }

    public string NumberText => IsNew ? "новый" : "№ " + Number;

    public string Kicker => string.Join(" · ", Platforms.Select(p => p.Platform)) +
        (IsNew ? " · новый билд" : " · выпуск " + Number);
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
    public string Summary => IsMissing ? "папка не найдена"
        : LastRelease == null ? "версий пока нет"
        : LastRelease.Version + " · " + string.Join(", ", LastRelease.Platforms.Select(p => p.Platform));
}

/// <summary>
/// Собирает каталог выпусков проекта из Versions/ и меты. Сейчас проект — одна Windows-платформа
/// (Билд → Патч → Доставка → Проверка); Android добавит свою полосу в тот же выпуск.
/// </summary>
public static class ReleaseCatalog
{
    public const string Windows = "Windows";

    /// <summary>
    /// Выпуски от нового к старому. <paramref name="newBuildVersion"/> — версия билда в папке-источнике,
    /// которой ещё нет в Versions/ (null — нет такой): становится первой записью «новый билд».
    /// </summary>
    public static List<ReleaseItem> Build(UpdateProject project, string? newBuildVersion, string newBuildDetail = "")
    {
        var meta = project.Meta;
        var delivered = DeliveredDates(project);
        var maxVersion = project.Versions.Count > 0 ? project.Versions[^1].Version : null;
        var result = new List<ReleaseItem>();

        for (int i = 0; i < project.Versions.Count; i++)
        {
            var v = project.Versions[i];
            var isMax = v.Version == maxVersion;
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

            // Доставка и проверка «устаревают», если после них пересобрали патч / заново доставили.
            DateTime? deliveredAt = delivered.TryGetValue(v.Version, out var d) ? d : null;
            if (deliveredAt.HasValue && patchedAt.HasValue && deliveredAt < patchedAt)
                stages.Add(new StageItem(StageKind.Delivery, StageState.Pending, "устарела: патч пересобран"));
            else if (deliveredAt.HasValue)
                stages.Add(new StageItem(StageKind.Delivery, StageState.Done, DeliveryLabel(meta) + " · " + Stamp(deliveredAt.Value)));
            else
                stages.Add(new StageItem(StageKind.Delivery, StageState.Pending, "не доставлен"));

            if (meta.VerifiedVersions.TryGetValue(v.Version, out var check) &&
                (!deliveredAt.HasValue || check.At >= deliveredAt))
            {
                stages.Add(check.Succeeded
                    ? new StageItem(StageKind.Verify, StageState.Done, "прошла " + Stamp(check.At))
                    : new StageItem(StageKind.Verify, StageState.Failed, "не прошла " + Stamp(check.At) + "\n" + FirstLine(check.Details),
                        "не прошла " + Stamp(check.At) + "\n" + check.Details));
            }
            else
            {
                stages.Add(new StageItem(StageKind.Verify, StageState.Pending,
                    check != null ? "устарела: доставлено заново" : "не проверялся"));
            }

            result.Add(new ReleaseItem(i + 1, v.Version, isNew: false, isLatest: false,
                new[] { new PlatformTrack(Windows, stages) }));
        }

        if (newBuildVersion != null)
        {
            var stages = new List<StageItem>
            {
                new(StageKind.Build, StageState.Done, newBuildDetail),
                new(StageKind.Patch, StageState.Pending, "не собран"),
                new(StageKind.Delivery, StageState.Pending, "не доставлен"),
                new(StageKind.Verify, StageState.Pending, "не проверялся")
            };
            result.Add(new ReleaseItem(project.Versions.Count + 1, newBuildVersion, isNew: true, isLatest: false,
                new[] { new PlatformTrack(Windows, stages) }));
        }

        result.Reverse();
        if (result.Count > 0)
            result[0] = new ReleaseItem(result[0].Number, result[0].Version, result[0].IsNew, isLatest: true, result[0].Platforms);
        return result;
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

    private static string DeliveryLabel(ProjectMeta meta) =>
        meta.Delivery.Method == Core.Delivery.DeliveryMethods.Ftp ? "FTP" : "папка";

    public static string Stamp(DateTime at) => at.ToString("dd.MM.yyyy HH:mm");

    private static string FirstLine(string text)
    {
        var line = (text ?? "").Trim().Split('\n')[0].Trim();
        return line.Length > 90 ? line.Substring(0, 90) + "…" : line;
    }
}
