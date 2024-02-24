using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public interface IFileComparer
{
    ValueTask<bool> CompareFile(SyncFilePair pair);
}
