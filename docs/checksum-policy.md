# Checksum 생략과 파일 존재 확인 계약

2026-09-27에 확정한 FishSyncClient의 checksum 비교 계약이다. [src 리뷰 후속 보고서](src-review-follow-up.md)의 보류 항목 4번에서 논의한 원본 checksum 생략의 의미를 정의한다.

기대 메타데이터는 생성 시 고정하는 불변 값이다. record 전환과 HTTP 응답 크기의 진행률 전달 방식은 [불변 Metadata와 HTTP 진행률](immutable-metadata.md)을 참고한다.

## 결정과 의도

원본 checksum은 선택 사항이다. 원본 `Metadata` 또는 `Metadata.Checksum`이 `null`이면 대상 경로에 파일이 존재하는 것으로 동기화 요구사항을 충족한다. 파일 크기와 내용은 비교하지 않으며 원본 checksum을 자동으로 계산하지 않는다.

checksum 생략은 오류가 아닌 정상적인 정책이다. 따라서 원본 checksum이 없을 때 메타데이터 비교기가 `true`를 반환하고 오류 정책을 적용하지 않는 동작은 의도한 동작이다. 다만 불완전한 값까지 생략으로 취급하던 동작과 `true`를 내용 동일성으로 해석할 수 있는 표현은 명확히 구분한다.

| 원본 checksum | 대상 파일 | 로컬 checksum 비교 및 동기화 |
| --- | --- | --- |
| 생략 | 없음 | `false`: 적용 시 파일 생성 필요 |
| 생략 | 있음 | `true`: 크기·내용을 읽지 않고 기존 파일 유지 |
| 유효한 값 | 없음 | `false`: 적용 시 파일 생성 필요 |
| 유효한 값 | 있음 | 대상 내용을 해시하여 갱신 여부 결정 |
| 불완전하거나 잘못된 값 | 어느 경우든 | `FileComparerException`: 해당 파일을 읽거나 쓰기 전에 거부 |

기존 대상이 0바이트이거나 이전 전송에서 일부만 저장된 파일이어도 checksum이 생략되면 그대로 유지한다. 이는 존재 확인 정책의 의도적인 결과다. checksum 생략은 경로에 실제 파일이 있는지를 확인하는 정책이며, 디렉터리는 파일로 인정하지 않고 기존 링크 차단 정책도 유지한다.

## 생략과 잘못된 값

- `Metadata == null` 또는 `Metadata.Checksum == null`만 생략이다.
- nullable checksum에 담긴 `default(SyncFileChecksum)`은 생략이 아닌 잘못된 값이다.
- checksum이 제공되면 알고리즘과 해시 문자열이 모두 필요하다. null, 빈 문자열, 공백은 유효한 값이 아니다.
- 현재 지원하는 알고리즘 이름은 정확히 `md5`, `sha1`이다. 이름은 대소문자를 구분한다.
- MD5는 32자, SHA1은 40자의 ASCII 16진수 문자열이어야 한다. 접두사·구분자·공백은 허용하지 않는다.
- 16진수 값의 대소문자는 비교 시 무시한다.
- 잘못된 값이나 미지원 알고리즘은 `FileComparerException`으로 거부한다. 기존의 미지원 알고리즘 `KeyNotFoundException` 대신 비교기 오류로 통일한다.

검증은 checksum 비교기에서 수행한다. `SyncFileChecksum` 생성자나 checksum을 사용하지 않는 크기 비교기의 계약은 변경하지 않는다. 빈 문자열로 생략을 표현하던 호출자는 `Checksum = null`로 변경해야 한다.

## 두 비교기의 책임

### LocalFileChecksumComparer

제공된 원본 checksum을 먼저 검증하고 대상 파일의 실제 존재 여부를 확인한다. 원본 checksum이 생략되었고 대상이 존재하면 파일 스트림을 열지 않고 `true`를 반환한다. 유효한 checksum이 있으면 대상 내용을 읽어 비교한다. 대상의 checksum 메타데이터는 필요하지 않다.

대상이 없더라도 잘못된 원본 checksum을 먼저 거부한다. 따라서 해당 파일의 다운로드·생성을 시작한 뒤 입력 오류를 발견하지 않는다. 이는 비교되는 파일별 보장이며, 동기화 전체의 사전 검증이나 롤백을 의미하지 않는다. 경로만 비교하여 신규 파일로 분류하는 `CompareFiles`는 신규 파일의 checksum 비교기를 호출하지 않는다.

### FileChecksumMetadataComparer

저장소에 접근하지 않으며 실제 파일 존재 여부를 확인하지 않는다. 호출자가 대상 파일의 존재를 보장한 쌍에 사용하는 메타데이터 전용 비교기다.

`SyncFileCollectionSyncer.CompareFiles`는 호출자가 제공한 원본·대상 목록에서 경로를 비교하고, 양쪽에 있는 파일 쌍에만 비교기를 호출한다. 목록에 대상이 없으면 `AddedFiles`로 분류한다. 대상 목록이 실제 존재하는 파일을 나타내도록 유지하는 책임은 호출자에게 있다.

반면 `CompareAndSyncFiles`는 아직 생성되지 않은 대상 쌍도 비교기에 전달한다. 로컬 파일 생성·존재 확인과 전송 후 검증에는 `LocalFileChecksumComparer`를 사용한다. 메타데이터 비교기를 누락 파일 탐지나 전송 후 실제 내용 검증 용도로 사용하지 않는다. 현재 CLI의 로컬 비교기 팩터리는 `LocalFileChecksumComparer`를 사용하며, 메타데이터 비교기를 기본 생성 경로에 추가하지 않는다.

## 원본과 대상의 비대칭 및 오류 정책

원본 checksum이 없으면 내용에 대한 요구사항이 없으므로 대상 checksum을 검사하지 않는다. `ComparerErrorHandlingModes`가 무엇이든 `true`를 반환한다.

원본에 유효한 checksum이 있으면 내용 검증 요구사항이 존재한다. 이때 대상 checksum의 상태는 다음과 같이 처리한다.

| 대상 checksum 상태 | 메타데이터 비교기 동작 |
| --- | --- |
| 생략 | `ComparerErrorHandlingModes` 적용 |
| 유효하지만 알고리즘이 다름 | `ComparerErrorHandlingModes` 적용 |
| 같은 알고리즘의 유효한 값 | hex 대소문자를 무시하여 비교 |
| 제공되었지만 불완전·형식 오류·미지원 알고리즘 | 오류 정책과 관계없이 `FileComparerException` |

`ReturnEqual`, `ReturnNotEqual`, `ThrowException`은 비교 불가 상황에서 각각 `true`, `false`, 예외를 선택한다. 잘못된 원본 또는 검사 대상 checksum을 정상 값으로 바꾸는 옵션이 아니다. 파일 읽기 실패나 취소를 checksum 생략으로 취급하지 않는다.

## 결과의 의미와 전송 후 검사

`IFileComparer.AreEqual`의 `true`는 **선택한 비교 기준을 충족하여 추가 내용 전송이 필요하지 않음**을 뜻한다. 반드시 바이트 단위 동일성이나 무결성 검증 완료를 의미하지 않는다. 기존 `bool` API와 `IdenticalFiles` / `IdenticalFilePairs` 결과 이름은 유지하며 같은 의미로 해석한다.

동기화 적용 시 전송 후에도 같은 비교기를 사용한다. 원본 checksum이 없으면 전송 완료 후 파일 존재 여부만 확인한다. 크기·내용 검증은 추가하지 않는다. 전송 자체가 실패하거나 취소된 경우의 예외 처리는 그대로 유지한다.

## 회귀 검증

- 원본 메타데이터 전체 생략과 checksum만 생략한 경우 모두 기존 내용·빈 파일·부분 파일 유지, 스트림 미오픈, 누락 파일 생성.
- 잘못된 원본 checksum은 기존/누락 대상 모두 전송 전에 거부하며 기존 내용을 보존.
- 잘못된 필드, 기본 구조체 값, 알고리즘별 길이, ASCII hex, 알고리즘 이름 대소문자 및 미지원 알고리즘 검증.
- 메타데이터 비교기의 모든 오류 모드에서 원본 생략은 허용하고, 검사할 checksum의 잘못된 값은 거부.
- 대상 checksum 생략 및 서로 다른 유효한 알고리즘의 기존 오류 정책 유지.
- 목록 비교에서 대상 누락을 신규 파일로 분류하고 MD5/SHA1 hex 대소문자를 무시.

검증 명령:

```sh
dotnet test test/FishSyncClientTest.csproj --configuration Release
dotnet test gui.tests/gui.tests.csproj --configuration Release
dotnet build FishSyncClient.sln --configuration Release
```
