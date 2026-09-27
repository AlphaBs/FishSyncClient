using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

/// <summary>
/// 대상 파일의 존재 여부를 확인하고, 원본 체크섬이 있으면 대상 내용을 검증한다.
/// 원본 체크섬이 생략되면 대상의 내용이나 크기를 읽지 않고 존재 여부만으로 충족 여부를 판단한다.
/// 제공된 체크섬은 대상 파일이 없더라도 유효해야 한다.
/// </summary>
public class LocalFileChecksumComparer : IFileComparer
{
    public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetLocalFile = pair.Target as LocalSyncFile;
        if (targetLocalFile == null)
            throw new FileComparerException("Target should be LocalSyncFile");

        var sourceChecksum = pair.Source.Metadata?.Checksum;
        if (sourceChecksum.HasValue)
            ChecksumMetadataValidator.Validate(sourceChecksum.Value, "source");

        if (!targetLocalFile.Exists)
            return false;

        if (!sourceChecksum.HasValue)
            return true;

        using var readStream = await targetLocalFile.OpenReadStream(cancellationToken);
        var targetChecksum = await ChecksumAlgorithms.ComputeHashAsync(sourceChecksum.Value.AlgorithmName, readStream, cancellationToken);
        return string.Equals(sourceChecksum.Value.ChecksumHexString, targetChecksum, StringComparison.OrdinalIgnoreCase);
    }
}
