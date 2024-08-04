using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;

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
                FileProgressEventType.DoneSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
        });

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
        });

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
                await syncContent(pair, comparer, byteProgress, cancellationToken);
                updatedFiles.Add(pair);
            }

            Interlocked.Increment(ref processor.ProgressedFiles);
            fileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.DoneSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken,
            EnsureOrdered = false
        });

        await processor.ProcessBlock(pairs, block, fileProgress, byteProgress, cancellationToken);

        return new SyncFilePairCollectionCompareResult(
            updatedFiles.ToList(),
            identicalFiles.ToList()
        );

        static async Task syncContent(
            SyncFilePair pair, 
            IFileComparer comparer, 
            IProgress<SyncFileByteProgress>? byteProgress, 
            CancellationToken cancellationToken)
        {
            int failCount = 0;
            while (failCount < 3)
            {
                await pair.SyncContent(byteProgress, cancellationToken);
                var areEqual = await comparer.AreEqual(pair, cancellationToken);
                if (areEqual)
                    return;
                else
                {
                    failCount++;
                }
            }

            throw new Exception();
        }
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
            foreach (var pair in pairs)
            {
                Interlocked.Increment(ref TotalFiles);
                fileProgress?.Report(new FileProgressEvent(
                    FileProgressEventType.Queue, ProgressedFiles, TotalFiles, pair.Source.Path.SubPath));
                byteProgress?.Report(
                    new SyncFileByteProgress(
                        pair.Source,
                        new ByteProgress
                        (
                            totalBytes: pair.Source.Metadata?.Size ?? 0,
                            progressedBytes: 0
                        )));

                await block.SendAsync(pair, cancellationToken);
            }

            block.Complete();
            await block.Completion;
        }
    }
}