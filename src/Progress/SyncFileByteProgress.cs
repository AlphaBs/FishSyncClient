using FishSyncClient.Files;

namespace FishSyncClient.Progress;

public class SyncFileByteProgress
{
    public SyncFileByteProgress(SyncFile syncFile, ByteProgress progress)
    {
        SyncFile = syncFile;
        Progress = progress;
    }

    public SyncFile SyncFile { get; }
    public ByteProgress Progress { get; }
}