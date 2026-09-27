using FishSyncClient.Progress;

namespace FishSyncClient.Files;

public abstract class SyncFile
{
    public SyncFile(RootedPath path)
    {
        Path = path;
    }

    public RootedPath Path { get; }

    /// <summary>
    /// 생성 시 정하는 기대 메타데이터다. null이면 메타데이터를 제공하지 않은 상태다.
    /// 파일의 동등성 및 해시 계산에 사용하므로 생성 후 교체하지 않는다.
    /// </summary>
    public SyncFileMetadata? Metadata { get; init; }

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
