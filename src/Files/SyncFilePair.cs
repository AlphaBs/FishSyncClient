using FishSyncClient.Internals;
using FishSyncClient.Progress;

namespace FishSyncClient.Files;

public struct SyncFilePair
{
    public SyncFilePair(SyncFile source, SyncFile target) =>
        (Source, Target) = (source, target);

    public SyncFile Source;
    public SyncFile Target;

    public async Task SyncContent(
        IProgress<SyncFileByteProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        var source = Source;
        var progressReporter = new SyncProgress<ByteProgress>(byteProgress =>
            progress?.Report(new SyncFileByteProgress(source, byteProgress)));
        using var targetStream = await Target.OpenWriteStream(cancellationToken);
        await source.CopyTo(targetStream, progressReporter, cancellationToken);
    }
}