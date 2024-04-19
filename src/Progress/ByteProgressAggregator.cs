namespace FishSyncClient.Progress;

public class ByteProgressAggregator : IProgress<ByteProgress>
{
    private readonly Action<ByteProgress> _reporter;

    public ByteProgressAggregator(Action<ByteProgress> reporter)
    {
        _reporter = reporter;
    }

    private ByteProgress currentProgress;

    public void Report(ByteProgress value)
    {
        if (value.TotalBytes == 0 && value.ProgressedBytes == 0)
            return;

        currentProgress += value;
        _reporter.Invoke(currentProgress);
    }
}