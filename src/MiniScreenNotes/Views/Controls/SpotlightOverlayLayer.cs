using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace Com.MiniScreenNotes.Views.Controls;

public sealed class SpotlightOverlayLayer : FrameworkElement
{
    private static readonly WpfBrush DimBrush = CreateFrozenBrush(WpfColor.FromArgb(175, 10, 15, 25));
    private static readonly WpfPen RingPen = CreateFrozenPen(WpfColor.FromArgb(200, 255, 255, 255), 2.0);

    private bool _isActive;
    private Point? _center;
    private double _radius = 140.0;

    public SpotlightOverlayLayer()
    {
        IsHitTestVisible = false;
    }

    public double Radius
    {
        get => _radius;
        set
        {
            var clamped = Math.Clamp(value, 60.0, 450.0);
            if (Math.Abs(_radius - clamped) > 0.01)
            {
                _radius = clamped;
                InvalidateVisual();
            }
        }
    }

    public event Action<double>? RadiusChanged;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                if (!value)
                {
                    _center = null;
                }

                InvalidateVisual();
            }
        }
    }

    public void UpdatePosition(Point position)
    {
        _center = position;
        InvalidateVisual();
    }

    public void AdjustRadius(double delta)
    {
        _radius = Math.Clamp(_radius + delta, 60.0, 450.0);
        RadiusChanged?.Invoke(_radius);
        InvalidateVisual();
    }

    public void ClearPosition()
    {
        _center = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!_isActive || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var fullRect = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        if (_center is { } center)
        {
            var hole = new EllipseGeometry(center, _radius, _radius);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, hole);
            combined.Freeze();
            dc.DrawGeometry(DimBrush, null, combined);
            dc.DrawEllipse(null, RingPen, center, _radius, _radius);
        }
        else
        {
            dc.DrawGeometry(DimBrush, null, fullRect);
        }
    }

    private static WpfBrush CreateFrozenBrush(WpfColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static WpfPen CreateFrozenPen(WpfColor color, double thickness)
    {
        var pen = new WpfPen(CreateFrozenBrush(color), thickness);
        pen.Freeze();
        return pen;
    }
}
