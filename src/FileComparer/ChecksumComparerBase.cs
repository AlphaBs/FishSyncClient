using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public abstract class ChecksumComparerBase : IFileComparer
{
    public ValueTask<bool> CompareFile(SyncFilePair pair)
    {
        var sourceChecksum = getChecksum(pair.Source);
        var targetChecksum = getChecksum(pair.Target);
        return new ValueTask<bool>(sourceChecksum == targetChecksum);
    }

    private string? getChecksum(SyncFile file)
    {
        if (IsSupportedAlgorithmName(file.Metadata?.ChecksumAlgorithm ?? "") &&
            !string.IsNullOrEmpty(file.Metadata?.Checksum))
        {
            return file.Metadata.Checksum;
        }
        else if (file.Path.IsRooted)
        {
            return ComputeChecksum(file.Path.GetFullPath());
        }
        else
        {
            return null;
        }
    }

    protected abstract bool IsSupportedAlgorithmName(string algorithmName);
    protected abstract string ComputeChecksum(string fullPath);
}