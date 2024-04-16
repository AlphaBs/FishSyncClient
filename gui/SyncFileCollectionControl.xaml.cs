using FishSyncClient.Files;
using NeoSmart.PrettySize;
using System.Collections;
using System.Windows.Controls;

namespace FishSyncClient.Gui;

/// <summary>
/// SyncFileCollectionControl.xaml에 대한 상호 작용 논리
/// </summary>
public partial class SyncFileCollectionControl : UserControl, ISyncFileCollection
{
    public SyncFileCollectionControl()
    {
        InitializeComponent();
    }

    private readonly Dictionary<string, SyncFileItem> pathItemMap = new();

    public string CollectionName
    {
        get => lbName.Content.ToString() ?? "";
        set => lbName.Content = value;
    }

    public long TotalFiles { get; private set; }
    public long TotalBytes { get; private set; }

    public int Count => throw new NotImplementedException();

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
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool SetProgress(string path, double progress)
    {
        if (pathItemMap.TryGetValue(path, out var item))
        {
            if (item.IsProgressing)
            {
                item.Status = progress.ToString("p");
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
