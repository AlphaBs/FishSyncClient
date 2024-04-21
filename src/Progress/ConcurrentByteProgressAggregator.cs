namespace FishSyncClient.Progress;

public class ConcurrentByteProgressAggregator : IProgress<ByteProgress>, IDisposable
{
    private static ThreadLocal<ByteProgress> createProgressStorage() => new(() => new ByteProgress(), true);

    private ThreadLocal<ByteProgress> _progressStorage = createProgressStorage();

    public void Report(ByteProgress value)
    {
        _progressStorage.Value += value;
    }

    public void Clear()
    {
        _progressStorage = createProgressStorage();
    }

    public ByteProgress AggregateProgress()
    {
        return _progressStorage.Values.Aggregate(new ByteProgress(), (a, b) => a + b);
    }

    public void Dispose() => _progressStorage.Dispose();
}
