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
        IProgress<FishFileProgressEventArgs>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        var identicalFiles = new ConcurrentBag<SyncFilePair>();
        var updatedFiles = new ConcurrentBag<SyncFilePair>();

        var progressed = 0;
        RootedPath lastFilePath = new();

        var executor = new ActionBlock<SyncFilePair>(async pair => 
        {
            progress?.Report(new FishFileProgressEventArgs(progressed, pairs.Count, pair.Source.Path));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
                identicalFiles.Add(pair);
            else
                updatedFiles.Add(pair);

            Interlocked.Increment(ref progressed);
            lastFilePath = pair.Source.Path;

        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        });

        foreach (var pair in pairs)
        {
            await executor.SendAsync(pair, cancellationToken);
        }

        executor.Complete();
        await executor.Completion;

        progress?.Report(new FishFileProgressEventArgs(progressed, pairs.Count, lastFilePath));

        // convert concurrent collection to non-concurrent collection, 
        // for efficient accessing to its elements
        return new FishFileSyncResult(
            updatedFiles.ToArray(), 
            identicalFiles.ToArray());
    }
}