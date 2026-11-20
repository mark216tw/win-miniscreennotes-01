using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace Com.MiniScreenNotes.Views.Controls;

public sealed class SpotlightOverlayLayer : FrameworkElement
{
    private bool _isActive;
    private Point? _center;
    private double _radius = 140.0;

    public SpotlightOverlayLayer()
    {
        IsHitTestVisible = false;
    }

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

        WpfBrush dimBrush = new SolidColorBrush(WpfColor.FromArgb(175, 10, 15, 25));
        dimBrush.Freeze();

        var fullRect = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        if (_center is { } center)
        {
            var hole = new EllipseGeometry(center, _radius, _radius);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, hole);
            combined.Freeze();
            dc.DrawGeometry(dimBrush, null, combined);

            WpfBrush ringBrush = new SolidColorBrush(WpfColor.FromArgb(200, 255, 255, 255));
            ringBrush.Freeze();
            var ringPen = new WpfPen(ringBrush, 2.0);
            ringPen.Freeze();
            dc.DrawEllipse(null, ringPen, center, _radius, _radius);
        }
        else
        {
            dc.DrawGeometry(dimBrush, null, fullRect);
        }
    }
}
