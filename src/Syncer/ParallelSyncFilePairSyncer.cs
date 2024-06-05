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

    public async Task<SyncFilePairCollectionCompareResult> CompareFilePairs(IEnumerable<SyncFilePair> pairs, IFileComparer comparer, SyncerOptions options)
    {
        var identicalFiles = new ConcurrentBag<SyncFilePair>();
        var updatedFiles = new ConcurrentBag<SyncFilePair>();

        var processor = new SyncProcessor();
        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            options.FileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.StartCompare, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));

            var areEqual = await comparer.AreEqual(pair, options.CancellationToken);
            if (areEqual)
                identicalFiles.Add(pair);
            else
                updatedFiles.Add(pair);

            Interlocked.Increment(ref processor.ProgressedFiles);
            options.FileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.DoneSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
        });

        await processor.ProcessBlock(pairs, block, options);

        return new SyncFilePairCollectionCompareResult(
            updatedFiles.ToList(),
            identicalFiles.ToList());
    }

    public async Task SyncFilePairs(IEnumerable<SyncFilePair> pairs, SyncerOptions options)
    {
        var processor = new SyncProcessor();
        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            options.FileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.StartSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
            await pair.SyncContent(options.ByteProgress, options.CancellationToken);

            Interlocked.Increment(ref processor.ProgressedFiles);
            options.FileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.DoneSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
        });

        await processor.ProcessBlock(pairs, block, options);
    }

    public async Task<SyncFilePairCollectionCompareResult> CompareAndSyncFilePairs(
        IEnumerable<SyncFilePair> pairs,
        IFileComparer comparer,
        SyncerOptions options)
    {
        var identicalFiles = new ConcurrentBag<SyncFilePair>();
        var updatedFiles = new ConcurrentBag<SyncFilePair>();

        var processor = new SyncProcessor();
        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            options.FileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.StartSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));

            var areEqual = await comparer.AreEqual(pair, options.CancellationToken);
            if (areEqual)
                identicalFiles.Add(pair);
            else
            {
                await syncContent(pair, comparer, options);
                updatedFiles.Add(pair);
            }

            Interlocked.Increment(ref processor.ProgressedFiles);
            options.FileProgress?.Report(new FileProgressEvent(
                FileProgressEventType.DoneSync, processor.ProgressedFiles, processor.TotalFiles, pair.Source.Path.SubPath));
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = options.CancellationToken,
            EnsureOrdered = false
        });

        await processor.ProcessBlock(pairs, block, options);

        return new SyncFilePairCollectionCompareResult(
            updatedFiles.ToList(),
            identicalFiles.ToList()
        );

        static async Task syncContent(SyncFilePair pair, IFileComparer comparer, SyncerOptions options)
        {
            int failCount = 0;
            while (failCount < 3)
            {
                await pair.SyncContent(options.ByteProgress, options.CancellationToken);
                var areEqual = await comparer.AreEqual(pair, options.CancellationToken);
                if (areEqual)
                    return;
                else
                    failCount++;
            }

            throw new Exception();
        }
    }

    class SyncProcessor
    {
        public int TotalFiles;
        public int ProgressedFiles;

        public async Task ProcessBlock(IEnumerable<SyncFilePair> pairs, ActionBlock<SyncFilePair> block, SyncerOptions options)
        {
            var totalFiles = 0;
            var progressedFiles = 0;

            foreach (var pair in pairs)
            {
                Interlocked.Increment(ref totalFiles);
                options.FileProgress?.Report(new FileProgressEvent(
                    FileProgressEventType.Queue, progressedFiles, totalFiles, pair.Source.Path.SubPath));
                options.ByteProgress?.Report(
                    new SyncFileByteProgress(
                        pair.Source,
                        new ByteProgress
                        (
                            totalBytes: pair.Source.Metadata?.Size ?? 0,
                            progressedBytes: 0
                        )));

                await block.SendAsync(pair);
            }

            block.Complete();
            await block.Completion;
        }
    }
}