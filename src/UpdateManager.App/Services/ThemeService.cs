using Avalonia;
using Avalonia.Styling;

namespace UpdateManager.App.Services;

/// <summary>Режим темы. Имена совпадают с форматом theme.txt из 1.x.</summary>
public enum AppTheme { System, Light, Dark }

/// <summary>
/// Тема приложения: Fluent + RequestedThemeVariant. Режим хранится в профиле пользователя
/// (%AppData%\UpdateManager\theme.txt — тот же файл, что у 1.x). System = как в Windows
/// (Avalonia сама следит за системной темой).
/// </summary>
public sealed class ThemeService
{
    private readonly string _filePath;

    public ThemeService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UpdateManager", "theme.txt"))
    {
    }

    public ThemeService(string filePath)
    {
        _filePath = filePath;
        Mode = Load();
    }

    public AppTheme Mode { get; private set; }

    /// <summary>Применить текущий режим к приложению (вызвать при старте).</summary>
    public void Apply()
    {
        if (Application.Current == null)
            return;

        Application.Current.RequestedThemeVariant = Mode switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    public void SetMode(AppTheme mode)
    {
        Mode = mode;
        Save();
        Apply();
    }

    private AppTheme Load()
    {
        try
        {
            if (File.Exists(_filePath) && Enum.TryParse(File.ReadAllText(_filePath).Trim(), out AppTheme mode))
                return mode;
        }
        catch
        {
            // нечитаемый файл темы — не повод падать, берём режим по умолчанию
        }
        return AppTheme.System;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, Mode.ToString());
        }
        catch
        {
            // не сохранилась тема — не критично
        }
    }
}
