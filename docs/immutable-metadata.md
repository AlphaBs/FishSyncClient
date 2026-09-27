# 불변 Metadata와 HTTP 진행률

2026-09-28. `SyncFileMetadata`를 불변 값 객체로 만들고, HTTP 응답에서 알게 된 크기를 전송 진행률에 별도로 전달한다. [src 리뷰 후속 보고서](src-review-follow-up.md)의 6번과 메타데이터 변경에 따른 9번 문제를 다룬다.

## 기대 메타데이터

`SyncFileMetadata`는 `sealed record`이며 `Size`와 `Checksum`은 `init` 속성이다. `SyncFile.Metadata`도 `init`이므로 생성한 파일의 기대 메타데이터를 나중에 교체할 수 없다.

```csharp
var metadata = new SyncFileMetadata
{
    Size = 100,
    Checksum = null
};

var file = new ReadableHttpSyncFile(path, httpClient)
{
    Location = location,
    Metadata = metadata
};

// 새로운 기대값이 필요하면 별도의 값 객체와 파일 객체를 만든다.
var revisedMetadata = metadata with { Size = 200 };
var revisedFile = new ReadableHttpSyncFile(path, httpClient)
{
    Location = location,
    Metadata = revisedMetadata
};
```

기존 객체 초기화 문법은 유지한다. 생성 후 `file.Metadata = ...` 또는 `file.Metadata.Size = ...`로 수정하던 호출자는 생성 시 값을 제공하도록 바꿔야 한다. 기존 setter를 사용하던 바이너리도 재빌드가 필요하며, 메타데이터 상속은 지원하지 않는다.

`OpenReadStream` 호출 후 Metadata가 채워질 것을 기대하던 호출자도 변경이 필요하다. HTTP 읽기는 Metadata를 생성하지 않는다. 크기 비교기를 사용할 때는 기대 크기를 미리 제공하고, 크기를 모르는 다운로드의 진행 상황은 진행률 콜백으로 받는다.

`Size`는 기존 `long`과 기본값 `0`을 유지한다. `Metadata == null`은 기대 메타데이터를 제공하지 않았다는 뜻이다. Metadata 객체를 만들고 Size만 생략하면 `0`이므로 실제 0바이트 파일과 구분되지 않는다. 크기의 nullable 표현이나 음수 처리 정책은 이번 변경에서 추가하지 않는다.

checksum 생략 시 파일 존재 여부만 확인하는 [checksum 계약](checksum-policy.md)은 유지한다. 불변화가 checksum이나 크기를 필수로 만드는 것은 아니다.

## 파일 동등성과 해시

`SyncFile.Equals/GetHashCode`는 기존처럼 경로와 Metadata를 사용한다. Metadata가 record가 되므로 별도 인스턴스여도 필드 값이 같으면 동등하다. 기존의 Metadata 참조 비교에서 값 비교로 바뀌는 부분이다.

- 같은 경로와 같은 Metadata 값은 같은 파일 키로 취급한다.
- Metadata가 없으면 없는 상태 자체를 비교한다. null과 기본값 Metadata 객체는 다르다.
- 값 객체의 checksum 문자열 동등성은 대소문자를 구분한다. 파일 내용 비교기의 hex 대소문자 무시 정책과는 별개다.
- 기본 `SyncFile` 구현에서는 파일 생성 후 Metadata가 바뀌지 않고 HTTP 읽기도 이를 수정하지 않으므로, Metadata 변경으로 Dictionary·HashSet의 키를 잃는 문제가 사라진다.

## HTTP 응답 크기와 진행률

이전에는 `OpenReadStream`이 `Content-Length`를 `Metadata.Size`에 덮어쓰고, `CopyTo`가 변경 전후 Size 차이를 진행률에 보고했다. 이제 각 내부 `ResponseStream`이 해당 응답의 `Content-Length`를 `long?`으로 보관한다. HTTP 파일 객체에 마지막 응답의 크기를 저장하지 않으므로 반복·동시 다운로드에서도 응답끼리 값이 섞이지 않는다.

공개 `OpenReadStream`은 계속 `Stream`을 반환한다. `CopyTo`도 이 가상 메서드를 호출하므로 기존 재정의를 유지한다. 재정의가 일반 스트림을 반환하면 기대 크기를 진행률에 사용한다.

진행률 계산은 다음과 같다.

1. 큐에 파일을 등록할 때 `Metadata?.Size ?? 0`을 전체 크기에 반영한다.
2. HTTP 응답의 Content-Length가 있으면 전송 예정 크기로 사용하고, 없으면 등록한 크기를 유지한다.
3. `CopyTo`는 `전송 예정 크기 - 등록한 크기`를 `ByteProgress.TotalBytes` 증감량으로 한 번 보고한다.
4. 실제 복사한 바이트는 기존처럼 `ByteProgress.ProgressedBytes` 증감량으로 보고한다.

| 등록한 크기 | 응답 Content-Length | TotalBytes 보정 | 기대 Metadata |
| --- | --- | --- | --- |
| 미상이라 0 | 100 | +100 | null 유지 |
| 100 | 100 | 0 | 100 유지 |
| 100 | 80 | -20 | 100 유지 |
| 100 | 0 | -100 | 100 유지 |
| 100 | 없음 | 0 | 100 유지 |
| 미상이라 0 | 없음 | 0 | null 유지 |

둘 다 미상이면 전체 크기는 0으로 남고 실제 복사한 바이트만 누적된다. 퍼센트를 산출할 별도 미상 상태는 추가하지 않는다. `CopyTo`를 직접 호출할 때도 기존 증감량 계약을 따르므로 전체 크기 집계가 필요하면 호출자가 초기 기대 크기를 먼저 반영해야 한다.

Content-Length는 응답이 선언한 전송 크기이며 실제로 읽은 바이트 수나 무결성 보장이 아니다. 기대 크기와 다르다는 이유만으로 HTTP 읽기를 새로 실패시키지는 않는다. 크기 비교기를 선택했다면 전송 후에도 원래 기대 크기로 비교하므로 불일치를 감지한다. checksum 비교기를 선택하고 checksum을 생략했다면 기존처럼 존재 여부만 확인한다.

## 검증 범위

- record 값 동등성, `with` 복사 시 원본 유지, 파일 키의 값 비교.
- HTTP 읽기·복사 전후 Metadata와 해시, Dictionary·HashSet 조회 유지.
- 기대 크기 있음/없음/0, 헤더 있음/없음/0, 양수·음수 진행률 보정과 실제 바이트 집계.
- 같은 파일의 반복·동시 요청에서 응답별 크기 분리.
- 기대 크기와 다른 다운로드에 대한 크기 비교기의 전송 후 실패.
- `OpenReadStream` 재정의 유지, 진행률 콜백 실패 시 응답 해제 및 기존 스트림·취소 회귀 테스트.
