namespace FishSyncClient.Progress;

public struct ByteProgress
{
    public long TotalBytes;
    public long ProgressedBytes;

    public ByteProgress(long totalBytes, long progressedBytes)
    {
        TotalBytes = totalBytes;
        ProgressedBytes = progressedBytes;
    }

    public double GetPercentage(bool hundred) => (double)ProgressedBytes / TotalBytes * (hundred ? 100 : 1);

    public static ByteProgress operator +(ByteProgress a, ByteProgress b)
    {
        return new ByteProgress
        {
            TotalBytes = a.TotalBytes + b.TotalBytes,
            ProgressedBytes = a.ProgressedBytes + b.ProgressedBytes
        };
    }

    public static ByteProgress operator -(ByteProgress a, ByteProgress b)
    {
        return new ByteProgress
        {
            TotalBytes = a.TotalBytes - b.TotalBytes,
            ProgressedBytes = b.TotalBytes - b.TotalBytes
        };
    }
}