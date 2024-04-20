using System.Threading.Tasks.Dataflow;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClient.Server.BucketSyncActions;

public class SyncActionProgress
{
    public SyncActionProgress(FileProgressEventType eventType, BucketSyncAction action)
    {
        EventType = eventType;
        Action = action;
    }

    public FileProgressEventType EventType { get; set; }
    public BucketSyncAction Action { get; set; }
}

public class SyncActionByteProgress
{
    public string Path { get; set; }
    public ByteProgress Progress { get; set; }
}

public class SyncHandlerParameters
{
    public IBucketSyncActionHandler Handler { get; set; }
    public SyncFile File { get; set; }
    public BucketSyncAction Action { get; set; }
}

public class SimpleBucketSyncActionCollectionHandler : IBucketSyncActionCollectionHandler
{
    private readonly int _maxParallelism;
    private readonly List<IBucketSyncActionHandler> _handlers = new();
    private readonly IProgress<SyncActionProgress> _actionProgress;
    private readonly IProgress<SyncActionByteProgress> _byteProgress;

    public SimpleBucketSyncActionCollectionHandler(
        int maxParallelism,
        IProgress<SyncActionProgress> actionProgress,
        IProgress<SyncActionByteProgress> byteProgress)
    {
        _maxParallelism = maxParallelism;
        _actionProgress = actionProgress;
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

    public IReadOnlyDictionary<BucketSyncAction, IBucketSyncActionHandler?> PairActionAndHandler(IEnumerable<BucketSyncAction> actions)
    {
        return actions.ToDictionary(
            keySelector: action => action,
            elementSelector: action => _handlers.FirstOrDefault(handler => handler.CanHandle(action)))!;
    }

    public Task Handle(
        ISyncFileCollection files,
        IEnumerable<BucketSyncAction> actions,
        CancellationToken cancellationToken)
    {
        return handle(files, actions, _actionProgress, _byteProgress, cancellationToken);
    }

    private async Task handle(
        ISyncFileCollection files,
        IEnumerable<BucketSyncAction> actions,
        IProgress<SyncActionProgress> actionProgress,
        IProgress<SyncActionByteProgress> byteProgress,
        CancellationToken cancellationToken)
    {
        var actionHandlerPairs = PairActionAndHandler(actions);
        var requiredActions = actionHandlerPairs
            .Where(pair => pair.Value == null)
            .Select(pair => pair.Key)
            .ToList();
        if (requiredActions.Any())
            throw new ActionRequiredException(requiredActions);

        var handlerBlock = createHandlerBlock(actionProgress, byteProgress, cancellationToken);
        foreach (var pair in actionHandlerPairs)
        {
            var action = pair.Key;
            var handler = pair.Value;
            var file = files.FindFileByPath(action.Path);

            if (handler == null)
                throw new InvalidOperationException();
            if (file == null)
                throw new InvalidOperationException();

            actionProgress.Report(new SyncActionProgress(FileProgressEventType.Queue, action));
            await handlerBlock.SendAsync(new SyncHandlerParameters
            {
                Action = action,
                Handler = handler,
                File = file
            });
        }
        handlerBlock.Complete();
        await handlerBlock.Completion;
    }

    private ActionBlock<SyncHandlerParameters> createHandlerBlock(
        IProgress<SyncActionProgress> actionProgress,
        IProgress<SyncActionByteProgress> byteProgress,
        CancellationToken cancellationToken)
    {
        var block = new ActionBlock<SyncHandlerParameters>(async parameters =>
        {
            var progressReporter = new Progress<ByteProgress>(progress =>
            {
                byteProgress.Report(new SyncActionByteProgress
                {
                    Path = parameters.File.Path.SubPath,
                    Progress = progress
                });
            });

            actionProgress.Report(new SyncActionProgress(FileProgressEventType.StartSync, parameters.Action));
            await parameters.Handler.Handle(parameters.File, parameters.Action, progressReporter, cancellationToken);
            actionProgress.Report(new SyncActionProgress(FileProgressEventType.DoneSync, parameters.Action));
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = _maxParallelism,
            EnsureOrdered = false,
            CancellationToken = cancellationToken
        });

        return block;
    }
}