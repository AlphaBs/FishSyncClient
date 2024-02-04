namespace FishSyncClient.Syncer;

public class FishPathSyncer
{
    public FishPathSyncResult Sync(IEnumerable<FishPath> source, IEnumerable<FishPath> target)
    {
        var sourceDict = source.ToDictionary(s => s.Path.GetFullPath(), s => s);
        var targetDict = target.ToDictionary(t => t.Path.GetFullPath(), t => t);

        var duplicated = sourceDict.Intersect(targetDict)
            .Select(kv => kv.Value).ToArray();
        var added = sourceDict.Except(targetDict)
            .Select(kv => kv.Value).ToArray();
        var deleted = targetDict.Except(sourceDict)
            .Select(kv => kv.Value).ToArray();
        
        return new FishPathSyncResult(added, duplicated, deleted);
    }
}
