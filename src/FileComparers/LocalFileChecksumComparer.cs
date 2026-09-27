using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class LocalFileChecksumComparer : IFileComparer
{
    public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetLocalFile = pair.Target as LocalSyncFile;
        if (targetLocalFile == null)
            throw new FileComparerException("Target should be LocalSyncFile");
        if (!targetLocalFile.Exists)
            return false;

        var sourceChecksum = pair.Source.Metadata?.Checksum?.ChecksumHexString;
        var sourceChecksumAlgorithmName = pair.Source.Metadata?.Checksum?.AlgorithmName;
        if (string.IsNullOrEmpty(sourceChecksum) || string.IsNullOrEmpty(sourceChecksumAlgorithmName))
            return true;

        using var readStream = await targetLocalFile.OpenReadStream(cancellationToken);
        var targetChecksum = await ChecksumAlgorithms.ComputeHashAsync(sourceChecksumAlgorithmName, readStream, cancellationToken);
        return string.Equals(sourceChecksum, targetChecksum, StringComparison.OrdinalIgnoreCase);
    }
}
