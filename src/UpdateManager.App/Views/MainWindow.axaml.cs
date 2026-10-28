using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

        CatalogList.AddHandler(PointerPressedEvent, OnCatalogPressed, RoutingStrategies.Tunnel, true);
        CatalogList.AddHandler(PointerMovedEvent, OnCatalogMoved, RoutingStrategies.Tunnel, true);
        CatalogList.AddHandler(PointerReleasedEvent, OnCatalogReleased, RoutingStrategies.Tunnel, true);
        CatalogList.AddHandler(PointerCaptureLostEvent, OnCatalogCaptureLost, RoutingStrategies.Direct, true);
        _holdTimer.Tick += OnHoldElapsed;
        _edgeTimer.Tick += OnEdgeScroll;
        MarkedMenu.Closed += (_, _) => Dispatcher.UIThread.Post(() => Vm?.EndMarking()); // после команды пункта, не до
    }

    // --- Каталог: выделение областью. Удержание записи 1 с (сдвиг раньше — не удержание) задаёт точку
    // начала; протяжка выделяет от неё до записи под указателем, у краёв список прокручивается;
    // отпускание — меню выделенного, закрыли мимо — выделение спадает. ---

    private const double HoldSlop = 8;
    private const double EdgeZone = 28;
    private const double EdgeStep = 14;
    private readonly DispatcherTimer _holdTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _edgeTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private Point _holdStart;
    private Point _lastPosition;
    private IPointer? _holdPointer;
    private ReleaseItem? _holdItem;
    private bool _dragMarking;

    private ContextMenu MarkedMenu => (ContextMenu)CatalogList.Resources["MarkedMenu"]!;

    private void OnCatalogPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(CatalogList).Properties.IsLeftButtonPressed)
            return;
        _holdStart = _lastPosition = e.GetPosition(CatalogList);
        _holdPointer = e.Pointer;
        _holdItem = ReleaseAt(_holdStart);
        if (_holdItem != null)
            _holdTimer.Start();
    }

    private void OnHoldElapsed(object? sender, EventArgs e)
    {
        _holdTimer.Stop();
        if (_holdItem == null || Vm == null)
            return;
        Vm.StartMarking(_holdItem);
        _dragMarking = true;
        _holdPointer?.Capture(CatalogList); // протяжка за пределы списка не теряет ни движение, ни отпускание
    }

    private void OnCatalogMoved(object? sender, PointerEventArgs e)
    {
        _lastPosition = e.GetPosition(CatalogList);
        if (_holdTimer.IsEnabled)
        {
            if (Math.Abs(_lastPosition.X - _holdStart.X) > HoldSlop || Math.Abs(_lastPosition.Y - _holdStart.Y) > HoldSlop)
                _holdTimer.Stop(); // сдвинули раньше секунды — это не удержание
            return;
        }
        if (!_dragMarking)
            return;

        MarkUnderPointer();
        var nearEdge = _lastPosition.Y < EdgeZone || _lastPosition.Y > CatalogList.Bounds.Height - EdgeZone;
        if (nearEdge && !_edgeTimer.IsEnabled)
            _edgeTimer.Start();
        else if (!nearEdge)
            _edgeTimer.Stop();
    }

    // Указатель у края списка — прокручиваем и продолжаем выделение до записи, что въехала под него.
    private void OnEdgeScroll(object? sender, EventArgs e)
    {
        var scroll = CatalogList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (!_dragMarking || scroll == null)
        {
            _edgeTimer.Stop();
            return;
        }
        var up = _lastPosition.Y < EdgeZone;
        var depth = up ? EdgeZone - _lastPosition.Y : _lastPosition.Y - (CatalogList.Bounds.Height - EdgeZone);
        var step = EdgeStep * Math.Clamp(depth / EdgeZone, 0.3, 3); // чем дальше за край, тем быстрее
        var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        scroll.Offset = new Vector(scroll.Offset.X, Math.Clamp(scroll.Offset.Y + (up ? -step : step), 0, max));
        MarkUnderPointer();
    }

    // Запись под указателем; за верхним/нижним краем — крайняя видимая (держим указатель в пределах списка).
    private void MarkUnderPointer()
    {
        var y = Math.Clamp(_lastPosition.Y, 1, Math.Max(1, CatalogList.Bounds.Height - 1));
        var x = Math.Clamp(_lastPosition.X, 1, Math.Max(1, CatalogList.Bounds.Width - 1));
        if (ReleaseAt(new Point(x, y)) is { } item)
            Vm?.MarkTo(item);
    }

    private void OnCatalogReleased(object? sender, PointerReleasedEventArgs e)
    {
        _holdTimer.Stop();
        _edgeTimer.Stop();
        if (!_dragMarking)
            return;

        _dragMarking = false;
        _holdPointer?.Capture(null);
        _holdPointer = null;
        e.Handled = true; // отпускание после выделения — не клик по записи
        if (Vm?.MarkedCount > 0)
        {
            MarkedMenu.DataContext = Vm;
            MarkedMenu.Open(CatalogList);
        }
        else
            Vm?.EndMarking();
    }

    private void OnCatalogCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_dragMarking)
            return;
        _dragMarking = false; // захват отняли (окно потеряло фокус) — выделение не доведено
        _edgeTimer.Stop();
        Vm?.EndMarking();
    }

    private ReleaseItem? ReleaseAt(Point position) =>
        (CatalogList.InputHitTest(position) as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as ReleaseItem;

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
