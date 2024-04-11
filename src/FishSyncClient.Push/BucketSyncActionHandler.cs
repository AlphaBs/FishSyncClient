namespace FishSyncClient.Push;

public class CompositeBucketSyncActionHandler : IBucketSyncActionHandler
{
    private readonly IEnumerable<IBucketSyncActionHandler> _actionHandlers;

    public CompositeBucketSyncActionHandler(IEnumerable<IBucketSyncActionHandler> handlers) =>
        _actionHandlers = handlers;

    public bool CanHandle(BucketSyncAction action)
    {
        return _actionHandlers.Any(handler => handler.CanHandle(action));
    }

    public ValueTask Handle(BucketSyncAction action)
    {
        var handler = _actionHandlers.First(handler => handler.CanHandle(action));
        return handler.Handle(action);
    }
}