using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class FileChecksumMetadataComparer : IFileComparer
{
    private readonly ComparerErrorHandlingModes _errorMode;

    public FileChecksumMetadataComparer() : this(ComparerErrorHandlingModes.ThrowException)
    {

    }

    public FileChecksumMetadataComparer(ComparerErrorHandlingModes mode)
    {
        _errorMode = mode;
    }

    public ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        var areEqual = compare(pair.Source, pair.Target);
        return new ValueTask<bool>(areEqual);
    }

    private bool compare(SyncFile source, SyncFile target)
    {
        if (source.Metadata?.Checksum?.AlgorithmName != target.Metadata?.Checksum?.AlgorithmName)
        {
            switch (_errorMode)
            {
                case ComparerErrorHandlingModes.ReturnEqual:
                    return true;
                case ComparerErrorHandlingModes.ReturnNotEqual:
                    return false;
                case ComparerErrorHandlingModes.ThrowException:
                default:
                    throw new FileComparerException("Cannot compare checksum with different checksum algorithm");
            }
        }
        
        return source.Metadata?.Checksum?.ChecksumHexString == target.Metadata?.Checksum?.ChecksumHexString;
    }
}