# 규칙 기반 파일 동기화 명세

대상 버전: 1.0.0 (기존 API와 호환되지 않는 변경)

## 1. 목적과 범위

기존 `TargetPathMatcher`를 제거하고, 순서가 있는 `Rules` 목록으로 파일의 다운로드·업데이트·삭제 정책을 정의한다. 기존 경로 매처 방식과 병행하는 호환 모드는 제공하지 않는다. 기존 호출부는 새 규칙 시스템으로 변경해야 한다.

이 문서에서 서버는 동기화 원본(source), 로컬은 동기화 대상(target)을 의미한다. 라이브러리에서는 같은 정책을 원본과 대상에 적용한다.

버전 조회·저장은 호출자가 담당한다. 규칙 편집 GUI와 새로운 서버 API는 이번 작업 범위에 포함하지 않는다.

## 2. 설정 형식

```json
{
  "rules": [
    {
      "action": "fullSync",
      "condition": "onNewVersion",
      "pattern": "mods/+*"
    },
    {
      "action": "fullSync",
      "pattern": "resourcepacks/**"
    },
    {
      "action": "exclude",
      "pattern": "private/**"
    },
    {
      "action": "installOnly",
      "pattern": "**"
    }
  ]
}
```

| 필드 | 필수 여부 | 의미 |
| --- | --- | --- |
| `action` | 필수 | `fullSync`, `installOnly`, `exclude` 중 하나 |
| `condition` | 선택 | `always`, `onNewVersion` 중 하나. 생략하면 `always` |
| `pattern` | 필수 | 동기화 루트를 기준으로 한 상대 경로 glob |

JSON의 필드명과 값은 위 표기를 사용한다. C# API에서는 `Rules` 등의 일반적인 PascalCase 이름을 사용한다. 규칙은 `SyncRule`, 동작은 `SyncAction`, 조건은 `SyncCondition`, 실행 문맥은 `SyncContext`로 표현하며 모두 `FishSyncClient.Syncer` 네임스페이스에 있다.

## 3. 동작 정의

| 파일 상태 | `fullSync` | `installOnly` | `exclude` |
| --- | --- | --- | --- |
| 서버에만 존재 | 다운로드 | 다운로드 | 무시 |
| 양쪽에 존재하고 내용이 다름 | 업데이트 | 로컬 유지 | 무시 |
| 양쪽에 존재하고 내용이 같음 | 유지 | 로컬 유지 | 무시 |
| 로컬에만 존재 | 삭제 대상으로 반환 | 유지 | 무시 |

- `fullSync`: 대상을 원본과 일치시키는 정책이다.
- `installOnly`: 대상에 없는 파일만 설치한다. 기존 파일을 비교·업데이트하거나 삭제하지 않는다.
- `exclude`: 다운로드·내용 비교·업데이트·삭제 대상에서 제외한다. 서버에만 존재하는 파일도 다운로드하지 않는다.

`always`는 실행할 때마다 해당 정책을 평가한다는 의미다. `fullSync`에서도 파일 내용이 같으면 다시 다운로드하지 않는다.

내용의 동일 여부는 기존 `IFileComparer`로 판단한다. 런처에서는 체크섬 비교기를 전달하는 것을 기준으로 한다. 규칙 자체에 비교 알고리즘을 추가하지 않는다.

기존 라이브러리의 exclude는 누락 파일의 설치를 허용했지만, 새 시스템의 `exclude`는 모든 파일 작업을 제외한다. 기존 의미가 필요하면 `installOnly`를 사용한다.

## 4. 규칙 평가 순서

1. 서버와 로컬의 파일 경로를 비교하여 서버에만 있는 파일, 양쪽에 있는 파일, 로컬에만 있는 파일을 구분한다.
2. 각 파일의 상대 경로에 대해 규칙을 배열 순서대로 평가한다.
3. `pattern`과 `condition`이 모두 일치하는 첫 번째 규칙을 선택한다.
4. 경로가 일치해도 조건이 거짓이면 다음 규칙을 평가한다.
5. 규칙을 선택하면 평가를 종료한다. 뒤의 규칙과 병합하거나 덮어쓰지 않는다.
6. 아무 규칙도 선택되지 않으면 `exclude`를 적용한다.
7. 선택된 동작과 파일 상태에 따라 다운로드·비교·삭제 대상을 결정한다.

패턴의 구체성에 따른 자동 정렬은 하지 않는다. 광범위한 규칙보다 예외 규칙을 앞에 배치해야 한다.

**새 파일도 반드시 규칙 평가 대상에 포함한다.** 추가 파일을 무조건 다운로드하는 기존 흐름을 유지해서는 안 된다.

## 5. 실행 조건과 강제 패치

호출자가 실행 문맥으로 다음 정보를 전달한다.

| 값 | 의미 |
| --- | --- |
| `IsNewVersion` | 호출자가 판단한 새 버전 여부 |
| `IsForced` | 강제 패치 여부 |

| 조건 | 평가 |
| --- | --- |
| `always` | 항상 참 |
| `onNewVersion` | `IsNewVersion || IsForced` |

라이브러리는 버전을 조회하거나 저장하지 않는다. 호출자는 동기화 시작 전에 실행 문맥을 결정하고, 한 번의 실행 동안 같은 문맥을 사용한다.

강제 패치는 `onNewVersion` 조건을 활성화한다. 규칙의 순서를 바꾸거나 `exclude`, `installOnly` 정책을 우회하지 않는다.

조건은 규칙 선택에 적용된다. 예를 들어 `fullSync`의 `onNewVersion` 조건이 거짓이면, 누락 파일의 설치 여부도 뒤에서 선택된 규칙으로 결정한다. 뒤에 `installOnly`가 있으면 설치하고, `exclude`가 있거나 매칭되는 규칙이 없으면 설치하지 않는다.

## 6. 설정 누락과 빈 목록

| 설정 상태 | 처리 |
| --- | --- |
| `Rules` 미지정 | 설정 오류 |
| `Rules`가 `null` | 설정 오류 |
| `Rules: []` | 모든 파일을 제외하며 파일 변경 없음 |
| 파일에 매칭되는 규칙 없음 | 해당 파일에 `exclude` 적용 |
| `condition` 생략 | `always` 적용 |

규칙 누락을 전체 동기화로 해석하지 않는다. 기존 동작을 암묵적으로 복원하는 호환 처리를 두지 않는다.

전체 동기화는 다음처럼 명시한다.

```json
{
  "rules": [
    { "action": "fullSync", "pattern": "**" }
  ]
}
```

누락 파일만 설치하려면 다음처럼 명시한다.

```json
{
  "rules": [
    { "action": "installOnly", "pattern": "**" }
  ]
}
```

## 7. 경로와 glob

- 기존 DotNet.Glob을 사용한다.
- 패턴은 동기화 루트에 대한 상대 경로로 작성한다.
- 문서 및 JSON 예제에서는 `/`를 경로 구분자로 사용한다.
- 패턴은 `PathOptions`에 따라 경로 구분자만 `/`로 통일한 뒤 `Glob.Parse()`에 전달한다. 패턴의 `.`이나 중복 구분자를 정리하거나 `..` 및 절대 경로를 별도로 거부하지 않는다.
- glob 문법의 허용 여부와 해석은 DotNet.Glob에 전적으로 맡긴다. 예를 들어 `[abc`처럼 라이브러리가 허용하는 패턴은 별도로 거부하지 않는다.
- 매칭 대상은 기존처럼 정규화된 상대 파일 경로다. 절대 경로 모양의 패턴을 허용해도 절대 경로를 탐색하는 것은 아니다. 실제 파일 경로의 검증과 정규화는 기존대로 유지한다.
- 대소문자 처리는 `PathOptions.CaseInsensitive`에 따른다.
- `mods/+*`는 `mods` 바로 아래에서 이름이 `+`로 시작하는 파일에 적용한다. `+`는 리터럴 문자다.
- `resourcepacks/**`는 `resourcepacks` 아래의 중첩 경로를 포함한다.
- 규칙은 파일 단위로 적용하며, 디렉터리 자체를 삭제하는 정책은 추가하지 않는다.

## 8. 예시 설정의 결과

2절의 설정을 기준으로 한다.

| 경로 또는 상태 | 일반 실행 | 새 버전 또는 강제 패치 |
| --- | --- | --- |
| `mods/+example.jar` | `installOnly`: 없으면 설치, 있으면 유지 | `fullSync`: 설치·비교 후 업데이트·삭제 대상 판정 |
| 로컬에만 있는 `mods/+old.jar` | 유지 | 삭제 대상으로 반환 |
| `resourcepacks/example.zip` | `fullSync` | `fullSync` |
| `private/token.json` | `exclude`: 서버에만 있어도 설치하지 않음 | 동일 |
| `config/options.json` | `installOnly`: 없으면 설치, 있으면 유지 | 동일 |
| 그 외 로컬 전용 파일 | 유지 | 유지 |

`mods/+*`를 버전과 관계없이 항상 동기화하려면 해당 규칙의 `condition`을 `always`로 바꾸거나 생략한다.

## 9. 라이브러리 변경 사항

- `SyncerOptions.TargetPathMatcher`를 제거하고 `Rules` 및 실행 문맥을 받도록 변경한다.
- 동기화 정책을 위한 기존 `RulePathMatcher`와 관련 매처 사용부를 새 평가기로 대체한다. 기존 동기화 경로 필터 API를 호환 진입점으로 남기지 않는다.
- 규칙 모델과 실행 문맥 모델, 순서 기반 규칙 평가기를 추가한다.
- `CompareFiles`와 `CompareAndSyncFiles`에서 동일한 평가 로직을 사용한다.
- 추가 파일, 기존 파일, 삭제 후보 모두에 선택된 정책을 적용한다.
- `AddedFiles`에는 정책상 설치할 파일만, `DeletedFiles`에는 정책상 삭제할 파일만 포함한다.
- `installOnly` 또는 `exclude`로 내용 비교를 생략한 파일을 내용이 동일하다고 판정한 결과에 포함하지 않는다.
- 실제 삭제는 기존처럼 호출자가 삭제 대상 목록을 받아 수행한다. `CompareAndSyncFiles`에 자동 삭제를 추가하지 않는다.
- 호출부가 규칙 평가 이후 별도 버전 조건으로 삭제 대상을 다시 필터링하는 중복 정책을 제거한다.
- 라이브러리의 기존 호출부와 테스트를 새 API로 변경한다. 런처 등 외부 소비자는 패키지 전환 시 함께 수정해야 한다.

이는 의도적인 breaking change다. 별도 저장소의 런처 설정 전달 및 패키지 갱신은 후속 연동 작업으로 다룬다.

## 10. 검증과 오류 처리

전체 규칙 목록을 파일 변경 전에 검증한다. 앞의 규칙에 가려지는 뒤쪽 규칙도 검증 대상이다.

다음은 설정 오류로 처리한다.

- 규칙 목록 미지정 또는 `null`
- 목록 안의 `null` 규칙
- 필수 필드 누락
- 알 수 없는 `action` 또는 `condition`
- `Glob.Parse()`가 거부하는 패턴(예: `null`, 빈 문자열). 공백만 있는 패턴 등 라이브러리가 허용하는 값은 추가 검증하지 않는다.

오류에는 해당 규칙의 인덱스와 잘못된 필드를 포함한다. 잘못된 값을 기본 동작으로 대체하지 않는다. 유효성 검증이 실패하면 다운로드·업데이트·삭제를 시작하지 않는다.

## 11. 완료 기준

- 세 action과 각 파일 상태의 조합이 3절 표와 일치한다.
- 첫 매칭 우선, 규칙 순서 변경, 조건 불충족 시 다음 규칙 평가를 검증한다.
- 일반 실행, 새 버전, 강제 패치에서 `onNewVersion` 평가를 검증한다.
- `exclude` 파일은 서버에만 존재해도 다운로드하지 않는다.
- `installOnly`는 누락 파일만 설치하며 기존 파일과 로컬 전용 파일을 보존한다.
- 강제 패치도 `exclude` 및 `installOnly`를 우회하지 않는다.
- 빈 규칙 목록과 매칭되지 않는 경로는 어떤 파일 변경도 발생시키지 않는다.
- 누락된 규칙 목록과 잘못된 규칙은 파일 변경 전에 오류가 발생한다.
- 상대 경로, 경로 구분자, 대소문자 옵션, `+` 리터럴 및 중첩 glob을 검증한다.
- 비교 전용 API와 비교·동기화 API가 동일한 정책 대상을 선택한다.
- 기존 라이브러리 호출부와 테스트가 새 API로 빌드되고 통과한다.

## 12. C# 사용법과 마이그레이션

```csharp
using FishSyncClient.Syncer;

var options = new SyncerOptions
{
    Rules =
    [
        new SyncRule
        {
            Action = SyncAction.FullSync,
            Condition = SyncCondition.OnNewVersion,
            Pattern = "mods/+*"
        },
        new SyncRule { Action = SyncAction.FullSync, Pattern = "resourcepacks/**" },
        new SyncRule { Action = SyncAction.Exclude, Pattern = "private/**" },
        new SyncRule { Action = SyncAction.InstallOnly, Pattern = "**" }
    ],
    Context = new SyncContext { IsNewVersion = true, IsForced = false }
};
```

`Context`를 생략하면 두 플래그 모두 `false`다. `Context = null`은 설정 오류다.
`PathOptions`, `SyncerOptions`, `SyncRule`, `SyncContext`는 모두 `sealed record`이며 속성은 `init`으로 설정한다.
기존 객체의 속성을 대입하여 변경하는 대신 `with`로 새 객체를 생성한다.

```csharp
var forcedOptions = options with { Context = options.Context with { IsForced = true } };
var caseSensitivePaths = new PathOptions() with { CaseInsensitive = false };
```

`with`는 얕은 복사다. `SyncerOptions.Rules`의 목록과 progress 객체는 복사본과 공유된다.
`IReadOnlyList`는 원본 목록 자체의 변경을 막지 않으므로 전달한 목록을 실행 중 변경하지 않는다.
record 동등성은 각 속성의 동등성에 따르며, `Rules` 목록 원소를 순서대로 비교하는 구조적 동등성을 제공하지 않는다.
기존 일반 클래스 상속 및 생성 후 속성 대입에 의존하던 소비자는 코드 수정과 재빌드가 필요하다.

각 실행을 시작할 때 규칙과 조건을 평가할 문맥을 읽어 고정한다.
설정은 실행 시작과 동시에 다른 스레드에서 변경하지 않는다.

JSON 규칙 목록도 기본 `System.Text.Json` 직렬화기로 읽을 수 있다.

```csharp
using System.Text.Json;
using FishSyncClient.Syncer;

var rules = JsonSerializer.Deserialize<SyncRule[]>(rulesJson);
var options = new SyncerOptions
{
    Rules = rules,
    Context = new SyncContext { IsNewVersion = isNewVersion, IsForced = isForced }
};
```

`rulesJson`은 규칙 배열이다. 2절처럼 `rules` 속성을 포함한 설정 객체는 호출자가 해당 배열을 추출하거나 별도의 설정 DTO로 읽는다.
`SyncerOptions`와 `SyncContext`는 JSON 설정 모델이 아닌 실행 정보다. 규칙을 읽은 뒤 버전 상태, progress, cancellation 등의 실행 정보를 지정하여 구성한다.
JSON의 action·condition은 명세에 정의된 camelCase 문자열로 전달해야 하며 숫자 enum 값이나 여러 값을 결합한 문자열은 허용하지 않는다.
알 수 없는 enum 값은 역직렬화 시 `JsonException`으로 보고하며, 예외의 `Path`에 규칙 인덱스와 필드가 포함된다.
필수 필드 누락 및 glob 오류는 동기화 시작 시 검증하며, `Rules[인덱스].필드`를 포함한 `ArgumentException`으로 보고한다.

기존 코드의 전환 기준은 다음과 같다.

| 기존 사용 방식 | 새 설정 |
| --- | --- |
| 옵션 또는 경로 필터 없이 전체 동기화 | `FullSync` / `**` 명시 |
| 특정 경로만 업데이트·삭제하고 나머지는 누락 파일만 설치 | 해당 경로에 `FullSync`, 마지막에 `InstallOnly` / `**` |
| 기존 제외 경로의 설치 허용·기존 파일 보존 동작 | 해당 경로에 `InstallOnly` |
| 해당 경로의 설치까지 완전히 제외 | 해당 경로에 `Exclude` |

비교·동기화가 끝나도 파일은 자동 삭제되지 않는다. 삭제까지 실행하려면 결과의 `DeletedFiles`를 `LocalSyncer.DeleteLocalFiles`에 전달한다.
`CompareAndSyncFiles`의 `UpdatedFilePairs`는 기존 API처럼 실제 전송된 추가 파일도 포함할 수 있다.
비교하지 않고 보존한 파일은 `IdenticalFilePairs`에 포함하지 않는다.

검증 명령:

```sh
dotnet build FishSyncClient.sln --configuration Release
dotnet test FishSyncClient.sln --configuration Release --no-build
```

Windows 전용 경로 테스트는 Windows에서 실행하며, 다른 운영체제에서는 건너뛴다.
