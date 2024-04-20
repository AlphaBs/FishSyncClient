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
        return _progressStorage.Values.Aggregate((a, b) => a + b);
    }
}
