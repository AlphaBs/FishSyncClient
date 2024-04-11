namespace FishSyncClient.Push;

[System.Serializable]
public class ActionRequiredException : Exception
{
    public ActionRequiredException(IReadOnlyCollection<BucketSyncAction> actions) => 
        Actions = actions;
        
    public ActionRequiredException(IReadOnlyCollection<BucketSyncAction> actions, string message) : base(message) => 
        Actions = actions;

    public ActionRequiredException(IReadOnlyCollection<BucketSyncAction> actions, string message, Exception inner) : base(message, inner) =>
        Actions = actions;

    public IReadOnlyCollection<BucketSyncAction> Actions { get; }
}