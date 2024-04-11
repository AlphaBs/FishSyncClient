namespace FishSyncClient.Common;

public struct ByteProgress
{
    public long TotalBytes;
    public long ProgressedBytes;

    public double GetPercentage() => (double)ProgressedBytes / TotalBytes * 100;
}