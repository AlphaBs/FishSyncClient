namespace FishSyncClient.Files;

public interface ISyncFileCollection : IReadOnlyCollection<SyncFile>
{
    SyncFile? FindFileByPath(string path);
}