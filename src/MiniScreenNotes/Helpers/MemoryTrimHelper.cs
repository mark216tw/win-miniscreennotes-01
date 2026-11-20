using System.Runtime;

namespace Com.MiniScreenNotes.Helpers;

public sealed class MemoryTrimHelper : IDisposable
{
    private readonly object _sync = new();
    private System.Threading.Timer? _timer;
    private bool _disposed;

    public void ScheduleTrim(TimeSpan delay)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _timer ??= new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void CancelPendingTrim()
    {
        lock (_sync)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void OnTimerElapsed(object? state)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _timer?.Dispose();
            _timer = null;
        }

        TrimNow();
    }

    public static void TrimNow()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            Win32Interop.TrimWorkingSet();
        }
        catch
        {
            // 記憶體修剪失敗不應影響應用程式正常運作
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
