using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

/// <summary>
/// 호출자가 존재 여부를 확인한 파일의 체크섬 메타데이터를 비교한다.
/// 저장소에 접근하지 않으므로 대상 파일의 누락을 감지하는 용도로 사용하지 않는다.
/// 원본 체크섬이 생략되면 내용 검증을 요구하지 않으며 모든 오류 모드에서 true를 반환한다.
/// 원본 체크섬이 있으면 잘못된 체크섬은 예외로 처리한다. 오류 모드는 대상 체크섬이 없거나
/// 서로 다른 지원 알고리즘을 사용하는 경우에 적용한다.
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
