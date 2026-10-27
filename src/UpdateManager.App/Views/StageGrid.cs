using Avalonia;
using Avalonia.Controls;

namespace UpdateManager.App.Views;

/// <summary>
/// Сетка блоков этапов: в ряд, пока каждому блоку хватает <see cref="MinCellWidth"/>, иначе по два в ряд.
/// Промежутки только между блоками — края полосы совпадают с краями колонки.
/// </summary>
public sealed class StageGrid : Panel
{
    public static readonly StyledProperty<double> MinCellWidthProperty =
        AvaloniaProperty.Register<StageGrid, double>(nameof(MinCellWidth), 150);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<StageGrid, double>(nameof(Spacing), 6);

    static StageGrid()
    {
        AffectsMeasure<StageGrid>(MinCellWidthProperty, SpacingProperty);
    }

    public double MinCellWidth
    {
        get => GetValue(MinCellWidthProperty);
        set => SetValue(MinCellWidthProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private int Columns(double width)
    {
        var count = Children.Count;
        if (count <= 2 || double.IsInfinity(width))
            return Math.Max(count, 1);
        return (width - Spacing * (count - 1)) / count >= MinCellWidth ? count : 2;
    }

    private double CellWidth(double width, int columns) =>
        Math.Max(0, (width - Spacing * (columns - 1)) / columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Columns(availableSize.Width);
        var cellWidth = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : CellWidth(availableSize.Width, columns);
        double rowHeight = 0, maxCell = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(cellWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            maxCell = Math.Max(maxCell, child.DesiredSize.Width);
        }

        var rows = (Children.Count + columns - 1) / columns;
        var width = double.IsInfinity(availableSize.Width) ? maxCell * columns + Spacing * (columns - 1) : availableSize.Width;
        return new Size(width, rows * rowHeight + Spacing * Math.Max(rows - 1, 0));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns(finalSize.Width);
        var cellWidth = CellWidth(finalSize.Width, columns);
        var rowHeight = Children.Count == 0 ? 0 : Children.Max(c => c.DesiredSize.Height);
        // Целые пиксели, последний столбец добирает остаток: иначе дробная ширина обрезает его правый контур.
        for (int i = 0; i < Children.Count; i++)
        {
            int row = i / columns, col = i % columns;
            var x = Math.Round(col * (cellWidth + Spacing));
            var width = col == columns - 1 ? Math.Floor(finalSize.Width) - x : Math.Round(cellWidth);
            Children[i].Arrange(new Rect(x, Math.Round(row * (rowHeight + Spacing)), width, rowHeight));
        }
        return finalSize;
    }
}
