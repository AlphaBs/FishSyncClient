﻿using DotNet.Globbing;
using FishSyncClient.Files;

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

        var intersects = new List<FishPathPair>();
        foreach (var sourceKv in sourceDict)
        {
            if (targetDict.TryGetValue(sourceKv.Key, out var targetValue))
            {
                intersects.Add(new FishPathPair(sourceKv.Value, targetValue));
            }
        }

        foreach (var intersect in intersects)
        {
            sourceDict.Remove(intersect.Source.Path.SubPath);
            targetDict.Remove(intersect.Target.Path.SubPath);
        }

        var duplicated = intersects
            .Where(p => !isExcludedPath(p.Source.Path))
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
    public FishPathSyncResult(FishPath[] added, FishPathPair[] duplicated, FishPath[] deleted)
    {
        AddedPaths = added;
        DuplicatedPaths = duplicated;
        DeletedPaths = deleted;
    }

    public FishPath[] AddedPaths { get; }
    public FishPathPair[] DuplicatedPaths { get; }
    public FishPath[] DeletedPaths { get; }
}