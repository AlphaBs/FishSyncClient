namespace FishSyncClient;

public interface IFileComparer
{
    ValueTask<bool> CompareFile(string path, FishFileMetadata file);
}
