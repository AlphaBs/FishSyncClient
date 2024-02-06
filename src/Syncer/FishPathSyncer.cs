namespace FishSyncClient.Syncer;

public class FishPathSyncer
{
    private readonly HashSet<string> _updateBlackFiles;

    public FishPathSyncer()
    {
        _updateBlackFiles = new();
    }

    public FishPathSyncer(IEnumerable<RootedPath> updateBlacklists, PathOptions pathOptions)
    {
        _updateBlackFiles = new HashSet<string>(updateBlacklists.Select(p => p.SubPath));
    }

    public FishPathSyncResult Sync(IEnumerable<FishPath> source, IEnumerable<FishPath> target)
    {
        var sourceDict = source.ToDictionary(s => s.Path.SubPath, s => s);
        var targetDict = target.ToDictionary(t => t.Path.SubPath, t => t);

        var intersects = new List<FishPath>();
        foreach (var sourceKv in sourceDict)
        {
            if (targetDict.Remove(sourceKv.Key))
            {
                intersects.Add(sourceKv.Value);
            }
        }

        foreach (var intersect in intersects)
        {
            sourceDict.Remove(intersect.Path.SubPath);
            targetDict.Remove(intersect.Path.SubPath);
        }

        var duplicated = intersects
            .Where(path => !_updateBlackFiles.Contains(path.Path.SubPath))
            .ToArray();
        var added = sourceDict
            .Select(kv => kv.Value)
            .ToArray();
        var deleted = targetDict
            .Where(kv => !_updateBlackFiles.Contains(kv.Key))
            .Select(kv => kv.Value)
            .ToArray();

        return new FishPathSyncResult(added, duplicated, deleted);
    }
}
