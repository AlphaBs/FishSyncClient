using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public class FishPathSyncer
{
    public FishPathSyncResult Sync(IEnumerable<SyncFile> source, IEnumerable<SyncFile> target)
    {
        var sourceDict = source.ToDictionary(s => s.Path.SubPath, s => s);
        var targetDict = target.ToDictionary(t => t.Path.SubPath, t => t);

        var intersects = new List<SyncFilePair>();
        foreach (var sourceKv in sourceDict)
        {
            if (targetDict.TryGetValue(sourceKv.Key, out var targetValue))
            {
                intersects.Add(new SyncFilePair(sourceKv.Value, targetValue));
            }
        }

        foreach (var intersect in intersects)
        {
            sourceDict.Remove(intersect.Source.Path.SubPath);
            targetDict.Remove(intersect.Target.Path.SubPath);
        }

        var duplicated = intersects
            .ToArray();
        var deleted = targetDict
            .Select(kv => kv.Value)
            .ToArray();
        var added = sourceDict
            .Select(kv => kv.Value)
            .ToArray();

        return new FishPathSyncResult(added, duplicated, deleted);
    }
}

public class FishPathSyncResult
{
    public FishPathSyncResult(SyncFile[] added, SyncFilePair[] duplicated, SyncFile[] deleted)
    {
        AddedPaths = added;
        DuplicatedPaths = duplicated;
        DeletedPaths = deleted;
    }

    public SyncFile[] AddedPaths { get; }
    public SyncFilePair[] DuplicatedPaths { get; }
    public SyncFile[] DeletedPaths { get; }
}