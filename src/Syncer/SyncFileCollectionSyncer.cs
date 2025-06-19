using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Syncer;

namespace FishSyncClient;

public class SyncFileCollectionSyncer
{
    private readonly ISyncFilePairSyncer _filePairSyncer;
    private readonly PathOptions _pathOptions;

    public SyncFileCollectionSyncer(ISyncFilePairSyncer pairSyncer, PathOptions pathOptions) =>
        (_filePairSyncer, _pathOptions) = (pairSyncer, pathOptions);

    public async Task<SyncFileCollectionComparerResult> CompareFiles(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncerOptions? options)
    {
        options ??= new();

        var pathComparer = new SyncPathComparer();
        var pathCompareResult = pathComparer.ComparePaths(sources, targets, _pathOptions);

        var fileCompareResult = await _filePairSyncer.CompareFilePairs(
            pathCompareResult.DuplicatedFiles.Where(pair => options.TargetPathMatcher.Match(pair.Source.Path.SubPath)),
            comparer,
            options.FileProgress,
            options.ByteProgress,
            options.CancellationToken);

        return new SyncFileCollectionComparerResult(
            pathCompareResult.AddedFiles,
            fileCompareResult.UpdatedFiles,
            fileCompareResult.IdenticalFiles,
            pathCompareResult.DeletedFiles.Where(file => options.TargetPathMatcher.Match(file.Path.SubPath)).ToList());
    }

    public async Task<SyncFileCollectionComparerResult> CompareAndSyncFiles(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncerOptions? options)
    {
        options ??= new();

        var pathComparer = new SyncPathComparer();
        var pathCompareResult = pathComparer.ComparePaths(sources, targets, _pathOptions);

        var addedFilePairs = CreateFilePairs(pathCompareResult.AddedFiles);
        var duplicatedFilePairs = pathCompareResult.DuplicatedFiles.Where(pair => options.TargetPathMatcher.Match(pair.Source.Path.SubPath));
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
            pathCompareResult.DeletedFiles.Where(file => options.TargetPathMatcher.Match(file.Path.SubPath)).ToList());
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