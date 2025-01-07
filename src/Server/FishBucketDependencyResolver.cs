namespace FishSyncClient.Server;

public class FishBucketDependencyResolver
{
    class State
    {
        public State(IFishApiClient client, int maxDeps) => 
            (ApiClient, MaxDeps) = (client, maxDeps);
        
        public int MaxDeps { get; set; }
        public IFishApiClient ApiClient { get; }
        public List<string> VisitHistory { get; } = new();
        public Dictionary<string, FishBucketFile> Files { get; } = new();
        public FishBucketFiles? Root { get; set; }
    }

    public static async Task<FishBucketFiles> Resolve(IFishApiClient client, string id, int maxDeps = 8, CancellationToken cancellationToken = default)
    {
        var state = new State(client, maxDeps);
        state.VisitHistory.Add(id);
        await resolve(id, state, cancellationToken);

        if (state.Root == null)
            throw new InvalidOperationException("No root found");
        
        return new FishBucketFiles
        {
            Id = state.Root.Id,
            Dependencies = state.VisitHistory,
            LastUpdated = state.Root.LastUpdated,
            Files = state.Files.Values.ToList()
        };
    }

    private static async Task resolve(string id, State state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var current = await state.ApiClient.GetBucketFiles(id, cancellationToken);
        if (state.Root == null)
            state.Root = current;

        if (state.VisitHistory.Count() >= state.MaxDeps)
            throw new InvalidOperationException("Exceeded max dependencies ({state.MaxDeps}) at ID: {id}");
        
        foreach (var dep in current.Dependencies)
        {
            if (!state.VisitHistory.Contains(dep))
            {
                state.VisitHistory.Add(dep);
                await resolve(dep, state, cancellationToken);
            }
        }
        
        foreach (var file in current.Files)
        {
            state.Files[file.Path] = file;
        }
    }
}