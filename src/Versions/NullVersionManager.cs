
namespace FishSyncClient.Versions;

public class NullVersionManager : IVersionManager
{
    public Task<bool> CheckNewVersion(string? sourceVersion)
    {
        return Task.FromResult(true);
    }

    public Task<string?> GetCurrentVersion()
    {
        return Task.FromResult<string?>(null);
    }

    public Task UpdateVersion(string newVersion)
    {
        return Task.CompletedTask;
    }
}