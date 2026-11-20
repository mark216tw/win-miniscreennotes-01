using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace Com.MiniScreenNotes.Models.Annotations;

public sealed class TextAnnotation : IAnnotationElement
{
    private readonly FormattedText? _formatted;
    private readonly FormattedText? _shadow;

    public TextAnnotation(string monitorId, Point position, string text, WpfColor color, double fontSize)
    {
        MonitorId = monitorId;
        Position = position;
        Text = text;
        Color = color;
        FontSize = Math.Max(14.0, fontSize);

        if (!string.IsNullOrWhiteSpace(Text))
        {
            _formatted = BuildFormattedText(Color);
            _shadow = BuildFormattedText(WpfColor.FromArgb(160, 0, 0, 0));
        }
    }

    public string MonitorId { get; }
    public Point Position { get; }
    public string Text { get; }
    public WpfColor Color { get; }
    public double FontSize { get; }

    public void Render(DrawingContext dc)
    {
        if (_formatted is null || _shadow is null)
        {
            return;
        }

        dc.DrawText(_shadow, new Point(Position.X + 1.5, Position.Y + 1.5));
        dc.DrawText(_formatted, Position);
    }

    public bool HitTest(Point point, double tolerance)
    {
        var formatted = _formatted ?? BuildFormattedText(Color);
        var bounds = new Rect(
            Position.X - tolerance,
            Position.Y - tolerance,
            Math.Max(20.0, formatted.Width) + (tolerance * 2.0),
            Math.Max(20.0, formatted.Height) + (tolerance * 2.0));
        return bounds.Contains(point);
    }

    private FormattedText BuildFormattedText(WpfColor color)
    {
        WpfBrush brush = new SolidColorBrush(color);
        brush.Freeze();
        return new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            WpfFlowDirection.LeftToRight,
            new Typeface(new WpfFontFamily("Microsoft JhengHei UI, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            FontSize,
            brush,
            1.0);
    }
}
