using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public class SyncPathComparer
{
    public SyncFilePathCompareResult ComparePaths(
        IEnumerable<SyncFile> source, 
        IEnumerable<SyncFile> target,
        PathOptions pathOptions)
    {
        var comparer = pathOptions.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var sourceDict = source.ToDictionary(s => s.Path.SubPath, s => s, comparer);
        var targetDict = target.ToDictionary(t => t.Path.SubPath, t => t, comparer);

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

        return new SyncFilePathCompareResult(added, duplicated, deleted);
    }
}

public record SyncFilePathCompareResult(
    IReadOnlyCollection<SyncFile> AddedFiles,
    IReadOnlyCollection<SyncFilePair> DuplicatedFiles,
    IReadOnlyCollection<SyncFile> DeletedFiles
);