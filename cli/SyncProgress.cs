namespace FishSyncClient.Cli;

public class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _action;

    public SyncProgress(Action<T> action)
    {
        _action = action;
    }

    public void Report(T value)
    {
        _action.Invoke(value);
    }
}