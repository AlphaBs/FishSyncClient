using System.Threading.Tasks.Dataflow;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public class FilePairSyncer
{
    private readonly int _maxParallelism;

    public FilePairSyncer(int maxParallelism) => _maxParallelism = maxParallelism;

    public async Task SyncFilePairs(IEnumerable<SyncFilePair> pairs, SyncerOptions options)
    {
        int total = 0;
        int progressed = 0;

        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            options.FileProgress?.Report(new FileProgressEvent(FileProgressEventType.StartSync, progressed, total, pair.Source.Path.SubPath));
            await pair.SyncContent(options.ByteProgress, options.CancellationToken);

            Interlocked.Increment(ref progressed);
            options.FileProgress?.Report(new FileProgressEvent(FileProgressEventType.DoneSync, progressed, total, pair.Source.Path.SubPath));
        }, new ExecutionDataflowBlockOptions
        {
            CancellationToken = options.CancellationToken,
            EnsureOrdered = false,
            MaxDegreeOfParallelism = _maxParallelism
        });

        foreach (var pair in pairs)
        {
            options.FileProgress?.Report(new FileProgressEvent(FileProgressEventType.Queue, progressed, total, pair.Source.Path.SubPath));
            options.ByteProgress?.Report(
                new SyncFileByteProgress(
                    pair.Source, 
                    new ByteProgress 
                    (
                        totalBytes: pair.Source.Metadata?.Size ?? 0, 
                        progressedBytes: 0
                    )));

            Interlocked.Increment(ref total);
            await block.SendAsync(pair);
        }
        block.Complete();
        await block.Completion;
    }
}