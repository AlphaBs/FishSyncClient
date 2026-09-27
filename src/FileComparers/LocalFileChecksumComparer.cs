using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

/// <summary>
/// Checks the target's existence and, when supplied, the source's checksum.
/// An omitted source checksum accepts any existing target without reading its content or size.
/// A supplied checksum must be valid even when the target does not exist.
/// </summary>
public class LocalFileChecksumComparer : IFileComparer
{
    public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetLocalFile = pair.Target as LocalSyncFile;
        if (targetLocalFile == null)
            throw new FileComparerException("Target should be LocalSyncFile");

        var sourceChecksum = pair.Source.Metadata?.Checksum;
        if (sourceChecksum.HasValue)
            ChecksumMetadataValidator.Validate(sourceChecksum.Value, "source");

        if (!targetLocalFile.Exists)
            return false;

        if (!sourceChecksum.HasValue)
            return true;

        using var readStream = await targetLocalFile.OpenReadStream(cancellationToken);
        var targetChecksum = await ChecksumAlgorithms.ComputeHashAsync(sourceChecksum.Value.AlgorithmName, readStream, cancellationToken);
        return string.Equals(sourceChecksum.Value.ChecksumHexString, targetChecksum, StringComparison.OrdinalIgnoreCase);
    }
}
