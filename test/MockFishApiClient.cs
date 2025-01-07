using FishSyncClient.Server;

namespace FishSyncClientTest;

public class MockFishApiClient : IFishApiClient
{
    private readonly Dictionary<string, FishBucketFiles> _dict = new();

    public void Add(FishBucketFiles bucket)
    {
        if (string.IsNullOrEmpty(bucket.Id))
            throw new ArgumentException("Id was null or empty");
        _dict[bucket.Id] = bucket;
    }
    
    public Task<FishBucketFiles> GetBucketFiles(string id, CancellationToken cancellationToken = default)
    {
        var bucket = _dict[id];
        return Task.FromResult(bucket);
    }
}