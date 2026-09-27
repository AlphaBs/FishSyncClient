using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public interface IFileComparer
{
    /// <summary>
    /// 파일 쌍이 비교 기준을 충족하여 내용 전송이 필요하지 않으면 true를 반환한다.
    /// 이 결과가 반드시 바이트 단위 동일성이나 무결성 검증 완료를 의미하지는 않는다.
    /// 필요한 메타데이터와 파일 존재 여부를 확인할 책임은 구현에 따라 다르다.
    /// </summary>
    ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken);
}
