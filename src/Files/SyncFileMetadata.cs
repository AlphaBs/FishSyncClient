namespace FishSyncClient.Files;

/// <summary>
/// 파일을 비교할 때 사용하는 불변 기대 메타데이터다. 전송 중 관측한 값은 포함하지 않는다.
/// </summary>
public sealed record SyncFileMetadata
{
    public long Size { get; init; }

    /// <summary>
    /// 선택적인 기대 체크섬이다. null이면 체크섬 검증을 생략하며, 값이 있으면 비교 시
    /// 지원하는 알고리즘과 완전한 16진수 해시 문자열을 제공해야 한다.
    /// 빈 필드나 SyncFileChecksum의 기본값은 생략을 의미하지 않는다.
    /// </summary>
    public SyncFileChecksum? Checksum { get; init; }
}
