using Avalonia;
using Avalonia.Controls.Primitives;

namespace UpdateManager.App.Views;

/// <summary>
/// Сетка блоков этапов: в ряд, пока каждому блоку хватает <see cref="MinCellWidth"/>, иначе по два в ряд.
/// </summary>
public sealed class StageGrid : UniformGrid
{
    public static readonly StyledProperty<double> MinCellWidthProperty =
        AvaloniaProperty.Register<StageGrid, double>(nameof(MinCellWidth), 150);

    public double MinCellWidth
    {
        get => GetValue(MinCellWidthProperty);
        set => SetValue(MinCellWidthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        var fits = double.IsInfinity(availableSize.Width) || availableSize.Width / Math.Max(count, 1) >= MinCellWidth;
        Columns = fits || count <= 2 ? count : 2;
        Rows = 0;
        return base.MeasureOverride(availableSize);
    }
}
