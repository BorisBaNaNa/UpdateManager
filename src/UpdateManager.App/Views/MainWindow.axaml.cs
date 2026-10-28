using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using UpdateManager.App.ViewModels;

namespace UpdateManager.App.Views;

public partial class MainWindow : Window
{
    // Свайп полосы платформ: перетаскивание мышью/пальцем и горизонтальная прокрутка тачпада.
    private const double DragThreshold = 60;
    private const double WheelThreshold = 1.0;
    private static readonly TimeSpan WheelCooldown = TimeSpan.FromMilliseconds(450);

    private Point? _dragStart;
    private double _wheelX;
    private DateTime _lastWheelShift = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();

        // Туннелем и с обработанными: нажатие начинается на блоке-кнопке, а свайп не должен его нажать.
        TrackCarousel.AddHandler(PointerPressedEvent, OnSwipePressed, RoutingStrategies.Tunnel, true);
        TrackCarousel.AddHandler(PointerReleasedEvent, OnSwipeReleased, RoutingStrategies.Tunnel, true);
        TrackCarousel.AddHandler(PointerWheelChangedEvent, OnSwipeWheel, RoutingStrategies.Tunnel, true);
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private bool CanSwipe => Vm?.SelectedRelease?.IsMultiPlatform == true;

    private void OnSwipePressed(object? sender, PointerPressedEventArgs e)
    {
        _dragStart = CanSwipe && e.GetCurrentPoint(TrackCarousel).Properties.IsLeftButtonPressed
            ? e.GetPosition(TrackCarousel)
            : null;
    }

    private void OnSwipeReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragStart is not { } start)
            return;
        _dragStart = null;

        var delta = e.GetPosition(TrackCarousel) - start;
        if (Math.Abs(delta.X) < DragThreshold || Math.Abs(delta.X) < 1.5 * Math.Abs(delta.Y))
            return;

        // Влево — следующая платформа въезжает справа; вправо — назад. Отпускание блоку не достаётся.
        Vm?.ShiftTrack(delta.X < 0 ? 1 : -1);
        e.Handled = true;
    }

    private void OnSwipeWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!CanSwipe || Math.Abs(e.Delta.X) <= Math.Abs(e.Delta.Y))
            return;

        e.Handled = true;
        if (DateTime.Now - _lastWheelShift < WheelCooldown)
            return; // один жест — одно переключение

        _wheelX += e.Delta.X;
        if (Math.Abs(_wheelX) < WheelThreshold)
            return;

        // Пальцы влево (прокрутка вправо, Delta.X < 0) — следующая платформа.
        Vm?.ShiftTrack(_wheelX < 0 ? 1 : -1);
        _wheelX = 0;
        _lastWheelShift = DateTime.Now;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ExitRequested += (_, _) => Close();
            vm.SelfUpdate.ShutdownRequested += (_, _) => Close();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Окно впервые показано — время фоновой проверки обновлений.
        (DataContext as MainWindowViewModel)?.SelfUpdate.OnStartup();
    }
}
