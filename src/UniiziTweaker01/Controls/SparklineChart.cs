using System.Windows;
using System.Windows.Media;

namespace UniiziTweaker01.Controls;

/// <summary>Lightweight line/area chart for live history (right-aligned, fixed window).</summary>
public sealed class SparklineChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(SparklineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(SparklineChart),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(SparklineChart),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(SparklineChart),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 2 || h <= 2) return;

        var gridBrush = new SolidColorBrush(Color.FromArgb(48, 132, 147, 170));
        gridBrush.Freeze();
        var gridPen = new Pen(gridBrush, 1);
        gridPen.Freeze();
        for (int i = 1; i < 4; i++)
        {
            double gy = Math.Round(h * i / 4) + 0.5;
            dc.DrawLine(gridPen, new Point(0, gy), new Point(w, gy));
        }

        var values = Values;
        if (values == null || values.Count < 2) return;

        double max = Maximum <= 0 ? 100 : Maximum;
        int capacity = Math.Max(2, Capacity);
        double step = w / (capacity - 1);
        int n = values.Count;
        var points = new List<Point>(n);
        for (int i = 0; i < n; i++)
        {
            double x = w - (n - 1 - i) * step;
            double v = Math.Clamp(values[i], 0, max);
            double y = h - (v / max) * (h - 2) - 1;
            points.Add(new Point(x, y));
        }

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, h), true, true);
            foreach (var p in points) ctx.LineTo(p, true, false);
            ctx.LineTo(new Point(points[^1].X, h), true, false);
        }
        area.Freeze();

        var fill = Stroke.CloneCurrentValue();
        fill.Opacity = 0.18;
        dc.DrawGeometry(fill, null, area);

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
        }
        line.Freeze();
        var pen = new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round };
        dc.DrawGeometry(null, pen, line);
    }
}
