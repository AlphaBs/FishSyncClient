using FishSyncClient.Common;

namespace FishSyncClient.Push;

public class SimpleBucketSyncActionCollectionHandler : IBucketSyncActionCollectionHandler
{
    private readonly string _root;
    private readonly List<IBucketSyncActionHandler> _handlers = new();
    private readonly IProgress<FishFileProgressEventArgs> _fileProgress;
    private readonly IProgress<ByteProgress> _byteProgress;

    public SimpleBucketSyncActionCollectionHandler(
        string root,
        IProgress<FishFileProgressEventArgs> fileProgress,
        IProgress<ByteProgress> byteProgress)
    {
        _root = root;
        _fileProgress = fileProgress;
        _byteProgress = byteProgress;
    }

    public void Add(IBucketSyncActionHandler handler) => _handlers.Add(handler);

    public void AddRange(IEnumerable<IBucketSyncActionHandler> handlers)
    {
        foreach (var handler in handlers) 
        {
            Add(handler);
        }
    }

    public async Task Handle(IReadOnlyCollection<BucketSyncAction> actions, CancellationToken cancellationToken)
    {
        var actionHandlerMap = actions.ToDictionary(
            keySelector: action => action,
            elementSelector: action => _handlers.FirstOrDefault(handler => handler.CanHandle(action)));
        
        var requiredActions = actionHandlerMap
            .Where(kv => kv.Value == null)
            .Select(kv => kv.Key)
            .ToList();

        if (requiredActions.Any())
            throw new ActionRequiredException(requiredActions);

        int total = actions.Count;
        int progressed = 0;
        foreach (var kv in actionHandlerMap)
        {
            var action = kv.Key;
            var handler = kv.Value;

            _fileProgress.Report(new FishFileProgressEventArgs(
                type: FishFileProgressEventType.Start,
                progressed: progressed,
                total: total,
                current: action.Path
            ));

            using var fs = File.OpenRead(Path.Combine(_root, action.Path));
            await handler.Handle(fs, action, _byteProgress, cancellationToken);

            progressed++;
            _fileProgress.Report(new FishFileProgressEventArgs(
                type: FishFileProgressEventType.Done,
                progressed: progressed,
                total: total,
                current: action.Path
            ));
        }
    }
}