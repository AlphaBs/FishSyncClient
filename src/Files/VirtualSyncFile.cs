using FishSyncClient.Progress;

namespace FishSyncClient.Files;

public class VirtualSyncFile : SyncFile
{
    public VirtualSyncFile(RootedPath path) : base(path)
    {
    }

    public override bool IsReadable => false;
    public override bool IsWritable => false;

    public override Task CopyTo(Stream destination, IProgress<ByteProgress>? progress, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
