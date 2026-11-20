using System.Windows;
using System.Windows.Media;

namespace Com.MiniScreenNotes.Models.Annotations;

public interface IAnnotationElement
{
    string MonitorId { get; }
    void Render(DrawingContext dc);
    bool HitTest(Point point, double tolerance);
}
