using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace Com.MiniScreenNotes.Models.Annotations;

public sealed class FreehandStroke : IAnnotationElement
{
    private readonly List<Point> _points = [];
    private readonly WpfBrush _brush;
    private readonly WpfPen _pen;
    private readonly double _renderWidth;
    private StreamGeometry? _cachedGeometry;

    public FreehandStroke(string monitorId, WpfColor color, double strokeWidth, bool isHighlighter)
    {
        MonitorId = monitorId;
        Color = color;
        StrokeWidth = strokeWidth;
        IsHighlighter = isHighlighter;

        _renderWidth = IsHighlighter ? Math.Max(10.0, StrokeWidth * 3.5) : StrokeWidth;
        var renderColor = IsHighlighter
            ? WpfColor.FromArgb(115, Color.R, Color.G, Color.B)
            : Color;

        _brush = new SolidColorBrush(renderColor);
        _brush.Freeze();
        _pen = new WpfPen(_brush, _renderWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        _pen.Freeze();
    }

    public string MonitorId { get; }
    public WpfColor Color { get; }
    public double StrokeWidth { get; }
    public bool IsHighlighter { get; }
    public IReadOnlyList<Point> Points => _points;

    public void AddPoint(Point point)
    {
        if (_points.Count > 0)
        {
            var last = _points[^1];
            var dx = point.X - last.X;
            var dy = point.Y - last.Y;
            if ((dx * dx) + (dy * dy) < 4.0)
            {
                return;
            }
        }

        _points.Add(point);
        _cachedGeometry = null;
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count == 0)
        {
            return;
        }

        if (_points.Count == 1)
        {
            dc.DrawEllipse(_brush, null, _points[0], _renderWidth / 2.0, _renderWidth / 2.0);
            return;
        }

        var geometry = _cachedGeometry ?? BuildSmoothGeometry();
        _cachedGeometry = geometry;

        dc.DrawGeometry(null, _pen, geometry);
    }

    public bool HitTest(Point point, double tolerance)
    {
        var hitRadius = ((IsHighlighter ? Math.Max(10.0, StrokeWidth * 3.5) : StrokeWidth) / 2.0) + tolerance;
        var hitRadiusSq = hitRadius * hitRadius;

        if (_points.Count == 1)
        {
            return DistanceSquared(point, _points[0]) <= hitRadiusSq;
        }

        for (var i = 0; i < _points.Count - 1; i++)
        {
            if (DistanceToSegmentSquared(point, _points[i], _points[i + 1]) <= hitRadiusSq)
            {
                return true;
            }
        }

        return false;
    }

    private StreamGeometry BuildSmoothGeometry()
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(_points[0], false, false);

        if (_points.Count == 2)
        {
            ctx.LineTo(_points[1], true, false);
        }
        else
        {
            // Quadratic Bezier 中點插值平滑化
            for (var i = 1; i < _points.Count - 1; i++)
            {
                var mid = new Point(
                    (_points[i].X + _points[i + 1].X) / 2.0,
                    (_points[i].Y + _points[i + 1].Y) / 2.0);
                ctx.QuadraticBezierTo(_points[i], mid, true, false);
            }

            ctx.LineTo(_points[^1], true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private static double DistanceToSegmentSquared(Point p, Point a, Point b)
    {
        var l2 = DistanceSquared(a, b);
        if (l2 == 0)
        {
            return DistanceSquared(p, a);
        }

        var t = Math.Clamp((((p.X - a.X) * (b.X - a.X)) + ((p.Y - a.Y) * (b.Y - a.Y))) / l2, 0.0, 1.0);
        var proj = new Point(a.X + (t * (b.X - a.X)), a.Y + (t * (b.Y - a.Y)));
        return DistanceSquared(p, proj);
    }
}
