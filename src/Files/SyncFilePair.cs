using FishSyncClient.FileComparers;
using FishSyncClient.Internals;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;

namespace FishSyncClient.Files;

public readonly struct SyncFilePair
{
    public SyncFilePair(SyncFile source, SyncFile target) =>
        (Source, Target) = (source, target);

    public readonly SyncFile Source;
    public readonly SyncFile Target;

    public Task SyncContent(
        IProgress<SyncFileByteProgress>? progress = null, 
        CancellationToken cancellationToken = default)
        => SyncContent(null, progress, cancellationToken);

    internal async Task SyncContent(
        IFileComparer? verifier,
        IProgress<SyncFileByteProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = Source;
        var progressReporter = new SyncProgress<ByteProgress>(byteProgress =>
            progress?.Report(new SyncFileByteProgress(source, byteProgress)));
        if (Target is LocalSyncFile)
        {
            await LocalFileTransfer.CopyAndReplace(this, verifier, progressReporter, cancellationToken);
            return;
        }

        using (var targetStream = await Target.OpenWriteStream(cancellationToken))
            await source.CopyTo(targetStream, progressReporter, cancellationToken);
        if (verifier != null && !await verifier.AreEqual(this, cancellationToken))
            throw new FileIntegrityException(Target.Path.ToString());
    }
}
