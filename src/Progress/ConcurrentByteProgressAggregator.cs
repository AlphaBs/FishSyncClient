namespace FishSyncClient.Progress;

public class ConcurrentByteProgressAggregator : IProgress<ByteProgress>
{
    private readonly ThreadLocal<ByteProgress> _progressStorage = new(() => new ByteProgress(), true);

    public void Report(ByteProgress value)
    {
        _progressStorage.Value += value;
    }

    public ByteProgress AggregateProgress()
    {
        long aggregatedTotalBytes = 0;
        long aggregatedProgatedBytes = 0;

        foreach (var progress in _progressStorage.Values)
        {
            aggregatedTotalBytes += progress.TotalBytes;
            aggregatedProgatedBytes += progress.ProgressedBytes;
        }

        return new ByteProgress
        {
            TotalBytes = aggregatedTotalBytes,
            ProgressedBytes = aggregatedProgatedBytes
        };
    }
}
