using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Com.MiniScreenNotes.Helpers;
using Com.MiniScreenNotes.Models;
using Com.MiniScreenNotes.Resources.Strings;
using Microsoft.Extensions.Logging;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfClipboard = System.Windows.Clipboard;

namespace Com.MiniScreenNotes.Services;

public sealed class ScreenCaptureService
{
    private readonly ConfigService _configService;
    private readonly ILogger<ScreenCaptureService> _logger;

    public ScreenCaptureService(ConfigService configService, ILogger<ScreenCaptureService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public event Action<string, string?>? NotificationRequested;

    public bool CopyMonitorToClipboard(MonitorInfo monitor)
    {
        try
        {
            var bmp = CaptureMonitor(monitor);
            if (bmp is null || !TrySetClipboardImage(bmp))
            {
                NotificationRequested?.Invoke(Strings.Toast_CopyFailed, null);
                return false;
            }

            NotificationRequested?.Invoke(Strings.Toast_Copied, null);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "複製螢幕標註到剪貼簿失敗");
            NotificationRequested?.Invoke(Strings.Toast_CopyFailed, null);
            return false;
        }
    }

    private static bool TrySetClipboardImage(BitmapSource bmp)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                WpfClipboard.SetImage(bmp);
                return true;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
                Thread.Sleep(40);
            }
        }

        return false;
    }

    public bool SaveMonitorToFile(MonitorInfo monitor)
    {
        try
        {
            var bmp = CaptureMonitor(monitor);
            if (bmp is null)
            {
                NotificationRequested?.Invoke(Strings.Toast_SaveFailed, null);
                return false;
            }

            var config = _configService.CurrentConfig;
            if (config.QuickSaveEnabled && !string.IsNullOrWhiteSpace(config.QuickSaveDirectory)
                && TryQuickSaveBitmap(bmp, config.QuickSaveDirectory, config.QuickSaveFormat))
            {
                return true;
            }

            var defaultFileName = $"MiniScreenNotes_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var dialog = new SaveFileDialog
            {
                Title = Strings.Action_Save,
                FileName = defaultFileName,
                Filter = "PNG 圖片 (*.png)|*.png|JPEG 圖片 (*.jpg;*.jpeg)|*.jpg;*.jpeg",
                DefaultExt = ".png",
                InitialDirectory = Directory.Exists(config.LastSaveDirectory)
                    ? config.LastSaveDirectory
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            var format = Path.GetExtension(dialog.FileName).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                         || Path.GetExtension(dialog.FileName).Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                ? ImageSaveFormat.Jpeg
                : ImageSaveFormat.Png;

            WriteBitmapToFile(bmp, dialog.FileName, format);
            config.LastSaveDirectory = Path.GetDirectoryName(dialog.FileName);
            _configService.Save();

            NotificationRequested?.Invoke($"{Strings.Toast_Saved}{dialog.FileName}", Path.GetDirectoryName(dialog.FileName));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "儲存螢幕標註失敗");
            NotificationRequested?.Invoke(Strings.Toast_SaveFailed, null);
            return false;
        }
    }

    private bool TryQuickSaveBitmap(BitmapSource bmp, string directory, ImageSaveFormat format)
    {
        try
        {
            return QuickSaveBitmap(bmp, directory, format);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _logger.LogWarning(ex, "快速儲存失敗，改用另存新檔對話框：{Directory}", directory);
            NotificationRequested?.Invoke(Strings.Toast_QuickSaveFallback, null);
            return false;
        }
    }

    private bool QuickSaveBitmap(BitmapSource bmp, string directory, ImageSaveFormat format)
    {
        Directory.CreateDirectory(directory);
        var ext = format == ImageSaveFormat.Jpeg ? ".jpg" : ".png";
        var filePath = Path.Combine(directory, $"MiniScreenNotes_{DateTime.Now:yyyyMMdd_HHmmss_fff}{ext}");

        WriteBitmapToFile(bmp, filePath, format);
        NotificationRequested?.Invoke($"{Strings.Toast_Saved}{Path.GetFileName(filePath)}", directory);
        return true;
    }

    private static BitmapSource? CaptureMonitor(MonitorInfo monitor)
    {
        var bounds = monitor.PhysicalBounds;
        return ScreenCaptureHelper.CapturePhysicalRegion(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    private static void WriteBitmapToFile(BitmapSource bmp, string filePath, ImageSaveFormat format)
    {
        BitmapEncoder encoder = format == ImageSaveFormat.Jpeg
            ? new JpegBitmapEncoder { QualityLevel = 92 }
            : new PngBitmapEncoder();

        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }
}
