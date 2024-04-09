using System.Threading.Tasks.Dataflow;

namespace FishSyncClient.Downloader;

public class ParallelFileDownloader : IFishServerFileDownloader
{
    private readonly HttpClient _httpClient;
    private readonly int _maxDegreeOfParallelism;

    public ParallelFileDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;

        var processors = Environment.ProcessorCount;
        processors = Math.Max(2, processors);
        processors = Math.Min(6, processors);
        _maxDegreeOfParallelism = processors;
    }

    public ParallelFileDownloader(HttpClient httpClient, int maxDegreeOfParallelism)
    {
        _httpClient = httpClient;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public async ValueTask DownloadFiles(
        string root, 
        IReadOnlyCollection<ServerSyncFile> serverFiles, 
        IProgress<FishFileProgressEventArgs>? fileProgress, 
        IProgress<ByteProgress>? byteProgress, 
        CancellationToken cancellationToken)
    {
        var progressStorage = new ThreadLocal<ByteProgress>(
            () => new ByteProgress(), true);
        var totalBytes = serverFiles.Select(file => file.Metadata?.Size ?? 0).Sum();
        var progressedFiles = 0;

        var executor = new ActionBlock<ServerSyncFile>(async file =>
        {
            fileProgress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.Start, progressedFiles, serverFiles.Count, file.Path));
            
            if (file.Location != null)
            {
                var progress = new ByteProgressDelta(file.Metadata?.Size ?? 0, p =>
                {
                    var previousProgress = progressStorage.Value;
                    progressStorage.Value = new ByteProgress
                    {
                        TotalBytes = p.TotalBytes + previousProgress.TotalBytes,
                        ProgressedBytes = p.ProgressedBytes + previousProgress.ProgressedBytes
                    };
                });

                var dest = file.Path.WithRoot(root).GetFullPath();
                await HttpClientDownloadHelper.DownloadFileAsync(
                    _httpClient,
                    file.Location,
                    file.Metadata?.Size ?? 0,
                    dest,
                    progress,
                    cancellationToken);
            }

            Interlocked.Increment(ref progressedFiles);
            fileProgress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.Done, progressedFiles, serverFiles.Count, file.Path));

        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        });

        foreach (var file in serverFiles)
        {
            await executor.SendAsync(file, cancellationToken);
        }
        executor.Complete();

        while (!executor.Completion.IsCompleted)
        {
            await Task.WhenAny(executor.Completion, Task.Delay(1000));

            long aggregatedTotalBytes = totalBytes;
            long aggregatedProgressedBytes = 0;

            foreach (var progress in progressStorage.Values)
            {
                aggregatedTotalBytes += progress.TotalBytes;
                aggregatedProgressedBytes += progress.ProgressedBytes;
            }

            byteProgress?.Report(new ByteProgress
            {
                TotalBytes = aggregatedTotalBytes,
                ProgressedBytes = aggregatedProgressedBytes
            });
        }

        await executor.Completion;
    }
}