using FishSyncClient.Files;
using FishSyncClient.Progress;
using NeoSmart.PrettySize;
using System.Collections;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FishSyncClient.Gui;

/// <summary>
/// SyncFileCollectionControl.xaml에 대한 상호 작용 논리
/// </summary>
public partial class SyncFileCollectionControl : UserControl, ISyncFileCollection
{
    private ConcurrentByteProgressAggregator _progressAggregator = new();
    private readonly DispatcherTimer _timer;

    public SyncFileCollectionControl()
    {
        InitializeComponent();
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += _timer_Tick;
    }

    private readonly Dictionary<string, SyncFileItem> pathItemMap = new();

    public string CollectionName
    {
        get => lbName.Content.ToString() ?? "";
        set => lbName.Content = value;
    }

    public long TotalFiles { get; private set; }
    public long TotalBytes { get; private set; }

    public int Count => lvFiles.Items.Count;

    public void Clear()
    {
        lvFiles.Items.Clear();
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
        lvFiles.Items.Add(item);
        pathItemMap[file.Path.SubPath] = item;

        updateControl();
    }

    public IEnumerable<SyncFile> GetFiles() => lvFiles.Items.Cast<SyncFileItem>().Select(item => item.File);

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

    public bool AddProgress(string path, ByteProgress progress)
    {
        if (pathItemMap.TryGetValue(path, out var item))
        {
            if (item.IsProgressing)
            {
                item.CurrentProgress += progress;
                item.Status = item.CurrentProgress.GetPercentage(false).ToString("p");
                _progressAggregator.Report(progress);
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
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
        pbProgress.Value = 0;
        lbProgress.Content = "";
    }

    private void updateAggregatedProgress()
    {
        var progress = _progressAggregator.AggregateProgress();
        pbProgress.Maximum = progress.TotalBytes;
        pbProgress.Value = progress.ProgressedBytes;
        lbProgress.Content = $"{progress.GetPercentage(false):p} ({progress.ProgressedBytes:#,##} / {progress.TotalBytes:#,##})";
    }

    private void _timer_Tick(object? sender, EventArgs e)
    {
        updateAggregatedProgress();
    }

    private void updateControl()
    {
        lbTotalCount.Content = TotalFiles + "개";
        var prettySize = new PrettySize(TotalBytes);
        lbTotalSize.Content = $"{prettySize.Format(UnitBase.Base10)} ({TotalBytes:##,#} bytes)";
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
