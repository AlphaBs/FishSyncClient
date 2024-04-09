namespace FishSyncClient;

public struct ByteProgress
{
    public long TotalBytes;
    public long ProgressedBytes;

    public double GetPercentage() => (double)ProgressedBytes / TotalBytes * 100;
}