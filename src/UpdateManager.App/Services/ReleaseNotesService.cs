using System.Reflection;

namespace UpdateManager.App.Services;

/// <summary>Заметки одного выпуска: версия X.Y.Z и текст Markdown (тот же, что в GitHub Release).</summary>
public sealed record ReleaseNote(Version Version, string Markdown)
{
    public string VersionText => $"{Version.Major}.{Version.Minor}.{Version.Build}";
}

/// <summary>
/// Заметки выпусков, вшитые в программу (docs/releases/X.Y.Z.md → EmbeddedResource «ReleaseNotes.X.Y.Z.md»),
/// и «что нового» после обновления: последняя показанная версия хранится в профиле пользователя
/// (%AppData%\UpdateManager\whatsnew.txt). Нет файла — программа ещё ни разу не показывала заметки
/// (свежая установка или обновление с 2.0.0) — показываем всё до текущей версии.
/// </summary>
public sealed class ReleaseNotesService
{
    private const string ResourcePrefix = "ReleaseNotes.";

    private readonly string _seenFile;
    private readonly Func<IReadOnlyList<ReleaseNote>> _load;

    public ReleaseNotesService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UpdateManager", "whatsnew.txt"), LoadEmbedded)
    {
    }

    public ReleaseNotesService(string seenFile, Func<IReadOnlyList<ReleaseNote>> load)
    {
        _seenFile = seenFile;
        _load = load;
    }

    /// <summary>Все заметки, от новых к старым.</summary>
    public IReadOnlyList<ReleaseNote> All() => _load();

    /// <summary>
    /// Что показать при запуске версии <paramref name="current"/>: заметки новее последней показанной
    /// и не новее текущей. Пусто — эту версию уже показывали (или заметок нет).
    /// </summary>
    public IReadOnlyList<ReleaseNote> UnseenFor(string current)
    {
        var now = Normalize(current);
        if (now == null)
            return Array.Empty<ReleaseNote>();

        var seen = Normalize(ReadSeen());
        if (seen != null && seen >= now)
            return Array.Empty<ReleaseNote>();

        return All().Where(n => n.Version <= now && (seen == null || n.Version > seen)).ToList();
    }

    /// <summary>Запомнить, что заметки версии <paramref name="current"/> показаны.</summary>
    public void MarkSeen(string current)
    {
        var now = Normalize(current);
        if (now == null)
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_seenFile)!);
            File.WriteAllText(_seenFile, $"{now.Major}.{now.Minor}.{now.Build}");
        }
        catch
        {
            // не записали — покажем ещё раз при следующем запуске, не страшно
        }
    }

    private string? ReadSeen()
    {
        try
        {
            return File.Exists(_seenFile) ? File.ReadAllText(_seenFile).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>«2.0.1.0» / «2.0.1» → 2.0.1 (четвёртое число у нас всегда 0).</summary>
    public static Version? Normalize(string? text)
    {
        if (!Version.TryParse(text, out var v))
            return null;
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    private static IReadOnlyList<ReleaseNote> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var notes = new List<ReleaseNote>();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !name.EndsWith(".md", StringComparison.Ordinal))
                continue;

            var version = Normalize(name.Substring(ResourcePrefix.Length, name.Length - ResourcePrefix.Length - 3));
            if (version == null)
                continue;

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            notes.Add(new ReleaseNote(version, reader.ReadToEnd()));
        }
        return notes.OrderByDescending(n => n.Version).ToList();
    }
}
