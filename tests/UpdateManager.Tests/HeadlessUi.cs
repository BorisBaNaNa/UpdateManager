using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace UpdateManager.Tests;

/// <summary>
/// Настоящие окна без экрана (Avalonia.Headless): для поведения, которое живёт в привязках и контролах,
/// а не в VM. Одна сессия на процесс — всё выполняется на её UI-потоке.
/// </summary>
public static class HeadlessUi
{
    private static readonly Lazy<HeadlessUnitTestSession> Session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(HeadlessUi)));

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<UpdateManager.App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public static Task Run(Func<Task> action) =>
        Session.Value.Dispatch(action, CancellationToken.None);

    /// <summary>Довести очередь UI-потока: привязки, раскладка.</summary>
    public static void Flush() => Dispatcher.UIThread.RunJobs();

    /// <summary>Клик мышью по центру видимой кнопки с этим DataContext; ждёт, пока отработает команда.</summary>
    public static async Task Click(Window window, object dataContext)
    {
        Flush();
        var button = window.GetVisualDescendants().OfType<Button>()
            .First(b => ReferenceEquals(b.DataContext, dataContext) && b.IsEffectivelyVisible);
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(10);
            Flush();
        }
    }
}
