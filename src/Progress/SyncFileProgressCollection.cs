using FishSyncClient.Files;
using System.Collections;

namespace FishSyncClient.Progress;

public class SyncFileProgressCollection : ISyncFileCollection
{
    private readonly Dictionary<string, SyncFileProgressItem> _collection = new();
    private readonly ConcurrentByteProgressAggregator _progressAggregator = new();

    public class SyncFileProgressItem
    {
        public SyncFileProgressItem(SyncFile file)
        {
            File = file;
        }

        public SyncFile File { get; }
        public FileProgressEventType EventType { get; set; }
        public ByteProgress Progress { get; set; }
    }

    public SyncFileProgressCollection()
    {

    }

    public SyncFileProgressCollection(IEnumerable<SyncFile> files)
    {
        foreach (var file in files)
        {
            Add(file);
        }
    }

    public int Count => _collection.Count;

    public IEnumerable<SyncFileProgressItem> GetItems() => 
        _collection.Values;

    public SyncFileProgressItem? FindItemByPath(string path)
    {
        if (_collection.TryGetValue(path, out var item))
        {
            return item;
        }
        else
        {
            return null;
        }
    }

    public SyncFile? FindFileByPath(string path)
    {
        return FindItemByPath(path)?.File;
    }

    public void Add(SyncFile file)
    {
        var item = new SyncFileProgressItem(file);
        _collection[file.Path.SubPath] = item;
    }

    public bool SetEventType(SyncFile file, FileProgressEventType eventType) => 
        SetEventType(file.Path.SubPath, eventType);

    public bool SetEventType(string path, FileProgressEventType eventType)
    {
        var item = FindItemByPath(path);
        if (item == null)
            return false;

        item.EventType = eventType;
        return true;
    }

    public bool AddProgress(SyncFile file, ByteProgress progress) =>
        AddProgress(file.Path.SubPath, progress);

    public bool AddProgress(string path, ByteProgress progress)
    {
        var item = FindItemByPath(path);
        if (item == null)
            return false;

        item.Progress += progress;
        _progressAggregator.Report(progress);
        return true;
    }

    public void ClearProgress()
    {
        _progressAggregator.Clear();
    }

    public ByteProgress AggregateProgress()
    {
        return _progressAggregator.AggregateProgress();
    }

    private IEnumerable<SyncFile> getFiles() => _collection.Values.Select(item => item.File);

    public IEnumerator<SyncFile> GetEnumerator()
    {
        return getFiles().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return getFiles().GetEnumerator();
    }
}
