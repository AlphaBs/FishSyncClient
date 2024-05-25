namespace FishSyncClient.Progress;

public readonly struct ByteProgress
{
    public readonly long TotalBytes;
    public readonly long ProgressedBytes;

    public ByteProgress(long totalBytes, long progressedBytes)
    {
        TotalBytes = totalBytes;
        ProgressedBytes = progressedBytes;
    }

    public double GetRatio() => (double)ProgressedBytes / TotalBytes;

    public static ByteProgress operator +(ByteProgress a, ByteProgress b)
    {
        return new ByteProgress
        (
            totalBytes: a.TotalBytes + b.TotalBytes,
            progressedBytes: a.ProgressedBytes + b.ProgressedBytes
        );
    }

    public static ByteProgress operator -(ByteProgress a, ByteProgress b)
    {
        return new ByteProgress
        (
            totalBytes: a.TotalBytes - b.TotalBytes,
            progressedBytes: b.TotalBytes - b.TotalBytes
        );
    }
}