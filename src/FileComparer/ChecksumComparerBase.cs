using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public abstract class ChecksumComparerBase : IFileComparer
{
    public ValueTask<bool> CompareFile(FishPathPair pair)
    {
        var sourceChecksum = getChecksum(pair.Source);
        var targetChecksum = getChecksum(pair.Target);
        return new ValueTask<bool>(sourceChecksum == targetChecksum);
    }

    private string? getChecksum(FishPath path)
    {
        if (path is FishFileMetadata metadata &&
            IsSupportedAlgorithmName(metadata.ChecksumAlgorithm ?? "") &&
            !string.IsNullOrEmpty(metadata.Checksum))
        {
            return metadata.Checksum;
        }
        else if (path.Path.IsRooted)
        {
            return ComputeChecksum(path.Path.GetFullPath());
        }
        else
        {
            return null;
        }
    }

    protected abstract bool IsSupportedAlgorithmName(string algorithmName);
    protected abstract string ComputeChecksum(string fullPath);
}