namespace FishSyncClient.FileComparers;

public interface IFileComparer
{
    ValueTask<bool> CompareFile(string path, FishFileMetadata file);
}
