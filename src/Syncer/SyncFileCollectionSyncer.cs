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
        SyncerOptions? options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options), "Sync options with explicit Rules are required.");
        var evaluator = new SyncRuleEvaluator(options, _pathOptions);
        var pathCompareResult = SelectFiles(sources, targets, options, evaluator);

        var fileCompareResult = await _filePairSyncer.CompareFilePairs(
            pathCompareResult.DuplicatedFiles,
            new RuleFileComparer(evaluator),
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
        SyncerOptions? options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options), "Sync options with explicit Rules are required.");
        var evaluator = new SyncRuleEvaluator(options, _pathOptions);
        var pathCompareResult = SelectFiles(sources, targets, options, evaluator);

        var addedFilePairs = CreateFilePairs(pathCompareResult.AddedFiles);
        var duplicatedFilePairs = pathCompareResult.DuplicatedFiles;
        var fileCompareResult = await _filePairSyncer.CompareAndSyncFilePairs(
            addedFilePairs.Concat(duplicatedFilePairs),
            new RuleFileComparer(evaluator),
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
        SyncerOptions options,
        SyncRuleEvaluator evaluator)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        var paths = new SyncPathComparer().ComparePaths(sources, targets, _pathOptions);
        return new SyncFilePathCompareResult(
            paths.AddedFiles.Where(file => evaluator.Evaluate(file.Path.SubPath).Action != SyncAction.Exclude).ToArray(),
            paths.DuplicatedFiles.Where(pair => evaluator.Evaluate(pair.Source.Path.SubPath).Action
                is SyncAction.FullSync or SyncAction.UpdateOnly).ToArray(),
            paths.DeletedFiles.Where(file => evaluator.Evaluate(file.Path.SubPath).Action == SyncAction.FullSync).ToArray());
    }

    private sealed class RuleFileComparer(SyncRuleEvaluator evaluator) : IFileComparer
    {
        public ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
        {
            var comparer = evaluator.Evaluate(pair.Source.Path.SubPath).Comparer!;
            return comparer.AreEqual(pair, cancellationToken);
        }
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
