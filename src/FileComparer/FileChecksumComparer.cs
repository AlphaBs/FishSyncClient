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

    public IFileComparer? GetComparer(SyncFilePair pair)
    {
        var sourceChecksumAlgorithm = pair.Source.Metadata?.ChecksumAlgorithm;
        var targetChecksumAlgorithm = pair.Target.Metadata?.ChecksumAlgorithm;

        if (string.IsNullOrEmpty(sourceChecksumAlgorithm) && string.IsNullOrEmpty(targetChecksumAlgorithm))
        {
            // 체크섬 알고리즘이 둘다 명시되지 않은 경우
            // 아무 체크섬 알고리즘을 사용하면 된다.
            return DefaultComparer;
        }
        else if (sourceChecksumAlgorithm == targetChecksumAlgorithm)
        {
            // 체크섬 알고리즘이 같다면
            // 해당 알고리즘을 사용하면 된다.
            return GetComparer(sourceChecksumAlgorithm);
        }
        else if (pair.Source.Path.IsRooted && pair.Target.Path.IsRooted)
        {
            // 두 파일 모두 rooted 경로인데 서로 다른 체크섬 알고리즘을 사용한다면,
            // 둘 중 아무 체크섬 알고리즘을 사용하면 된다. 
            return GetComparer(sourceChecksumAlgorithm) ?? GetComparer(targetChecksumAlgorithm);
        }
        else if (pair.Source.Path.IsRooted)
        {
            // 한 파일만 rooted 경로인데 서로 다른 체크섬 알고리즘을 사용한다면,
            // rooted 경로가 아닌 쪽의 체크섬 알고리즘을 사용한다.
            return GetComparer(targetChecksumAlgorithm);
        }
        else if (pair.Target.Path.IsRooted)
        {
            // 한 파일만 rooted 경로인데 서로 다른 체크섬 알고리즘을 사용한다면,
            // rooted 경로가 아닌 쪽의 체크섬 알고리즘을 사용한다.
            return GetComparer(sourceChecksumAlgorithm);
        }
        else
        {
            // 두 파일 모두 rooted 경로가 아니며 다른 체크섬 알고리즘을 사용한다면,
            // 두 파일은 체크섬 비교가 불가능하다.
            return null;
        }
    }

    public async ValueTask<bool> AreEqual(SyncFilePair pair)
    {
        var comparer = GetComparer(pair) ?? DefaultComparer;
        if (comparer == null)
            throw new InvalidOperationException("Unsupported Algorithm");
        return await comparer.AreEqual(pair);
    }
}