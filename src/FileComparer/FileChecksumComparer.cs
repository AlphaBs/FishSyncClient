using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class FileChecksumComparer : IFileComparer
{
    private readonly Dictionary<string, IFileComparer> _comparers = new();

    public IFileComparer? DefaultComparer { get; set; }

    public void AddAlgorithm(string algorithmName, IFileComparer comparer)
    {
        _comparers.Add(algorithmName, comparer);
    }

    public IFileComparer? GetComparer(string? checksumAlgorithm)
    {
        if (_comparers.TryGetValue(checksumAlgorithm ?? "", out var comparer))
        {
            return comparer;
        }
        else
        {
            return null;
        }
    }

    public IFileComparer? GetComparer(FishPathPair pair)
    {
        var sourceMetadata = pair.Source as FishFileMetadata;
        var targetMetadata = pair.Target as FishFileMetadata;

        if (sourceMetadata != null && targetMetadata != null)
        {
            if (sourceMetadata.ChecksumAlgorithm == targetMetadata.ChecksumAlgorithm)
            {
                return GetComparer(sourceMetadata.ChecksumAlgorithm);
            }
            else
            {
                if (sourceMetadata.Path.IsRooted && targetMetadata.Path.IsRooted)
                {
                    // 두 파일 모두 rooted 경로인데 서로 다른 체크섬 알고리즘을 사용한다면,
                    // 둘 중 아무 체크섬 알고리즘을 사용하면 된다. 
                    return GetComparer(sourceMetadata.ChecksumAlgorithm) ?? GetComparer(targetMetadata.ChecksumAlgorithm);
                }
                else if (sourceMetadata.Path.IsRooted)
                {
                    // 한 파일만 rooted 경로인데 서로 다른 체크섬 알고리즘을 사용한다면,
                    // rooted 경로가 아닌 쪽의 체크섬 알고리즘을 사용한다.
                    return GetComparer(targetMetadata.ChecksumAlgorithm);
                }
                else if (targetMetadata.Path.IsRooted)
                {
                    // 한 파일만 rooted 경로인데 서로 다른 체크섬 알고리즘을 사용한다면,
                    // rooted 경로가 아닌 쪽의 체크섬 알고리즘을 사용한다.
                    return GetComparer(sourceMetadata.ChecksumAlgorithm);
                }
                else
                {
                    // 두 파일 모두 rooted 경로가 아니며 다른 체크섬 알고리즘을 사용한다면,
                    // 두 파일은 체크섬 비교가 불가능하다.
                    return null;
                }
            }
        }
        else if (sourceMetadata != null)
        {
            // 두 파일 중 체크섬 알고리즘이 명시되어 있는 경우
            return GetComparer(sourceMetadata.ChecksumAlgorithm);
        }
        else if (targetMetadata != null)
        {
            // 두 파일 중 체크섬 알고리즘이 명시되어 있는 경우
            return GetComparer(targetMetadata.ChecksumAlgorithm);
        }
        else
        {
            // 체크섬 알고리즘이 명시되어 있지 않은 경우,
            // 어떤 체크섬 알고리즘을 사용해도 된다. 
            return DefaultComparer;
        }
    }

    public async ValueTask<bool> CompareFile(FishPathPair pair)
    {
        var comparer = GetComparer(pair) ?? DefaultComparer;
        if (comparer == null)
            throw new InvalidOperationException("Unsupported Algorithm");        
        return await comparer.CompareFile(pair);
    }
}