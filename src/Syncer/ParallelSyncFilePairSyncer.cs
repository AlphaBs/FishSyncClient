using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace FishSyncClient.Syncer;

public class ParallelSyncFilePairSyncer : ISyncFilePairSyncer
{
    private readonly int _maxDegreeOfParallelism;

    public ParallelSyncFilePairSyncer()
    {
        var processors = Environment.ProcessorCount;
        processors = Math.Max(1, processors);
        processors = Math.Min(4, processors);

        _maxDegreeOfParallelism = processors;
    }

    public ParallelSyncFilePairSyncer(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public async Task<SyncFilePairCollectionCompareResult> CompareFilePairs(
        IEnumerable<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FileProgressEvent>? fileProgress,
        IProgress<SyncFileByteProgress>? byteProgress,
        CancellationToken cancellationToken)
    {
        var identicalFiles = new ConcurrentBag<SyncFilePair>();
        var updatedFiles = new ConcurrentBag<SyncFilePair>();

        var processor = new SyncProcessor();
        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.StartCompare, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
                identicalFiles.Add(pair);
            else
                updatedFiles.Add(pair);

            Interlocked.Increment(ref processor.ProgressedFiles);
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.DoneCompare, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
        }, CreateBlockOptions(cancellationToken));

        await processor.ProcessBlock(pairs, block, fileProgress, byteProgress, cancellationToken);

        return new SyncFilePairCollectionCompareResult(
            updatedFiles.ToList(),
            identicalFiles.ToList());
    }

    public async Task SyncFilePairs(
        IEnumerable<SyncFilePair> pairs,
        IProgress<FileProgressEvent>? fileProgress,
        IProgress<SyncFileByteProgress>? byteProgress,
        CancellationToken cancellationToken)
    {
        var processor = new SyncProcessor();
        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.StartSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
            await pair.SyncContent(byteProgress, cancellationToken);

            Interlocked.Increment(ref processor.ProgressedFiles);
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.DoneSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
        }, CreateBlockOptions(cancellationToken));

        await processor.ProcessBlock(pairs, block, fileProgress, byteProgress, cancellationToken);
    }

    public async Task<SyncFilePairCollectionCompareResult> CompareAndSyncFilePairs(
        IEnumerable<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FileProgressEvent>? fileProgress,
        IProgress<SyncFileByteProgress>? byteProgress,
        CancellationToken cancellationToken)
    {
        var identicalFiles = new ConcurrentBag<SyncFilePair>();
        var updatedFiles = new ConcurrentBag<SyncFilePair>();

        var processor = new SyncProcessor();
        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.StartSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
            {
                var size = pair.Source.Metadata?.Size ?? 0;
                byteProgress?.Report(new SyncFileByteProgress(pair.Source, new ByteProgress(0, size)));
                identicalFiles.Add(pair);
            }
            else
            {
                await pair.SyncContent(comparer, byteProgress, cancellationToken);
                updatedFiles.Add(pair);
            }

            Interlocked.Increment(ref processor.ProgressedFiles);
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.DoneSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
        }, CreateBlockOptions(cancellationToken));

        await processor.ProcessBlock(pairs, block, fileProgress, byteProgress, cancellationToken);

        return new SyncFilePairCollectionCompareResult(
            updatedFiles.ToList(),
            identicalFiles.ToList()
        );
    }

    private ExecutionDataflowBlockOptions CreateBlockOptions(CancellationToken cancellationToken)
    {
        return new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken,
            EnsureOrdered = false
        };
    }

    class SyncProcessor
    {
        public int TotalFiles = 0;
        public int ProgressedFiles = 0;

        public async Task ProcessBlock(
            IEnumerable<SyncFilePair> pairs,
            ActionBlock<SyncFilePair> block,
            IProgress<FileProgressEvent>? fileProgress,
            IProgress<SyncFileByteProgress>? byteProgress,
            CancellationToken cancellationToken)
        {
            try
            {
                foreach (var pair in pairs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref TotalFiles);
                    fileProgress?.Report(new FileProgressEvent(
                        FileProgressEventType.Queue, ProgressedFiles, TotalFiles, pair.Source.Path.SubPath));
                    byteProgress?.Report(new SyncFileByteProgress(
                        pair.Source, new ByteProgress(pair.Source.Metadata?.Size ?? 0, 0)));

                    if (!await block.SendAsync(pair, cancellationToken))
                        break;
                }
            }
            catch (Exception exception)
            {
                ((IDataflowBlock)block).Fault(exception);
                try
                {
                    await block.Completion;
                }
                catch
                {
                    // Observe worker failure, then preserve the original enqueue/iteration exception.
                }
                throw;
            }

            block.Complete();
            await block.Completion;
        }
    }
}
