using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfPen = System.Windows.Media.Pen;

namespace Com.MiniScreenNotes.Models.Annotations;

public sealed class StepNumberAnnotation : IAnnotationElement
{
    private readonly WpfBrush _fill;
    private readonly WpfBrush _borderBrush;
    private readonly WpfPen _borderPen;
    private readonly FormattedText _text;

    public StepNumberAnnotation(string monitorId, Point center, int number, WpfColor color, double radius)
    {
        MonitorId = monitorId;
        Center = center;
        Number = number;
        Color = color;
        Radius = Math.Max(14.0, radius);

        _fill = new SolidColorBrush(Color);
        _fill.Freeze();
        _borderBrush = new SolidColorBrush(WpfColor.FromRgb(255, 255, 255));
        _borderBrush.Freeze();
        _borderPen = new WpfPen(_borderBrush, 2.5);
        _borderPen.Freeze();

        var fontSize = Number >= 100 ? Radius * 0.85 : Radius * 1.1;
        _text = new FormattedText(
            Number.ToString(CultureInfo.InvariantCulture),
            CultureInfo.CurrentUICulture,
            WpfFlowDirection.LeftToRight,
            new Typeface(new WpfFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            fontSize,
            _borderBrush,
            1.0);
    }

    public string MonitorId { get; }
    public Point Center { get; }
    public int Number { get; }
    public WpfColor Color { get; }
    public double Radius { get; }

    public void Render(DrawingContext dc)
    {
        dc.DrawEllipse(_fill, _borderPen, Center, Radius, Radius);

        var origin = new Point(
            Center.X - (_text.Width / 2.0),
            Center.Y - (_text.Height / 2.0));
        dc.DrawText(_text, origin);
    }

    public bool HitTest(Point point, double tolerance)
    {
        var r = Radius + tolerance;
        var dx = point.X - Center.X;
        var dy = point.Y - Center.Y;
        return (dx * dx) + (dy * dy) <= r * r;
    }
}
