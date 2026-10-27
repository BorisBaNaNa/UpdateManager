using Avalonia;
using Avalonia.Media;

namespace UpdateManager.App;

internal static class Program
{
    /// <summary>Шрифт интерфейса — Golos Text (кириллица, OFL), лежит в Assets/Fonts.</summary>
    public const string FontFamily = "avares://UpdateManager/Assets/Fonts#Golos Text";

    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Используется и дизайнером превью.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions { DefaultFamilyName = FontFamily })
            .LogToTrace();
}
