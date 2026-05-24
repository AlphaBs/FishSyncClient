using Avalonia.Controls;
using Avalonia.Threading;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using NeoSmart.PrettySize;
using System.Collections;
using System.Collections.ObjectModel;

namespace FishSyncClient.Gui;

/// <summary>
/// Avalonia port of the WPF SyncFileCollectionControl.
/// </summary>
public partial class SyncFileCollectionControl : UserControl, ISyncFileCollection
{
    private ConcurrentByteProgressAggregator _progressAggregator = new();
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<SyncFileItem> _items = new();
    private readonly Dictionary<string, SyncFileItem> pathItemMap = new();

    // Byte progress is reported from background worker threads, once per stream buffer.
    // Instead of marshalling every report to the UI thread, accumulate per-file deltas here
    // and flush them in batches from the timer tick (UI thread). This collapses tens of
    // thousands of UI updates per second into ~10/s.
    private readonly object _pendingLock = new();
    private Dictionary<string, ByteProgress> _pendingByteProgress = new();

    public SyncFileCollectionControl()
    {
        InitializeComponent();

        lvFiles.ItemsSource = _items;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _timer.Tick += _timer_Tick;
    }

    public string CollectionName
    {
        get => lbName.Text ?? "";
        set => lbName.Text = value;
    }

    public long TotalFiles { get; private set; }
    public long TotalBytes { get; private set; }

    public int Count => _items.Count;

    public void Clear()
    {
        _items.Clear();
        pathItemMap.Clear();
        TotalFiles = 0;
        TotalBytes = 0;

        updateControl();
    }

    public void Add(SyncFile file)
    {
        TotalFiles++;
        TotalBytes += (file.Metadata?.Size ?? 0);

        var item = new SyncFileItem(file);
        _items.Add(item);
        pathItemMap[file.Path.SubPath] = item;

        updateControl();
    }

    public IEnumerable<SyncFile> GetFiles() => _items.Select(item => item.File);

    public SyncFile? FindFileByPath(string path)
    {
        if (pathItemMap.TryGetValue(path, out var item))
        {
            return item.File;
        }
        else
        {
            return null;
        }
    }

    public bool SetStatus(SyncFile file, FileProgressEventType type) => SetStatus(file.Path.SubPath, type);

    public bool SetStatus(string path, FileProgressEventType type)
    {
        if (type == FileProgressEventType.DoneSync)
            CompleteProgress(path);
        else
            StartProgress(path);

        return SetStatus(path, type switch
        {
            FileProgressEventType.Queue => "작업 대기",
            FileProgressEventType.StartCompare => "비교 중",
            FileProgressEventType.DoneCompare => "비교 완료",
            FileProgressEventType.StartSync => "동기화 중",
            FileProgressEventType.DoneSync => "동기화 완료",
            _ => "대기"
        });
    }

    public bool SetStatus(SyncFile file, string status) => SetStatus(file.Path.SubPath, status);

    public bool SetStatus(string path, string status)
    {
        if (pathItemMap.TryGetValue(path, out var item))
        {
            item.Status = status;
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool StartProgress(string path)
    {
        if (pathItemMap.TryGetValue(path, out var item))
        {
            item.IsProgressing = true;
            _timer.Start();
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool AddProgress(SyncFile file, ByteProgress progress) => AddProgress(file.Path.SubPath, progress);

    /// <summary>
    /// Records a byte-progress delta for the given path. Thread-safe and UI-free: the delta is
    /// merely accumulated and the actual UI update happens on the next timer tick. Safe to call
    /// from background worker threads at high frequency.
    /// </summary>
    public bool AddProgress(string path, ByteProgress progress)
    {
        // Aggregator is thread-safe (thread-local storage) and drives the overall progress bar.
        _progressAggregator.Report(progress);

        lock (_pendingLock)
        {
            _pendingByteProgress.TryGetValue(path, out var current);
            _pendingByteProgress[path] = current + progress;
        }

        return true;
    }

    private void flushPendingProgress()
    {
        Dictionary<string, ByteProgress> batch;
        lock (_pendingLock)
        {
            if (_pendingByteProgress.Count == 0)
                return;

            batch = _pendingByteProgress;
            _pendingByteProgress = new Dictionary<string, ByteProgress>();
        }

        foreach (var (path, delta) in batch)
        {
            if (pathItemMap.TryGetValue(path, out var item) && item.IsProgressing)
            {
                item.CurrentProgress += delta;
                item.Status = item.CurrentProgress.GetRatio().ToString("p");
            }
        }
    }

    public bool CompleteProgress(string path)
    {
        if (pathItemMap.TryGetValue(path, out var item))
        {
            item.IsProgressing = false;
            return true;
        }
        else
        {
            return false;
        }
    }

    public void ClearProgress()
    {
        _timer.Stop();
        _progressAggregator.Clear();
        lock (_pendingLock)
        {
            _pendingByteProgress.Clear();
        }
        pbProgress.Value = 0;
        lbProgress.Text = "";
    }

    private void updateAggregatedProgress()
    {
        var progress = _progressAggregator.AggregateProgress();
        pbProgress.Maximum = progress.TotalBytes;
        pbProgress.Value = progress.ProgressedBytes;
        lbProgress.Text = $"{progress.GetRatio():p} ({progress.ProgressedBytes:#,##} / {progress.TotalBytes:#,##})";
    }

    private void _timer_Tick(object? sender, EventArgs e)
    {
        flushPendingProgress();
        updateAggregatedProgress();
    }

    private void updateControl()
    {
        lbTotalCount.Text = TotalFiles + "개";
        var prettySize = new PrettySize(TotalBytes);
        lbTotalSize.Text = $"{prettySize.Format(UnitBase.Base10)} ({TotalBytes:##,#} bytes)";
    }

    public IEnumerator<SyncFile> GetEnumerator()
    {
        return GetFiles().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetFiles().GetEnumerator();
    }
}
