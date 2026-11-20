using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace Com.MiniScreenNotes.Views.Controls;

public sealed class LaserOverlayLayer : FrameworkElement
{
    private const double TrailLifetimeSeconds = 1.2;
    private const int PaletteSteps = 32;

    private static readonly WpfBrush HaloBrush = CreateFrozenBrush(WpfColor.FromArgb(95, 239, 68, 68));
    private static readonly WpfBrush RedBrush = CreateFrozenBrush(WpfColor.FromRgb(239, 68, 68));
    private static readonly WpfBrush CoreBrush = CreateFrozenBrush(WpfColor.FromRgb(255, 255, 255));
    private static WpfPen[]? s_glowPens;
    private static WpfPen[]? s_corePens;

    private readonly List<(Point Position, DateTime Timestamp)> _trail = [];
    private Point? _currentPointer;
    private bool _isActive;
    private bool _isRenderingHooked;

    public LaserOverlayLayer()
    {
        IsHitTestVisible = false;
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            if (_isActive)
            {
                HookRendering();
            }
            else
            {
                _trail.Clear();
                _currentPointer = null;
                UnhookRendering();
                InvalidateVisual();
            }
        }
    }

    public void UpdatePointer(Point position, bool isDrawingTrail)
    {
        _currentPointer = position;
        if (isDrawingTrail)
        {
            _trail.Add((position, DateTime.UtcNow));
        }

        InvalidateVisual();
    }

    public void ClearPointer()
    {
        _currentPointer = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!_isActive)
        {
            return;
        }

        var now = DateTime.UtcNow;
        _trail.RemoveAll(p => (now - p.Timestamp).TotalSeconds > TrailLifetimeSeconds);

        // 繪製漸細漸透明的彗星拖尾
        if (_trail.Count > 1)
        {
            var (glowPens, corePens) = GetPenPalettes();
            for (var i = 1; i < _trail.Count; i++)
            {
                var prev = _trail[i - 1];
                var curr = _trail[i];
                var age = (now - curr.Timestamp).TotalSeconds;
                var lifeRatio = Math.Clamp(1.0 - (age / TrailLifetimeSeconds), 0.0, 1.0);
                var paletteIndex = Math.Clamp((int)Math.Round(lifeRatio * (PaletteSteps - 1)), 0, PaletteSteps - 1);

                dc.DrawLine(glowPens[paletteIndex], prev.Position, curr.Position);
                dc.DrawLine(corePens[paletteIndex], prev.Position, curr.Position);
            }
        }

        // 前端高亮雷射點
        if (_currentPointer is { } pt)
        {
            dc.DrawEllipse(HaloBrush, null, pt, 16, 16);
            dc.DrawEllipse(RedBrush, null, pt, 7, 7);
            dc.DrawEllipse(CoreBrush, null, pt, 3.2, 3.2);
        }
    }

    private static (WpfPen[] GlowPens, WpfPen[] CorePens) GetPenPalettes()
    {
        if (s_glowPens is not null && s_corePens is not null)
        {
            return (s_glowPens, s_corePens);
        }

        var glowPens = new WpfPen[PaletteSteps];
        var corePens = new WpfPen[PaletteSteps];

        for (var i = 0; i < PaletteSteps; i++)
        {
            var lifeRatio = (double)i / (PaletteSteps - 1);
            var glowAlpha = (byte)(120 * lifeRatio);
            var coreAlpha = (byte)(255 * lifeRatio);
            var width = 2.0 + (8.0 * lifeRatio);

            var glowBrush = new SolidColorBrush(WpfColor.FromArgb(glowAlpha, 239, 68, 68));
            glowBrush.Freeze();
            var glowPen = new WpfPen(glowBrush, width * 2.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            glowPen.Freeze();
            glowPens[i] = glowPen;

            var coreBrush = new SolidColorBrush(WpfColor.FromArgb(coreAlpha, 255, 220, 220));
            coreBrush.Freeze();
            var corePen = new WpfPen(coreBrush, width * 0.65)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            corePen.Freeze();
            corePens[i] = corePen;
        }

        s_glowPens = glowPens;
        s_corePens = corePens;
        return (glowPens, corePens);
    }

    private static WpfBrush CreateFrozenBrush(WpfColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
    {
        if (_trail.Count > 0 || _currentPointer.HasValue)
        {
            InvalidateVisual();
        }
    }

    private void HookRendering()
    {
        if (!_isRenderingHooked)
        {
            CompositionTarget.Rendering += OnCompositionRendering;
            _isRenderingHooked = true;
        }
    }

    private void UnhookRendering()
    {
        if (_isRenderingHooked)
        {
            CompositionTarget.Rendering -= OnCompositionRendering;
            _isRenderingHooked = false;
        }
    }
}
