namespace FishSyncClient.Versions;

public interface IVersionManager
{
    Task<bool> CheckNewVersion(string sourceVersion);
    Task<string?> GetCurrentVersion();
    Task UpdateVersion(string newVersion);
}