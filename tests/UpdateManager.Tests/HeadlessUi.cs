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
        var center = CenterOf<Button>(window, dataContext);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        await Wait(TimeSpan.FromMilliseconds(200));
    }

    /// <summary>Центр (в координатах окна) видимого контрола с этим DataContext: по умолчанию — записи списка.</summary>
    public static Point CenterOf(Window window, object dataContext) => CenterOf<ListBoxItem>(window, dataContext);

    public static Point CenterOf<T>(Window window, object dataContext) where T : Control
    {
        Flush();
        var control = window.GetVisualDescendants().OfType<T>()
            .First(c => ReferenceEquals(c.DataContext, dataContext) && c.IsEffectivelyVisible);
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
    }

    /// <summary>Подождать настоящее время, прокручивая UI-поток (таймеры, команды).</summary>
    public static async Task Wait(TimeSpan time)
    {
        var until = DateTime.Now + time;
        while (DateTime.Now < until)
        {
            await Task.Delay(20);
            Flush();
        }
        Flush();
    }
}
