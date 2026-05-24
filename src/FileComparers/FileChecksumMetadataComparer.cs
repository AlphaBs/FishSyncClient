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
        var sourceChecksum = source.Metadata?.Checksum;
        var targetChecksum = target.Metadata?.Checksum;

        if (!hasChecksum(sourceChecksum))
            return true;

        if (!hasChecksum(targetChecksum))
            return handleCannotCompare();

        var sourceChecksumValue = sourceChecksum.GetValueOrDefault();
        var targetChecksumValue = targetChecksum.GetValueOrDefault();

        if (sourceChecksumValue.AlgorithmName != targetChecksumValue.AlgorithmName)
        {
            return handleCannotCompare();
        }
        
        return sourceChecksumValue.ChecksumHexString == targetChecksumValue.ChecksumHexString;
    }

    private static bool hasChecksum(SyncFileChecksum? checksum)
    {
        return checksum.HasValue &&
            !string.IsNullOrEmpty(checksum.Value.AlgorithmName) &&
            !string.IsNullOrEmpty(checksum.Value.ChecksumHexString);
    }

    private bool handleCannotCompare()
    {
        switch (_errorMode)
        {
            case ComparerErrorHandlingModes.ReturnEqual:
                return true;
            case ComparerErrorHandlingModes.ReturnNotEqual:
                return false;
            case ComparerErrorHandlingModes.ThrowException:
            default:
                throw new FileComparerException("Cannot compare checksum");
        }
    }
}
