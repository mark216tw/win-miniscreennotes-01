using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Resources.Strings;

namespace Com.MiniScreenNotes.Views;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _closeTimer;
    private string? _folderToOpen;

    public ToastWindow(string message, string? folderToOpen = null)
    {
        InitializeComponent();

        _folderToOpen = folderToOpen;
        UpdateContent(message, folderToOpen);

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.SetToolWindowStyle(hwnd, noActivate: true);
            Win32Interop.TryExcludeFromCapture(hwnd, true);
        };

        Loaded += (_, _) => PositionBottomRight();
        SizeChanged += (_, _) => PositionBottomRight();

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Close();
        };
        _closeTimer.Start();
    }

    public void UpdateMessage(string message, string? folderToOpen)
    {
        _folderToOpen = folderToOpen;
        UpdateContent(message, folderToOpen);
        _closeTimer.Stop();
        if (!IsMouseOver)
        {
            _closeTimer.Start();
        }

        UpdateLayout();
        PositionBottomRight();
    }

    private void UpdateContent(string message, string? folderToOpen)
    {
        MessageText.Text = message;
        HintText.Text = Strings.Toast_OpenFolderHint;
        HintText.Visibility = folderToOpen is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PositionBottomRight()
    {
        if (!IsLoaded)
        {
            return;
        }

        var monitor = Win32Interop.GetCursorPos(out var cursor)
            ? DisplayMonitorHelper.GetMonitorContainingPhysicalPoint(cursor.X, cursor.Y)
            : DisplayMonitorHelper.GetPrimaryMonitor();
        var workArea = monitor.DipWorkArea;

        Left = Math.Max(workArea.Left, workArea.Right - ActualWidth - 16);
        Top = Math.Max(workArea.Top, workArea.Bottom - ActualHeight - 16);
    }

    private void Border_MouseEnter(object sender, MouseEventArgs e)
    {
        _closeTimer.Stop();
    }

    private void Border_MouseLeave(object sender, MouseEventArgs e)
    {
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_folderToOpen) && Directory.Exists(_folderToOpen))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _folderToOpen,
                UseShellExecute = true
            });
        }

        _closeTimer.Stop();
        Close();
    }
}
