namespace FishSyncClient.Downloader;

public interface IFishServerFileDownloader
{
    ValueTask DownloadFiles(
        string root,
        IEnumerable<ServerSyncFile> serverFiles,
        IProgress<FishFileProgressEventArgs>? fileProgress,
        IProgress<ByteProgress>? byteProgress,
        CancellationToken cancellationToken);
}