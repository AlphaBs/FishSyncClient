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
        if (options == null)
            throw new ArgumentNullException(nameof(options), "Sync options with explicit Rules are required.");
        var pathCompareResult = SelectFiles(sources, targets, options);

        var fileCompareResult = await _filePairSyncer.CompareFilePairs(
            pathCompareResult.DuplicatedFiles,
            comparer,
            options.FileProgress,
            options.ByteProgress,
            options.CancellationToken);

        return new SyncFileCollectionComparerResult(
            pathCompareResult.AddedFiles,
            fileCompareResult.UpdatedFiles,
            fileCompareResult.IdenticalFiles,
            pathCompareResult.DeletedFiles);
    }

    public async Task<SyncFileCollectionComparerResult> CompareAndSyncFiles(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncerOptions? options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options), "Sync options with explicit Rules are required.");
        var pathCompareResult = SelectFiles(sources, targets, options);

        var addedFilePairs = CreateFilePairs(pathCompareResult.AddedFiles);
        var duplicatedFilePairs = pathCompareResult.DuplicatedFiles;
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
            pathCompareResult.DeletedFiles);
    }

    private SyncFilePathCompareResult SelectFiles(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        SyncerOptions options)
    {
        var evaluator = new SyncRuleEvaluator(options, _pathOptions);
        options.CancellationToken.ThrowIfCancellationRequested();
        var paths = new SyncPathComparer().ComparePaths(sources, targets, _pathOptions);
        return new SyncFilePathCompareResult(
            paths.AddedFiles.Where(file => evaluator.Evaluate(file.Path.SubPath) != SyncAction.Exclude).ToArray(),
            paths.DuplicatedFiles.Where(pair => evaluator.Evaluate(pair.Source.Path.SubPath) == SyncAction.FullSync).ToArray(),
            paths.DeletedFiles.Where(file => evaluator.Evaluate(file.Path.SubPath) == SyncAction.FullSync).ToArray());
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
