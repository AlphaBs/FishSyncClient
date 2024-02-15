using Ganss.Text;

namespace FishSyncClient.Syncer;

public class FishPathSyncer
{
    private readonly HashSet<string> _updateExcludeFiles = new();
    private readonly AhoCorasick _updateExcludeDirs = new();

    public FishPathSyncer() : this(Enumerable.Empty<RootedPath>())
    {
        
    }

    public FishPathSyncer(IEnumerable<RootedPath> updateExcludes)
    {
        foreach (var path in updateExcludes)
        {
            if (path.IsDirectory)
            {
                _updateExcludeFiles.Add(path.SubPath);
            }
            else
            {
                _updateExcludeDirs.Add(path.SubPath);
            }
        }
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
            .Where(path => !_updateExcludeDirs.Search(path.Path.SubPath).Any())
            .Where(path => !_updateExcludeFiles.Contains(path.Path.SubPath))
            .ToArray();
        var added = sourceDict
            .Select(kv => kv.Value)
            .ToArray();
        var deleted = targetDict
            .Where(kv => !_updateExcludeDirs.Search(kv.Key).Any())
            .Where(kv => !_updateExcludeFiles.Contains(kv.Key))
            .Select(kv => kv.Value)
            .ToArray();

        return new FishPathSyncResult(added, duplicated, deleted);
    }
}
