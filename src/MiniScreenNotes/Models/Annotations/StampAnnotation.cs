using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace Com.MiniScreenNotes.Models.Annotations;

public sealed class StampAnnotation : IAnnotationElement
{
    public StampAnnotation(string monitorId, bool isCheckMark, Point center, double size)
    {
        MonitorId = monitorId;
        IsCheckMark = isCheckMark;
        Center = center;
        Size = Math.Max(24.0, size);
    }

    public string MonitorId { get; }
    public bool IsCheckMark { get; }
    public Point Center { get; set; }
    public double Size { get; set; }

    public void Render(DrawingContext dc)
    {
        var half = Size / 2.0;
        var thickness = Math.Max(4.0, Size * 0.16);
        var color = IsCheckMark
            ? WpfColor.FromRgb(34, 197, 94)  // 綠色打勾
            : WpfColor.FromRgb(239, 68, 68); // 紅色打叉

        WpfBrush brush = new SolidColorBrush(color);
        brush.Freeze();

        // 白色外描邊讓符號在深淺背景都清晰
        WpfBrush outlineBrush = new SolidColorBrush(WpfColor.FromArgb(220, 255, 255, 255));
        outlineBrush.Freeze();

        var outlinePen = new WpfPen(outlineBrush, thickness + 3.0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        outlinePen.Freeze();

        var pen = new WpfPen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        if (IsCheckMark)
        {
            var p1 = new Point(Center.X - (half * 0.72), Center.Y + (half * 0.05));
            var p2 = new Point(Center.X - (half * 0.18), Center.Y + (half * 0.62));
            var p3 = new Point(Center.X + (half * 0.78), Center.Y - (half * 0.58));

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(p1, false, false);
                ctx.LineTo(p2, true, false);
                ctx.LineTo(p3, true, false);
            }
            geo.Freeze();

            dc.DrawGeometry(null, outlinePen, geo);
            dc.DrawGeometry(null, pen, geo);
        }
        else
        {
            var d = half * 0.65;
            var a1 = new Point(Center.X - d, Center.Y - d);
            var a2 = new Point(Center.X + d, Center.Y + d);
            var b1 = new Point(Center.X + d, Center.Y - d);
            var b2 = new Point(Center.X - d, Center.Y + d);

            dc.DrawLine(outlinePen, a1, a2);
            dc.DrawLine(outlinePen, b1, b2);
            dc.DrawLine(pen, a1, a2);
            dc.DrawLine(pen, b1, b2);
        }
    }

    public bool HitTest(Point point, double tolerance)
    {
        var half = (Size / 2.0) + tolerance;
        var rect = new Rect(Center.X - half, Center.Y - half, half * 2.0, half * 2.0);
        return rect.Contains(point);
    }
}
