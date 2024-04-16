using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public class ParallelFileSyncer : IFishFileSyncer
{
    private readonly int _maxDegreeOfParallelism;

    public ParallelFileSyncer()
    {
        var processors = Environment.ProcessorCount;
        processors = Math.Max(1, processors);
        processors = Math.Min(4, processors);
        _maxDegreeOfParallelism = processors;
    }

    public ParallelFileSyncer(int maxDegreeOfParallelism)
    {
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public async ValueTask<FishFileSyncResult> Sync(
        IReadOnlyCollection<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FishFileProgressEventArgs>? fileProgress = null,
        IProgress<ByteProgress>? byteProgress = null,
        CancellationToken cancellationToken = default)
    {
        var identicalFiles = new ConcurrentBag<SyncFilePair>();
        var updatedFiles = new ConcurrentBag<SyncFilePair>();

        var totalFiles = pairs.Count;
        var progressedFiles = 0;
        var progressStorage = new ThreadLocal<ByteProgress>(() => new ByteProgress(), true);

        var (firstBlock, lastBlock) = createBlock();
        foreach (var pair in pairs)
        {
            addProgressToStorage(pair.Source.Metadata?.Size ?? 0, 0);
            await firstBlock.SendAsync(pair, cancellationToken);
        }
        firstBlock.Complete();

        while (!lastBlock.Completion.IsCompleted)
        {
            aggregateAndReportByteProgress();
            await Task.WhenAny(Task.Delay(500), lastBlock.Completion);
        }

        await lastBlock.Completion; // throw exception if error
        aggregateAndReportByteProgress(); // report 100%
        progressStorage.Dispose();

        // convert concurrent collection to non-concurrent collection, 
        // for efficient accessing to its elements
        return new FishFileSyncResult(
            updatedFiles.ToArray(),
            identicalFiles.ToArray());

        (ITargetBlock<SyncFilePair>, IDataflowBlock) createBlock()
        {
            var comparerBlock = new TransformBlock<SyncFilePair, (bool, SyncFilePair)>(async pair =>
            {
                fileProgress?.Report(new FishFileProgressEventArgs(
                    FishFileProgressEventType.Start, progressedFiles, totalFiles, pair.Source.Path.SubPath));

                var areEqual = await comparer.AreEqual(pair, cancellationToken);
                return (areEqual, pair);

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = cancellationToken,
                EnsureOrdered = false
            });

            var copyBlock = new ActionBlock<(bool, SyncFilePair)>(async item =>
            {
                var (areEqual, pair) = item;
                if (areEqual)
                {
                    identicalFiles.Add(pair);
                    addProgressToStorage(0, pair.Source.Metadata?.Size ?? 0);
                }
                else
                {
                    var progressReporter = new SyncProgress<ByteProgress>(progress => 
                        addProgressToStorage(progress.TotalBytes, progress.ProgressedBytes));

                    await StreamProgressHelper.SyncFilePair(pair, progressReporter, cancellationToken);
                    updatedFiles.Add(pair);
                }

                Interlocked.Increment(ref progressedFiles);
                fileProgress?.Report(new FishFileProgressEventArgs(
                    FishFileProgressEventType.Done, progressedFiles, totalFiles, pair.Source.Path.SubPath));
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = cancellationToken,
                EnsureOrdered = false
            });

            comparerBlock.LinkTo(copyBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });

            return (comparerBlock, copyBlock);
        }

        void addProgressToStorage(long total, long progressed)
        {
            var storedProgress = progressStorage.Value;
            progressStorage.Value = new ByteProgress
            {
                TotalBytes = storedProgress.TotalBytes + total,
                ProgressedBytes = storedProgress.ProgressedBytes + progressed
            };
        }

        void aggregateAndReportByteProgress()
        {
            if (byteProgress == null)
                return;

            long aggregatedTotalBytes = 0;
            long aggregatedProgressedBytes = 0;
            foreach (var progress in progressStorage.Values)
            {
                aggregatedTotalBytes += progress.TotalBytes;
                aggregatedProgressedBytes += progress.ProgressedBytes;
            }

            byteProgress.Report(new ByteProgress
            {
                TotalBytes = aggregatedTotalBytes,
                ProgressedBytes = aggregatedProgressedBytes
            });
        }
    }
}