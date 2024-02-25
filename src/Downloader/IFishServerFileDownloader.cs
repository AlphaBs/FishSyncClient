namespace FishSyncClient.Downloader;

public interface IFishServerFileDownloader
{
    ValueTask DownloadFiles(
        string root,
        IReadOnlyCollection<ServerSyncFile> serverFiles,
        IProgress<FishFileProgressEventArgs>? fileProgress,
        IProgress<ByteProgress>? byteProgress,
        CancellationToken cancellationToken);
}