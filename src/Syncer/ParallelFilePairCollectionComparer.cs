using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public class ParallelSyncFilePairCollectionComparer : ISyncFilePairCollectionComparer
{
    private readonly int _maxDegreeOfParallelism;

    public ParallelSyncFilePairCollectionComparer()
    {
        var processors = Environment.ProcessorCount;
        processors = Math.Max(1, processors);
        processors = Math.Min(4, processors);

        _maxDegreeOfParallelism = processors;
    }

    public ParallelSyncFilePairCollectionComparer(int maxDegreeOfParallelism)
    {
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public async ValueTask<SyncFilePairCollectionCompareResult> ComparePairs(
        IReadOnlyCollection<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FileProgressEvent>? fileProgress = null,
        IProgress<SyncFileByteProgress>? byteProgress = null,
        CancellationToken cancellationToken = default)
    {
        var identicalFiles = new ConcurrentBag<SyncFilePair>();
        var updatedFiles = new ConcurrentBag<SyncFilePair>();

        var totalFiles = pairs.Count;
        var progressedFiles = 0;

        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.StartSync, progressedFiles, totalFiles, pair.Source.Path.SubPath));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
                identicalFiles.Add(pair);
            else
                updatedFiles.Add(pair);

            Interlocked.Increment(ref progressedFiles);
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.DoneSync, progressedFiles, totalFiles, pair.Source.Path.SubPath));
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken,
            EnsureOrdered = false
        });

        foreach (var file in pairs)
        {
            await block.SendAsync(file);
        }
        block.Complete();
        await block.Completion;

        // convert concurrent collection to non-concurrent collection, 
        // for efficient accessing to its elements
        return new SyncFilePairCollectionCompareResult(
            updatedFiles.ToArray(),
            identicalFiles.ToArray());
    }
}