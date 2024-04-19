using FishSyncClient.Progress;

namespace FishSyncClient.Files;

public abstract class SyncFile
{
    public SyncFile(RootedPath path)
    {
        Path = path;
    }

    public RootedPath Path { get; }
    public SyncFileMetadata? Metadata { get; set; }

    public abstract bool IsReadable { get; }
    public abstract bool IsWritable { get; }
    public abstract ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default);
    public abstract ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default);
    public abstract Task CopyTo(Stream destination, IProgress<ByteProgress>? progress, CancellationToken cancellationToken);

    public override string ToString()
    {
        return Path.ToString();
    }

    public override bool Equals(object obj)
    {
        if (obj is SyncFile syncFile)
        {
            return Path.Equals(syncFile.Path) && Metadata == syncFile.Metadata;
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        return Path.GetHashCode() ^ (Metadata?.GetHashCode() ?? 0);
    }
}