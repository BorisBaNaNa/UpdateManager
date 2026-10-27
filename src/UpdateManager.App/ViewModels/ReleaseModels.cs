using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.Project;

namespace UpdateManager.App.ViewModels;

/// <summary>Этап выпуска. Набор этапов зависит от платформы (Android — без патча, с файлами версии).</summary>
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
        StageKind.Files => "Файлы",
        StageKind.Delivery => "Доставка",
        _ => "Проверка"
    };

    public string ToolTipText => Title + ": " + FullDetail;

    /// <summary>
    /// Блок можно нажать: только в последнем выпуске и только пройденный или следующий этап —
    /// повторить шаг (пересобрать, доставить снова) можно, перепрыгнуть через непройденный — нет.
    /// </summary>
    public bool IsActionable { get; internal set; }

    /// <summary>Что сделает нажатие на блок.</summary>
    public string ActionText => Kind switch
    {
        StageKind.Build => "Сменить папку билда",
        StageKind.Patch => State == StageState.Done ? "Пересобрать" : "Собрать",
        StageKind.Files => State == StageState.Done ? "Создать заново" : "Создать",
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

    /// <summary>Состояние словами — цвет полосы никогда не единственный носитель.</summary>
    public string StateText => NextStage switch
    {
        null => "проверен",
        { Kind: StageKind.Build } => "нет билда",
        { Kind: StageKind.Patch } => "патч не собран",
        { Kind: StageKind.Files } => "файлы не созданы",
        { Kind: StageKind.Delivery } => "не доставлен",
        { IsFailed: true } => "проверка не прошла",
        _ => "не проверялся"
    };
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

    /// <summary>Состояние выпуска словами; при нескольких платформах — по каждой.</summary>
    public string StateText => Platforms.Count == 1
        ? Platforms[0].StateText
        : string.Join(", ", Platforms.Select(p => p.Platform + ": " + p.StateText));

    /// <summary>Вторая строка записи каталога: «№ 5 · проверка не прошла».</summary>
    public string CatalogLine => NumberText + " · " + StateText;

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
    public string Summary => IsMissing ? "папка не найдена — нажмите, чтобы убрать из списка"
        : LastRelease == null ? "версий пока нет"
        : LastRelease.Version + " · " + LastRelease.StateText;
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

            // Этап не засчитан, если выполнен раньше предыдущего. Повтор этапа и так стирает записи
            // следующих (MainWindowViewModel.ResetStagesFrom); правила ниже держат и старые записи —
            // например, проверку, сделанную до пересборки патча.
            DateTime? deliveredAt = delivered.TryGetValue(v.Version, out var d) ? d : null;
            if (!patched)
                stages.Add(new StageItem(StageKind.Delivery, StageState.Pending, "не доставлен"));
            else if (deliveredAt.HasValue && patchedAt.HasValue && deliveredAt < patchedAt)
                stages.Add(new StageItem(StageKind.Delivery, StageState.Pending, "устарела: патч пересобран"));
            else if (deliveredAt.HasValue)
                stages.Add(new StageItem(StageKind.Delivery, StageState.Done, DeliveryLabel(meta) + " · " + Stamp(deliveredAt.Value)));
            else
                stages.Add(new StageItem(StageKind.Delivery, StageState.Pending, "не доставлен"));

            // Проверка — о том, что лежит на сервере: засчитана, если сделана после последней сборки
            // патча и после последней доставки (доставить могли и не нами — тогда записи доставки нет).
            meta.VerifiedVersions.TryGetValue(v.Version, out var check);
            if (check == null)
            {
                stages.Add(new StageItem(StageKind.Verify, StageState.Pending, "не проверялся"));
            }
            else if (!patched || (patchedAt.HasValue && check.At < patchedAt))
            {
                stages.Add(new StageItem(StageKind.Verify, StageState.Pending, "устарела: патч пересобран"));
            }
            else if (deliveredAt.HasValue && check.At < deliveredAt)
            {
                stages.Add(new StageItem(StageKind.Verify, StageState.Pending, "устарела: доставлено заново"));
            }
            else
            {
                stages.Add(check.Succeeded
                    ? new StageItem(StageKind.Verify, StageState.Done, "прошла " + Stamp(check.At))
                    : new StageItem(StageKind.Verify, StageState.Failed, "не прошла " + Stamp(check.At) + "\n" + FirstLine(check.Details),
                        "не прошла " + Stamp(check.At) + "\n" + check.Details, check.Details.Trim()));
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
        {
            result[0] = new ReleaseItem(result[0].Number, result[0].Version, result[0].IsNew, isLatest: true, result[0].Platforms);
            foreach (var track in result[0].Platforms)
            {
                // Пройденные этапы и первый непройденный; дальше — заперто, пока не пройден предыдущий.
                var next = track.NextStage;
                var limit = next == null ? track.Stages.Count - 1 : IndexOf(track.Stages, next);
                for (int i = 0; i <= limit; i++)
                    track.Stages[i].IsActionable = true;
            }
        }
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
