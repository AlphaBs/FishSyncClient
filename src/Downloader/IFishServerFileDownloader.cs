namespace FishSyncClient.Downloader;

public interface IFishServerFileDownloader
{
    ValueTask DownloadFiles(
        string root,
        IEnumerable<FishServerFile> serverFiles,
        IProgress<FishFileProgressEventArgs>? fileProgress,
        IProgress<ByteProgress>? byteProgress,
        CancellationToken cancellationToken);
}