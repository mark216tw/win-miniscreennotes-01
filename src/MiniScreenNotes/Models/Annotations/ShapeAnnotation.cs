using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace Com.MiniScreenNotes.Models.Annotations;

public sealed class ShapeAnnotation : IAnnotationElement
{
    private readonly WpfBrush _brush;
    private readonly WpfPen _pen;

    public ShapeAnnotation(
        string monitorId,
        ToolType shapeTool,
        Point startPoint,
        Point endPoint,
        WpfColor color,
        double strokeWidth)
    {
        MonitorId = monitorId;
        ShapeTool = shapeTool;
        StartPoint = startPoint;
        EndPoint = endPoint;
        Color = color;
        StrokeWidth = strokeWidth;

        _brush = new SolidColorBrush(Color);
        _brush.Freeze();
        _pen = new WpfPen(_brush, StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        _pen.Freeze();
    }

    public string MonitorId { get; }
    public ToolType ShapeTool { get; }
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    public WpfColor Color { get; }
    public double StrokeWidth { get; }

    public void Render(DrawingContext dc)
    {
        var rect = new Rect(StartPoint, EndPoint);

        switch (ShapeTool)
        {
            case ToolType.Line:
                dc.DrawLine(_pen, StartPoint, EndPoint);
                break;

            case ToolType.Arrow:
                DrawArrow(dc, _pen, _brush, StartPoint, EndPoint);
                break;

            case ToolType.HollowRectangle:
                dc.DrawRectangle(null, _pen, rect);
                break;

            case ToolType.FilledRectangle:
                dc.DrawRectangle(_brush, null, rect);
                break;

            case ToolType.HollowEllipse:
                DrawEllipse(dc, null, _pen, rect);
                break;

            case ToolType.FilledEllipse:
                DrawEllipse(dc, _brush, null, rect);
                break;
        }
    }

    public bool HitTest(Point point, double tolerance)
    {
        var pad = (StrokeWidth / 2.0) + tolerance;
        var rect = new Rect(StartPoint, EndPoint);

        return ShapeTool switch
        {
            ToolType.Line or ToolType.Arrow => DistanceToSegment(point, StartPoint, EndPoint) <= pad,
            ToolType.FilledRectangle => Inflate(rect, tolerance).Contains(point),
            ToolType.HollowRectangle => HitTestHollowRect(point, rect, pad),
            ToolType.FilledEllipse => HitTestEllipse(point, rect, tolerance, filled: true),
            ToolType.HollowEllipse => HitTestEllipse(point, rect, pad, filled: false),
            _ => false
        };
    }

    private void DrawArrow(DrawingContext dc, WpfPen pen, WpfBrush brush, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1.0)
        {
            return;
        }

        var headLength = Math.Clamp(StrokeWidth * 4.2, 14.0, Math.Max(14.0, length * 0.45));
        var headWidth = headLength * 0.65;
        var angle = Math.Atan2(dy, dx);

        var shaftEnd = new Point(
            end.X - (Math.Cos(angle) * headLength * 0.72),
            end.Y - (Math.Sin(angle) * headLength * 0.72));
        dc.DrawLine(pen, start, shaftEnd);

        var left = new Point(
            end.X - (headLength * Math.Cos(angle)) + (headWidth * 0.5 * Math.Sin(angle)),
            end.Y - (headLength * Math.Sin(angle)) - (headWidth * 0.5 * Math.Cos(angle)));
        var right = new Point(
            end.X - (headLength * Math.Cos(angle)) - (headWidth * 0.5 * Math.Sin(angle)),
            end.Y - (headLength * Math.Sin(angle)) + (headWidth * 0.5 * Math.Cos(angle)));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(end, true, true);
            ctx.LineTo(left, true, false);
            ctx.LineTo(right, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);
    }

    private static void DrawEllipse(DrawingContext dc, WpfBrush? fill, WpfPen? pen, Rect rect)
    {
        var center = new Point(rect.X + (rect.Width / 2.0), rect.Y + (rect.Height / 2.0));
        dc.DrawEllipse(fill, pen, center, rect.Width / 2.0, rect.Height / 2.0);
    }

    private static Rect Inflate(Rect rect, double amount)
    {
        var copy = rect;
        copy.Inflate(amount, amount);
        return copy;
    }

    private static bool HitTestHollowRect(Point p, Rect rect, double pad)
    {
        var outer = Inflate(rect, pad);
        if (!outer.Contains(p))
        {
            return false;
        }

        if (rect.Width <= pad * 2 || rect.Height <= pad * 2)
        {
            return true;
        }

        var inner = rect;
        inner.Inflate(-pad, -pad);
        return !inner.Contains(p);
    }

    private static bool HitTestEllipse(Point p, Rect rect, double pad, bool filled)
    {
        var rx = Math.Max(1.0, rect.Width / 2.0);
        var ry = Math.Max(1.0, rect.Height / 2.0);
        var cx = rect.X + rx;
        var cy = rect.Y + ry;

        var nxOuter = (p.X - cx) / (rx + pad);
        var nyOuter = (p.Y - cy) / (ry + pad);
        if ((nxOuter * nxOuter) + (nyOuter * nyOuter) > 1.0)
        {
            return false;
        }

        if (filled || rx <= pad || ry <= pad)
        {
            return true;
        }

        var nxInner = (p.X - cx) / (rx - pad);
        var nyInner = (p.Y - cy) / (ry - pad);
        return (nxInner * nxInner) + (nyInner * nyInner) >= 1.0;
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var l2 = (dx * dx) + (dy * dy);
        if (l2 == 0)
        {
            return (p - a).Length;
        }

        var t = Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / l2, 0.0, 1.0);
        var proj = new Point(a.X + (t * dx), a.Y + (t * dy));
        return (p - proj).Length;
    }
}
