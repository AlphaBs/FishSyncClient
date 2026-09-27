namespace FishSyncClient.Files;

public class SyncFileMetadata
{
    public long Size { get; set; }

    /// <summary>
    /// 선택적인 기대 체크섬이다. null이면 체크섬 검증을 생략하며, 값이 있으면 비교 시
    /// 지원하는 알고리즘과 완전한 16진수 해시 문자열을 제공해야 한다.
    /// 빈 필드나 SyncFileChecksum의 기본값은 생략을 의미하지 않는다.
    /// </summary>
    public SyncFileChecksum? Checksum { get; set; }
}
