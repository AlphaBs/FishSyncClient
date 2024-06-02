using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class FileChecksumMetadataComparer : IFileComparer
{
    public ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        var areEqual = compare(pair.Source, pair.Target);
        return new ValueTask<bool>(areEqual);
    }

    private bool compare(SyncFile source, SyncFile target)
    {
        if (source.Metadata?.Checksum?.AlgorithmName != target.Metadata?.Checksum?.AlgorithmName)
            return false;
        
        return source.Metadata?.Checksum?.ChecksumHexString == target.Metadata?.Checksum?.ChecksumHexString;
    }
}