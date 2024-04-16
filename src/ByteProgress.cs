namespace FishSyncClient;

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
}