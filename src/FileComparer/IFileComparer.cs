using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public interface IFileComparer
{
    ValueTask<bool> CompareFile(FishPathPair pair);
}
