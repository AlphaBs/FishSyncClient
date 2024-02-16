using DotNet.Globbing;

namespace FishSyncClient.Syncer;

public class FishPathSyncer
{
    private readonly Glob[] _excludes;
    
    public FishPathSyncer() : this(Enumerable.Empty<string>())
    {
        
    }

    public FishPathSyncer(IEnumerable<string> updateExcludes)
    {
        _excludes = updateExcludes.Select(pattern => Glob.Parse(pattern)).ToArray();
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
            .Where(p => !isExcludedPath(p.Path))
            .ToArray();
        var deleted = targetDict
            .Select(kv => kv.Value)
            .Where(p => !isExcludedPath(p.Path))
            .ToArray();
        var added = sourceDict
            .Select(kv => kv.Value)
            .ToArray();

        return new FishPathSyncResult(added, duplicated, deleted);
    }

    private bool isExcludedPath(RootedPath path)
    {
        return _excludes.Any(glob => glob.IsMatch(path.SubPath));
    }
}

public class FishPathSyncResult
{
    public FishPathSyncResult(FishPath[] added, FishPath[] duplicated, FishPath[] deleted)
    {
        AddedPaths = added;
        DuplicatedPaths = duplicated;
        DeletedPaths = deleted;
    }

    public FishPath[] AddedPaths { get; }
    public FishPath[] DuplicatedPaths { get; }
    public FishPath[] DeletedPaths { get; }
}