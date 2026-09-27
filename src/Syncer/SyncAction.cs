namespace FishSyncClient.Syncer;

public enum SyncAction
{
    /// <summary>Install missing files, update changed files, and report target-only files for deletion.</summary>
    FullSync = 1,
    /// <summary>Install missing files and preserve all existing target files.</summary>
    InstallOnly = 2,
    /// <summary>Do not install, compare, update, or delete files.</summary>
    Exclude = 3,
    /// <summary>Install missing files and update changed files, but preserve target-only files.</summary>
    UpdateOnly = 4
}
