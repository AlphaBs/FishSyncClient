using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class LocalFileChecksumComparer : IFileComparer
{
    public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        var sourceChecksum = pair.Source.Metadata?.Checksum?.ChecksumHexString;
        var sourceChecksumAlgorithmName = pair.Source.Metadata?.Checksum?.AlgorithmName;
        if (string.IsNullOrEmpty(sourceChecksum) || string.IsNullOrEmpty(sourceChecksumAlgorithmName))
            return true;
        
        var targetLocalFile = pair.Target as LocalSyncFile;
        if (targetLocalFile == null)
            throw new FileComparerException("Target should be LocalSyncFile");
        if (!targetLocalFile.Exists)
            return false;

        var targetChecksum = await getChecksum(sourceChecksumAlgorithmName, targetLocalFile);
        var areEqual = sourceChecksum == targetChecksum;
        return areEqual;
    }

    private async ValueTask<string> getChecksum(string algName, LocalSyncFile file)
    {
        using var readStream = await file.OpenReadStream(default);
        return ChecksumAlgorithms.ComputeHash(algName, readStream);
    }
}