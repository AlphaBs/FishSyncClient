using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

/// <summary>
/// Compares checksum metadata for files whose existence is established by the caller.
/// This comparer performs no storage access and must not be used to detect missing targets.
/// An omitted source checksum imposes no content requirement and returns true in every error mode.
/// When the source checksum is present, invalid checksums throw; the error mode applies to
/// an absent target checksum or different supported algorithms.
/// </summary>
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
        cancellationToken.ThrowIfCancellationRequested();
        var areEqual = compare(pair.Source, pair.Target);
        return new ValueTask<bool>(areEqual);
    }

    private bool compare(SyncFile source, SyncFile target)
    {
        var sourceChecksum = source.Metadata?.Checksum;
        var targetChecksum = target.Metadata?.Checksum;

        if (!sourceChecksum.HasValue)
            return true;

        ChecksumMetadataValidator.Validate(sourceChecksum.Value, "source");

        if (!targetChecksum.HasValue)
            return handleCannotCompare();

        ChecksumMetadataValidator.Validate(targetChecksum.Value, "target");
        var sourceChecksumValue = sourceChecksum.Value;
        var targetChecksumValue = targetChecksum.Value;

        if (sourceChecksumValue.AlgorithmName != targetChecksumValue.AlgorithmName)
        {
            return handleCannotCompare();
        }
        
        return string.Equals(sourceChecksumValue.ChecksumHexString, targetChecksumValue.ChecksumHexString,
            StringComparison.OrdinalIgnoreCase);
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
