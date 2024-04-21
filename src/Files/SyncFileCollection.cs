using System.Collections;

namespace FishSyncClient.Files;

public class SyncFileCollection : ISyncFileCollection
{
    private readonly Dictionary<string, SyncFile> _collection = new();

    public SyncFileCollection(IEnumerable<SyncFile> files)
    {
        foreach (var file in files) 
        {
            _collection[file.Path.SubPath] = file;
        }
    }

    public int Count => _collection.Count;

    public SyncFile? FindFileByPath(string path)
    {
        if (_collection.TryGetValue(path, out var file))
            return file;
        else
            return null;
    }

    public IEnumerator<SyncFile> GetEnumerator()
    {
        return _collection.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _collection.Values.GetEnumerator();
    }
}
