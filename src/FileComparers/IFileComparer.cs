using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public interface IFileComparer
{
    /// <summary>
    /// Returns true when the pair satisfies this comparer's policy and needs no content transfer.
    /// This does not necessarily establish byte-for-byte equality or verified integrity.
    /// Required metadata and responsibility for checking existence depend on the implementation.
    /// </summary>
    ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken);
}
