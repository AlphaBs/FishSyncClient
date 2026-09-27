# 규칙 기반 파일 동기화 명세

대상 버전: 1.0.0 (기존 API와 호환되지 않는 변경)

## 1. 목적과 범위

기존 `TargetPathMatcher`를 제거하고, 순서가 있는 `Rules` 목록으로 파일의 설치·갱신·삭제 정책과 비교기를 선택한다. 기존 경로 매처 방식과 병행하는 호환 모드는 제공하지 않는다.

이 문서에서 서버는 동기화 원본(source), 로컬은 동기화 대상(target)을 의미한다. 라이브러리는 파일 상태와 규칙을 평가하고, 실제 삭제는 기존처럼 호출자가 수행한다.

`SyncRule`, `SyncAction`, `SyncCondition`, `SyncContext`, `SyncerOptions`는 런타임 API다. JSON 속성, enum converter, JSON 필드명이나 값에 대한 계약을 제공하지 않는다. 설정의 직렬화·역직렬화, 설정 DTO의 검증 및 런타임 규칙으로의 변환은 호출자의 책임이다. 호출자는 직접 생성하거나 DI로 받은 `IFileComparer` 인스턴스를 규칙에 전달한다.

버전 조회·저장 역시 호출자가 담당한다. 별도 저장소의 런처 연동, 규칙 편집 GUI, 새로운 서버 API와 패키지 배포는 이번 작업 범위에 포함하지 않는다.

## 2. 런타임 규칙

규칙 관련 모델은 `FishSyncClient.Syncer` 네임스페이스에 있다.

| 속성 | 필수 여부 | 의미 |
| --- | --- | --- |
| `Action` | 필수 | `FullSync`, `UpdateOnly`, `InstallOnly`, `Exclude` 중 하나 |
| `Condition` | 필수 | `Always`, `OnNewVersion` 중 하나. 기본값 없음 |
| `Pattern` | 필수 | 동기화 루트를 기준으로 한 상대 경로 glob |
| `Comparer` | 필수 인자 | 런타임 `IFileComparer`. `Exclude`에서만 `null` 허용 |

`SyncRule`은 네 생성자 인자를 모두 명시해야 한다. 매개변수 없는 생성자와 속성 기본값은 제공하지 않는다. `Action`과 `Condition`의 enum 값 `0`도 유효한 값이 아니다.

```csharp
using FishSyncClient.FileComparers;
using FishSyncClient.Syncer;

IFileComparer checksum = new LocalFileChecksumComparer();
IFileComparer size = new LocalFileSizeComparer();

var options = new SyncerOptions
{
    Rules =
    [
        new SyncRule(SyncAction.Exclude, SyncCondition.Always, "private/**", null),
        new SyncRule(SyncAction.InstallOnly, SyncCondition.Always, "config/user/**", checksum),
        new SyncRule(SyncAction.FullSync, SyncCondition.Always, "mods/+*", checksum),
        new SyncRule(SyncAction.FullSync, SyncCondition.OnNewVersion, "**", checksum),
        new SyncRule(SyncAction.UpdateOnly, SyncCondition.Always, "**", size)
    ],
    Context = new SyncContext { IsNewVersion = isNewVersion, IsForced = isForced }
};

// syncer는 대상 루트로 생성한 LocalSyncer이고, sourceFiles는 원본 파일 목록이다.
var result = await syncer.CompareAndSyncFiles(sourceFiles, options);
syncer.DeleteLocalFiles(result.DeletedFiles);
```

위 예시에서 일반 파일은 일반 실행 시 크기를 비교하여 갱신하고, 로컬에만 있는 파일은 보존한다. 새 버전이나 강제 패치에서는 체크섬을 비교하고 로컬 전용 파일을 삭제 대상으로 반환한다. `mods/+*`는 항상 체크섬 비교 및 삭제 대상 판정을 수행한다. `config/user/**`는 누락 파일만 설치하고, `private/**`는 설치도 하지 않는다.

## 3. 동작 정의

| 파일 상태 | `FullSync` | `UpdateOnly` | `InstallOnly` | `Exclude` |
| --- | --- | --- | --- | --- |
| 서버에만 존재 | 설치 | 설치 | 설치 | 무시 |
| 양쪽에 존재하고 비교 결과가 다름 | 갱신 | 갱신 | 로컬 유지 | 무시 |
| 양쪽에 존재하고 비교 결과가 같음 | 유지 | 유지 | 로컬 유지 | 무시 |
| 로컬에만 존재 | 삭제 대상으로 반환 | 유지 | 유지 | 무시 |

- `FullSync`: 대상을 원본과 일치시키는 정책이다.
- `UpdateOnly`: 누락 파일을 설치하고 기존 파일을 비교·갱신하되, 로컬 전용 파일을 보존한다.
- `InstallOnly`: 누락 파일만 설치한다. 기존 파일을 비교·갱신하거나 삭제하지 않는다.
- `Exclude`: 설치·내용 비교·갱신·삭제 대상에서 제외한다. 서버에만 있는 파일도 설치하지 않는다.

`Always`는 매번 해당 정책을 평가한다는 의미다. 파일이 같다고 판정되면 다시 다운로드하지 않는다.

기존 라이브러리의 `Excludes`는 누락 파일의 설치를 허용했다. 그 동작은 새 API의 `InstallOnly`에 해당하며, 새 `Exclude`와는 다르다.

## 4. 비교기 선택과 전송 후 검증

첫 번째로 선택된 규칙의 `Comparer`만 사용한다. `CompareFiles`와 `CompareAndSyncFiles`는 공통 비교기 인자를 받지 않는다. `FullSync`, `UpdateOnly`, `InstallOnly` 규칙에 비교기가 없으면 실행 전에 오류가 발생한다. 뒤의 규칙에서 비교기를 가져오거나 여러 비교기를 합치지 않는다.

비교기 선택은 `CompareFiles`와 `CompareAndSyncFiles`에 동일하게 적용된다. 비교·동기화 시에는 전송 전 비교와 전송 후 검증에 같은 비교기를 사용한다. 전송 후에도 다르다고 판정하면 기존처럼 `FileIntegrityException`이 발생한다.


`InstallOnly`의 기존 파일과 `Exclude` 파일에는 비교기를 호출하지 않는다. `InstallOnly`로 설치하는 새 파일에는 선택된 비교기를 적용하여 전송 전후를 확인한다. 비교 전용 API의 새 파일은 기존처럼 `AddedFiles`에만 포함하고 내용 비교를 하지 않는다.

비교기의 알고리즘, 필요한 메타데이터와 판정 의미는 `IFileComparer` 구현에 따른다. 예를 들어 `LocalFileSizeComparer`에는 원본 크기를 제공한다. `LocalFileChecksumComparer`는 원본 체크섬이 있으면 내용을 비교하고, 생략되면 대상 파일 존재 여부만 확인한다. 제공된 잘못된 체크섬은 오류로 처리한다. 자세한 판정과 오류 정책은 [checksum 계약](checksum-policy.md)을 따른다. 라이브러리는 비교기를 복제하거나 직렬화하지 않으며, 호출자는 사용하는 비교기가 병렬 호출을 지원하도록 구성한다.

## 5. 규칙 평가 순서

1. 전체 규칙 목록을 검증하고, 규칙 순서와 실행 문맥을 고정한다.
2. 원본과 대상의 경로를 비교하여 새 파일, 양쪽에 있는 파일, 로컬 전용 파일을 구분한다.
3. 각 상대 경로에 대해 `Pattern`과 `Condition`이 모두 일치하는 첫 번째 규칙을 선택한다.
4. 경로가 일치해도 조건이 거짓이면 다음 규칙으로 넘어간다.
5. 선택된 규칙의 동작과 비교기를 적용한다. 뒤의 규칙과 병합하지 않는다.
6. 선택된 규칙이 없으면 해당 경로를 포함한 예외를 발생시킨다. 새 파일·기존 파일·로컬 전용 파일 모두에 같은 기준을 적용한다.

패턴의 구체성에 따른 자동 정렬은 하지 않는다. 광범위한 규칙보다 예외 규칙을 앞에 배치한다. 모든 경로의 규칙 매칭을 내용 비교·전송 전에 마치므로, 뒤에서 미매칭 파일이 발견되어도 앞의 파일을 먼저 변경하지 않는다.

나머지 경로를 제외하려면 호출자가 마지막 규칙을 명시한다. 모든 파일을 제외할 때도 빈 목록 대신 이 규칙 하나를 전달한다.

```csharp
new SyncRule(SyncAction.Exclude, SyncCondition.Always, "**", null)
```

`AddedFiles`에는 설치할 파일만, `DeletedFiles`에는 `FullSync`에 해당하는 삭제 후보만 포함한다. `InstallOnly`나 `Exclude`로 비교를 생략한 기존 파일은 `IdenticalFilePairs`에 포함하지 않는다. `CompareAndSyncFiles`의 `UpdatedFilePairs`에는 기존 API처럼 실제 전송된 추가 파일도 포함될 수 있다.

## 6. 실행 조건과 강제 패치

호출자가 `SyncContext`로 새 버전 여부인 `IsNewVersion`과 강제 패치 여부인 `IsForced`를 전달한다.

| 조건 | 평가 |
| --- | --- |
| `Always` | 항상 참 |
| `OnNewVersion` | `IsNewVersion || IsForced` |

`Context`를 생략하면 두 플래그 모두 `false`다. `Context = null`은 설정 오류다.

강제 패치는 `OnNewVersion` 조건을 활성화한다. 규칙 순서를 바꾸거나 `Exclude`, `InstallOnly`, `UpdateOnly`의 보존 정책을 우회하지 않는다. 누락 파일의 설치 여부도 조건을 만족하여 선택된 규칙으로 결정한다.

라이브러리는 버전을 조회하거나 저장하지 않는다. 호출자는 실행 전에 문맥을 결정하고, 성공 후 필요한 버전 저장을 수행한다.

## 7. 설정 검증과 경로 처리

| 설정 상태 | 처리 |
| --- | --- |
| 옵션 또는 `Rules` 미지정·`null` | 설정 오류 |
| `Rules = []` | 파일이 없어도 설정 오류 |
| 매칭되는 규칙 없음 | 해당 경로를 포함한 예외. 파일 변경 없음 |
| `Condition` 생략 | 생성자에서 명시 필수. enum 값 `0`도 설정 오류 |
| `Comparer` 생략·`null` | 생성자 인자는 생략 불가. `null`은 `Exclude`에서만 허용 |

전체 규칙 목록을 파일 열거 및 변경 전에 검증한다. 앞의 규칙에 가려지거나 현재 조건이 거짓인 규칙도 검증한다. 다음은 설정 오류다.

- 빈 규칙 목록 또는 목록 안의 `null` 규칙
- 필수 속성 누락
- 정의되지 않은 `Action` 또는 `Condition` enum 값(`0` 포함)
- `FullSync`, `UpdateOnly`, `InstallOnly` 규칙의 `Comparer = null`
- `Glob.Parse()`가 거부하는 패턴(예: `null`, 빈 문자열)

규칙 오류는 인덱스와 속성 이름을 포함한 `ArgumentException`으로 보고한다. 예를 들어 `Rules[1].pattern`이 메시지에 포함된다. 규칙 자체가 유효하더라도 현재 문맥에서 어떤 규칙에도 매칭되지 않는 파일이 있으면 경로를 포함한 `ArgumentException`을 발생시킨다. 검증 실패 시 다운로드·갱신·삭제를 시작하지 않는다. 호출자의 JSON 오류 처리는 이 검증과 별개다.

패턴은 기존 DotNet.Glob을 사용한다. `PathOptions`에 따라 구분자만 `/`로 통일한 뒤 `Glob.Parse()`에 전달한다. 패턴의 `.`이나 중복 구분자를 정리하거나 `..` 및 절대 경로를 별도로 거부하지 않는다. 공백 또는 `[abc`처럼 DotNet.Glob이 허용하는 패턴도 추가로 거부하지 않는다.

매칭 대상은 기존처럼 정규화된 상대 파일 경로다. 절대 경로 모양의 패턴을 허용해도 절대 경로를 탐색하지 않는다. 실제 파일 경로의 검증과 정규화는 기존대로 유지한다. 대소문자 처리는 `PathOptions.CaseInsensitive`에 따른다.

`mods/+*`의 `+`는 리터럴이며 바로 아래 파일만 매칭한다. `resourcepacks/**`는 중첩 경로를 포함한다. 규칙은 파일 단위로 적용하며, 디렉터리 자체를 삭제하는 정책은 추가하지 않는다.

로컬 파일 열거·읽기·쓰기·삭제 시 심볼릭 링크와 Windows 정션을 포함한 reparse point를 거부한다. 루트와 상위 디렉터리도 같은 정책을 적용하며, 링크를 발견하면 건너뛰지 않고 예외를 발생시킨다. 열거 이후 링크가 생기는 경우를 위해 실제 I/O와 교체·삭제 직전에도 검사한다. 경로 검사는 다른 프로세스가 검사와 시스템 호출 사이에 경로를 바꾸는 경쟁 조건까지 원자적으로 차단하지는 않으므로, 동기화 중 루트/디렉터리 구조를 외부에서 변경하지 않는다.

## 8. 기존 런처 정책의 이전

`ml-codebase`에서는 설정의 `includes`를 `AlwaysUpdates`로 변환한다. 기존의 비교기 분기와 삭제 후보 필터는 다음 순서로 표현한다.

| 순서 | 경로 | 동작 | 조건 | 비교기 |
| --- | --- | --- | --- | --- |
| 1 | 기존 `excludes` | `InstallOnly` | `Always` | 체크섬 비교기 |
| 2 | 기존 `includes` / `AlwaysUpdates` | `FullSync` | `Always` | 체크섬 비교기 |
| 3 | `**` | `FullSync` | `OnNewVersion` | 체크섬 비교기 |
| 4 | `**` | `UpdateOnly` | `Always` | 크기 비교기 |

각 기존 패턴을 같은 위치의 개별 `SyncRule`로 변환하고, 비교기는 각 규칙에 직접 전달한다. 제외 규칙을 먼저 배치하여 포함 패턴과 겹쳐도 기존 파일을 보존한다. 원본 메타데이터와 비교기 구현은 호출자가 준비한다.

일반 실행에서도 기본 파일은 크기가 다르면 갱신되지만 로컬 전용 파일은 남는다. 새 버전·강제 패치에서는 3번 규칙이 선택되어 체크섬 비교와 삭제 판정을 수행한다. 런처는 반환된 `DeletedFiles`를 별도 버전·경로 필터 없이 `DeleteLocalFiles`에 전달할 수 있다.

기존 `Includes` API로 특정 경로만 갱신·삭제하고 나머지는 설치만 하던 소비자는 해당 경로에 `FullSync`, 마지막에 `InstallOnly` / `**`를 둔다. 전체 동기화는 `FullSync` / `**`, 설치까지 완전히 차단하려면 `Exclude`를 명시한다.

## 9. 옵션의 수명과 변경

`PathOptions`, `SyncerOptions`, `SyncRule`, `SyncContext`는 모두 `sealed record`이며 속성은 `init`으로 설정한다. 변경 시 `with`로 새 객체를 생성한다.

```csharp
var forcedOptions = options with { Context = options.Context with { IsForced = true } };
var caseSensitivePaths = new PathOptions() with { CaseInsensitive = false };
```

`with`는 얕은 복사다. 규칙 목록, 비교기와 progress 객체는 복사본과 공유된다. `IReadOnlyList`는 원본 목록 자체의 변경을 막지 않으므로 실행 시작과 동시에 다른 스레드에서 설정을 변경하지 않는다. 실행 시작 시 읽은 규칙과 문맥을 한 실행 동안 사용한다. 비교기 내부 상태의 관리와 수명은 호출자의 책임이다.

record 동등성은 각 속성의 동등성에 따르며, 규칙 목록의 구조적 동등성을 제공하지 않는다. 일반 클래스 상속, 생성 후 속성 대입, 기존 경로 필터 API에 의존하던 소비자는 수정 및 재빌드가 필요하다.

## 10. 검증

- 네 action의 추가·갱신·보존·삭제 후보 동작을 비교 전용 및 비교·동기화 API에서 검증한다.
- 규칙 순서, 조건 불충족 시 다음 규칙 선택, 일반·새 버전·강제 패치 문맥을 검증한다.
- 선택된 규칙의 비교기, 전송 후 검증, 취소 토큰 전달을 검증한다.
- 기존 런처의 제외 우선순위와 크기·체크섬 비교 및 삭제 조건을 실제 파일로 검증한다.
- 빈 목록, 비교기 누락 및 잘못된 규칙이 파일 열거 전에 실패하는지 검증한다.
- 새 파일·기존 파일·로컬 전용 파일의 미매칭 오류가 내용 비교·전송 전에 발생하고, 명시적인 `Exclude` 규칙은 파일을 보존하는지 검증한다.
- 상대 경로, 구분자, 대소문자 옵션, `+` 리터럴 및 중첩 glob을 검증한다.

```sh
dotnet build FishSyncClient.sln --configuration Release
dotnet test FishSyncClient.sln --configuration Release --no-build
```

Windows 전용 경로 테스트는 Windows에서 실행하며, 다른 운영체제에서는 건너뛴다.
