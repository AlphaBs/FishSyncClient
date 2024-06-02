using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class FileSizeMetadataComparer : IFileComparer
{
    private readonly ComparerErrorHandlingModes _errorMode;

    public FileSizeMetadataComparer() : this(ComparerErrorHandlingModes.ThrowException)
    {

    }

    public FileSizeMetadataComparer(ComparerErrorHandlingModes mode)
    {
        _errorMode = mode;
    }

    public ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        try
        {
            var sourceSize = getSize(pair.Source);
            var targetSize = getSize(pair.Target);
            return new ValueTask<bool>(sourceSize == targetSize);
        }
        catch (FileComparerException ex)
        {
            switch (_errorMode)
            {
                case ComparerErrorHandlingModes.ReturnEqual:
                    return new ValueTask<bool>(true);
                case ComparerErrorHandlingModes.ReturnNotEqual:
                    return new ValueTask<bool>(false);
                case ComparerErrorHandlingModes.ThrowException:
                default:
                    throw ex;
            }
        }
    }

    private long getSize(SyncFile file)
    {
        if (file.Metadata != null && file.Metadata.Size >= 0)
        {
            return file.Metadata.Size;
        }
        else
        {
            throw new FileComparerException("Cannot get file size: " + file.Path);
        }
    }
}
