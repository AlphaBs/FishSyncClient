using FishSyncClient.Internals;
using FishSyncClient.Progress;

namespace FishSyncClient.Files;

public class LocalSyncFile : SyncFile
{
    public LocalSyncFile(RootedPath path) : base(path)
    {
    }

    public override bool IsReadable => Path.IsRooted;
    public override bool IsWritable => Path.IsRooted;

    public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken)
    {
        var stream = File.OpenRead(Path.GetFullPath());
        return new ValueTask<Stream>(stream);
    }

    public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath();
        PathHelper.CreateParentDirectory(fullPath);
        var stream = File.Create(fullPath);
        return new ValueTask<Stream>(stream);
    }

    public override async Task CopyTo(Stream destination, IProgress<ByteProgress>? progress, CancellationToken cancellationToken)
    {
        using var sourceStream = await OpenReadStream(cancellationToken);
        await StreamProgressHelper.CopyStreamWithPeriodicProgress(
            sourceStream, 
            destination, 
            100, 
            new SyncProgress<long>(read =>
                progress?.Report(new ByteProgress(0, read))), 
            cancellationToken);
    }
}
