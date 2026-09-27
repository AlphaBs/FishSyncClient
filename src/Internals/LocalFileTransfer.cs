using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;

namespace FishSyncClient.Internals;

internal static class LocalFileTransfer
{
    public static async Task CopyAndReplace(
        SyncFilePair pair,
        IFileComparer? verifier,
        IProgress<ByteProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destination = pair.Target.Path.GetFullPath();
        LocalPathGuard.EnsureNoLinks(destination);
        PathHelper.CreateParentDirectory(destination);
        LocalPathGuard.EnsureNoLinks(destination);

        // A sibling file keeps the final rename on the same filesystem.
        var directory = System.IO.Path.GetDirectoryName(destination)!;
        var temporaryName = $".fishsync-{Guid.NewGuid():N}.tmp";
        var temporaryPath = System.IO.Path.Combine(directory, temporaryName);
        var temporaryFile = new LocalSyncFile(RootedPath.Create(directory, temporaryName, new PathOptions()));
        var created = false;
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                await pair.Source.CopyTo(stream, progress, cancellationToken);
            }

            if (verifier != null && !await verifier.AreEqual(new SyncFilePair(pair.Source, temporaryFile), cancellationToken))
                throw new FileIntegrityException(pair.Target.Path.ToString());

            cancellationToken.ThrowIfCancellationRequested();
            LocalPathGuard.EnsureNoLinks(temporaryPath);
            LocalPathGuard.EnsureNoLinks(destination);
            if (File.Exists(destination))
                File.Replace(temporaryPath, destination, null);
            else
                File.Move(temporaryPath, destination);
        }
        finally
        {
            if (created)
            {
                // Never follow a directory replaced with a link while the transfer was running.
                LocalPathGuard.EnsureNoLinks(temporaryPath);
                File.Delete(temporaryPath);
            }
        }
    }
}
