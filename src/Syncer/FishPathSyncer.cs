namespace FishSyncClient.Syncer;

public class FishPathSyncer
{
    private readonly HashSet<string> _updateBlacklists;

    public FishPathSyncer()
    {
        _updateBlacklists = new();
    }

    public FishPathSyncer(IEnumerable<string> updateBlacklists, PathOptions pathOptions)
    {
        var a = updateBlacklists.Select(p => PathHelper.NormalizePath(p, pathOptions));
        _updateBlacklists = new HashSet<string>(a);
    }

    public FishPathSyncResult Sync(IEnumerable<FishPath> source, IEnumerable<FishPath> target)
    {
        var sourceDict = source.ToDictionary(s => s.Path.GetFullPath(), s => s);
        var targetDict = target.ToDictionary(t => t.Path.GetFullPath(), t => t);

        var duplicated = sourceDict.Intersect(targetDict)
            .Where(kv => !_updateBlacklists.Contains(kv.Key))
            .Select(kv => kv.Value).ToArray();
        var added = sourceDict.Except(targetDict)
            .Select(kv => kv.Value).ToArray();
        var deleted = targetDict.Except(sourceDict)
            .Where(kv => !_updateBlacklists.Contains(kv.Key))
            .Select(kv => kv.Value).ToArray();
        
        return new FishPathSyncResult(added, duplicated, deleted);
    }
}
