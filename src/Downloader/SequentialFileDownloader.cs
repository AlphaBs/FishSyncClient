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
        IEnumerable<ServerSyncFile> serverFiles, 
        IProgress<FishFileProgressEventArgs>? fileProgress, 
        IProgress<ByteProgress>? byteProgress,
        CancellationToken cancellationToken)
    {
        var filesArr = serverFiles.ToArray();
        long totalBytes = filesArr.Select(f => f.Metadata?.Size ?? 0).Sum();
        long progressedBytes = 0;

        for (int i = 0; i < filesArr.Length; i++)
        {
            fileProgress?.Report(new FishFileProgressEventArgs(i, filesArr.Length, filesArr[i].Path));

            var file = filesArr[i];
            var dest = file.Path.WithRoot(root).GetFullPath();
        
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

            await HttpClientDownloadHelper.DownloadFileAsync(
                _httpClient, 
                file.Location.ToString(), 
                file.Metadata?.Size ?? 0,
                dest,
                progress);
        }

        if (filesArr.Any())
            fileProgress?.Report(new FishFileProgressEventArgs(filesArr.Length, filesArr.Length, filesArr.Last().Path));
    }
}