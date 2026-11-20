using System.Windows;
using Com.MiniScreenNotes.Models;

namespace Com.MiniScreenNotes.Helpers;

public static class DockSnapHelper
{
    public const double DefaultSnapThreshold = 24.0;
    public const double DefaultFloatingSlideDistance = 24.0;

    /// <summary>
    /// 依據面板吸附邊緣與方向，計算移入／移出過渡動畫的隱藏位移向量。
    /// 吸附於邊緣時完整移出螢幕邊界；浮動狀態則朝較近側滑動固定距離。
    /// </summary>
    public static Vector GetSlideOffset(
        DockEdge edge,
        PanelOrientation orientation,
        Size panelSize,
        Rect monitorBounds,
        Point panelPosition,
        double floatingSlideDistance = DefaultFloatingSlideDistance)
    {
        var width = Math.Max(panelSize.Width, 1.0);
        var height = Math.Max(panelSize.Height, 1.0);
        var distance = Math.Max(0.0, floatingSlideDistance);

        return edge switch
        {
            DockEdge.Top => new Vector(0, -height),
            DockEdge.Bottom => new Vector(0, height),
            DockEdge.Left => new Vector(-width, 0),
            DockEdge.Right => new Vector(width, 0),
            _ => GetFloatingSlideOffset(orientation, panelSize, monitorBounds, panelPosition, distance)
        };
    }

    private static Vector GetFloatingSlideOffset(
        PanelOrientation orientation,
        Size panelSize,
        Rect monitorBounds,
        Point panelPosition,
        double distance)
    {
        if (orientation == PanelOrientation.Horizontal)
        {
            var panelCenterY = panelPosition.Y + (panelSize.Height / 2.0);
            var monitorCenterY = monitorBounds.Top + (monitorBounds.Height / 2.0);
            return panelCenterY <= monitorCenterY
                ? new Vector(0, -distance)
                : new Vector(0, distance);
        }

        var panelCenterX = panelPosition.X + (panelSize.Width / 2.0);
        var monitorCenterX = monitorBounds.Left + (monitorBounds.Width / 2.0);
        return panelCenterX <= monitorCenterX
            ? new Vector(-distance, 0)
            : new Vector(distance, 0);
    }

    /// <summary>
    /// 依據面板方向與目前所在螢幕邊界，判斷是否應吸附至合法邊緣。
    /// 橫向面板僅吸附上/下緣；直向面板僅吸附左/右緣。
    /// </summary>
    public static DockEdge DetectDockEdge(
        Rect panelBounds,
        Rect monitorBounds,
        PanelOrientation orientation,
        double threshold = DefaultSnapThreshold)
    {
        if (orientation == PanelOrientation.Horizontal)
        {
            var distTop = Math.Abs(panelBounds.Top - monitorBounds.Top);
            var distBottom = Math.Abs(monitorBounds.Bottom - panelBounds.Bottom);

            if (distTop <= threshold && distTop <= distBottom)
            {
                return DockEdge.Top;
            }

            if (distBottom <= threshold)
            {
                return DockEdge.Bottom;
            }

            return DockEdge.None;
        }
        else
        {
            var distLeft = Math.Abs(panelBounds.Left - monitorBounds.Left);
            var distRight = Math.Abs(monitorBounds.Right - panelBounds.Right);

            if (distLeft <= threshold && distLeft <= distRight)
            {
                return DockEdge.Left;
            }

            if (distRight <= threshold)
            {
                return DockEdge.Right;
            }

            return DockEdge.None;
        }
    }

    /// <summary>
    /// 計算吸附後的面板展開座標（保持在螢幕範圍內）。
    /// </summary>
    public static Point GetDockedExpandedPosition(Rect panelBounds, Rect monitorBounds, DockEdge edge)
    {
        var clampedX = Math.Clamp(panelBounds.Left, monitorBounds.Left, Math.Max(monitorBounds.Left, monitorBounds.Right - panelBounds.Width));
        var clampedY = Math.Clamp(panelBounds.Top, monitorBounds.Top, Math.Max(monitorBounds.Top, monitorBounds.Bottom - panelBounds.Height));

        return edge switch
        {
            DockEdge.Top => new Point(clampedX, monitorBounds.Top),
            DockEdge.Bottom => new Point(clampedX, monitorBounds.Bottom - panelBounds.Height),
            DockEdge.Left => new Point(monitorBounds.Left, clampedY),
            DockEdge.Right => new Point(monitorBounds.Right - panelBounds.Width, clampedY),
            _ => new Point(clampedX, clampedY)
        };
    }

    /// <summary>
    /// 計算自動隱藏收合時只露出小手把的位置。
    /// </summary>
    public static Point GetHiddenHandlePosition(
        double panelLeft,
        double panelTop,
        double panelWidth,
        double panelHeight,
        double handleWidth,
        double handleHeight,
        Rect monitorBounds,
        DockEdge edge)
    {
        var centerX = panelLeft + (panelWidth / 2.0);
        var centerY = panelTop + (panelHeight / 2.0);

        return edge switch
        {
            DockEdge.Top => new Point(
                Math.Clamp(centerX - (handleWidth / 2.0), monitorBounds.Left, monitorBounds.Right - handleWidth),
                monitorBounds.Top),
            DockEdge.Bottom => new Point(
                Math.Clamp(centerX - (handleWidth / 2.0), monitorBounds.Left, monitorBounds.Right - handleWidth),
                monitorBounds.Bottom - handleHeight),
            DockEdge.Left => new Point(
                monitorBounds.Left,
                Math.Clamp(centerY - (handleHeight / 2.0), monitorBounds.Top, monitorBounds.Bottom - handleHeight)),
            DockEdge.Right => new Point(
                monitorBounds.Right - handleWidth,
                Math.Clamp(centerY - (handleHeight / 2.0), monitorBounds.Top, monitorBounds.Bottom - handleHeight)),
            _ => new Point(panelLeft, panelTop)
        };
    }

    /// <summary>
    /// 確保視窗位置落在所有可用螢幕的範圍內（多螢幕拔除或解析度變更時安全還原）。
    /// </summary>
    public static Point ClampToAvailableMonitors(Point desiredPosition, Size panelSize, IReadOnlyList<Rect> monitorBoundsList)
    {
        if (monitorBoundsList.Count == 0)
        {
            return new Point(Math.Max(0, desiredPosition.X), Math.Max(0, desiredPosition.Y));
        }

        var panelRect = new Rect(desiredPosition, panelSize);
        var target = monitorBoundsList.FirstOrDefault(m => m.IntersectsWith(panelRect));
        if (target.IsEmpty)
        {
            target = monitorBoundsList[0];
        }

        var x = Math.Clamp(desiredPosition.X, target.Left, Math.Max(target.Left, target.Right - panelSize.Width));
        var y = Math.Clamp(desiredPosition.Y, target.Top, Math.Max(target.Top, target.Bottom - panelSize.Height));
        return new Point(x, y);
    }

    /// <summary>
    /// 依據面板方向、總工具數量與每列/行上限，計算格線的欄數與列數。
    /// </summary>
    public static (int Columns, int Rows) CalculateGridDimensions(int itemCount, int maxItemsPerLine, PanelOrientation orientation)
    {
        var safeMax = Math.Max(1, maxItemsPerLine);
        var safeCount = Math.Max(1, itemCount);
        var lines = (int)Math.Ceiling((double)safeCount / safeMax);
        var primaryCount = Math.Min(safeCount, safeMax);

        return orientation == PanelOrientation.Horizontal
            ? (primaryCount, lines)
            : (lines, primaryCount);
    }

    /// <summary>
    /// 依面板方向計算工具在格線中的列與欄；橫向為列優先，直向為欄優先。
    /// </summary>
    public static (int Row, int Column) GetGridCell(
        int index,
        int columns,
        int rows,
        PanelOrientation orientation)
    {
        var safeColumns = Math.Max(1, columns);
        var safeRows = Math.Max(1, rows);

        return orientation == PanelOrientation.Horizontal
            ? (index / safeColumns, index % safeColumns)
            : (index % safeRows, index / safeRows);
    }
}
