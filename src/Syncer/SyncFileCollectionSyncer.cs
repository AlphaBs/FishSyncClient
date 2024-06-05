using FishSyncClient.Files;
using FishSyncClient.FileComparers;
using FishSyncClient.Syncer;

namespace FishSyncClient;

public class SyncFileCollectionSyncer
{
    private readonly ISyncFilePairSyncer _filePairSyncer;

    public SyncFileCollectionSyncer(ISyncFilePairSyncer pairSyncer) =>
        _filePairSyncer = pairSyncer;

    public async Task<SyncFileCollectionComparerResult> CompareFiles(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncerOptions? options)
    {
        options ??= new();
        var filter = GlobPatternMatcher.ParseFrom(options.Includes, options.Excludes);

        var pathComparer = new SyncPathComparer();
        var pathCompareResult = pathComparer.ComparePaths(sources, targets);

        var fileCompareResult = await _filePairSyncer.CompareFilePairs(
            pathCompareResult.DuplicatedFiles.Where(pair => filter.Match(pair.Source.Path.SubPath)),
            comparer,
            options.FileProgress,
            options.ByteProgress,
            options.CancellationToken);

        return new SyncFileCollectionComparerResult(
            pathCompareResult.AddedFiles,
            fileCompareResult.UpdatedFiles,
            fileCompareResult.IdenticalFiles,
            pathCompareResult.DeletedFiles.Where(file => filter.Match(file.Path.SubPath)).ToList());
    }

    public async Task<SyncFileCollectionComparerResult> CompareAndSyncFiles(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncerOptions? options)
    {
        options ??= new();
        var filter = GlobPatternMatcher.ParseFrom(options.Includes, options.Excludes);

        var pathComparer = new SyncPathComparer();
        var pathCompareResult = pathComparer.ComparePaths(sources, targets);

        var addedFilePairs = CreateFilePairs(pathCompareResult.AddedFiles);
        var duplicatedFilePairs = pathCompareResult.DuplicatedFiles.Where(pair => filter.Match(pair.Source.Path.SubPath));
        var fileCompareResult = await _filePairSyncer.CompareAndSyncFilePairs(
            addedFilePairs.Concat(duplicatedFilePairs),
            comparer,
            options.FileProgress,
            options.ByteProgress,
            options.CancellationToken);

        return new SyncFileCollectionComparerResult(
            pathCompareResult.AddedFiles,
            fileCompareResult.UpdatedFiles,
            fileCompareResult.IdenticalFiles,
            pathCompareResult.DeletedFiles.Where(file => filter.Match(file.Path.SubPath)).ToList());
    }

    protected virtual IEnumerable<SyncFilePair> CreateFilePairs(IEnumerable<SyncFile> sourceFiles)
    {
        yield break;
    }
}

public record SyncFileCollectionComparerResult(
    IReadOnlyCollection<SyncFile> AddedFiles,
    IReadOnlyCollection<SyncFilePair> UpdatedFilePairs,
    IReadOnlyCollection<SyncFilePair> IdenticalFilePairs,
    IReadOnlyCollection<SyncFile> DeletedFiles
);