namespace FishSyncClient.Downloader;

public class SequentialFileDownloader : IFishServerFileDownloader
{
    private readonly HttpClient _httpClient;

    public SequentialFileDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async ValueTask DownloadFiles(
        string root,
        IReadOnlyCollection<ServerSyncFile> serverFiles,
        IProgress<FishFileProgressEventArgs>? fileProgress,
        IProgress<ByteProgress>? byteProgress,
        CancellationToken cancellationToken)
    {
        long totalBytes = serverFiles.Select(f => f.Metadata?.Size ?? 0).Sum();
        long progressedBytes = 0;
        int progressed = 0;

        foreach (var file in serverFiles)
        {
            fileProgress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.Start, progressed, serverFiles.Count, file.Path));

            if (file.Location != null)
            {
                var progress = new ByteProgressDelta(file.Metadata?.Size ?? 0, p =>
                {
                    totalBytes += p.TotalBytes;
                    progressedBytes += p.ProgressedBytes;
                    byteProgress?.Report(new ByteProgress
                    {
                        TotalBytes = totalBytes,
                        ProgressedBytes = progressedBytes
                    });
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

            progressed++;
            fileProgress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.Done, progressed, serverFiles.Count, file.Path));
        }
    }
}