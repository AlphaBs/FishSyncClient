using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public record FishFileSyncResult(
    IReadOnlyCollection<SyncFilePair> UpdatedFiles,
    IReadOnlyCollection<SyncFilePair> IdenticalFiles
);