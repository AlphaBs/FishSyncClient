using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public interface IFileComparer
{
    ValueTask<bool> AreEqual(SyncFilePair pair);
}
