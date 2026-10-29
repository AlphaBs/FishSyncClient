# FishSyncClient

파일 동기화용 .NET 라이브러리와 FishSyncServer용 CLI·GUI 클라이언트입니다.

- 로컬·원격 파일 목록 비교, 크기·체크섬 비교
- 동기화에 필요한 파일 복사·삭제
- FishSyncServer와 PULL(서버 → 로컬), PUSH(로컬 → 서버)

비교 규칙은 [checksum 계약](docs/checksum-policy.md)과 [불변 Metadata와 HTTP 진행률](docs/immutable-metadata.md)을 참고하세요.

# FishSyncClient.Cli

FishSyncServer와 동기화하는 CLI입니다. 사용법·설정·주의사항은 배포 ZIP에 함께 들어가는 [CLI README](docs/cli-readme.md)와 `--help`에 있습니다.

소스에서 실행할 때는 `cli` 대신 `dotnet run --project cli --no-launch-profile --`를 씁니다. 인증 정보는 [.env.example](.env.example)을 `.env`로 복사해 채웁니다(`.env`는 Git에서 제외).

```sh
dotnet run --project cli --no-launch-profile -- --help
dotnet run --project cli --no-launch-profile -- pull my-bucket --root /absolute/path/files
```

## CLI 배포 ZIP 생성

.NET 10 SDK, Python 3, 대상 OS의 NativeAOT 빌드 도구가 필요합니다. 다른 OS용은 해당 OS에서 빌드해야 합니다.

```sh
python3 scripts/release-cli.py                  # 현재 OS·아키텍처
python3 scripts/release-cli.py --rid linux-x64  # win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
# 또는: task cli
```

버전은 `cli/cli.csproj`의 `Version`을 따릅니다. 결과는 `artifacts/releases/FishSyncClientCli-<RID>-<Version>.zip`이며, 실행 파일·[CLI README](docs/cli-readme.md)·인증 정보가 없는 `config/config.json`이 들어갑니다.

Windows·Linux·macOS x64를 한 번에 빌드하려면 GitHub Actions의 **CLI NativeAOT packages** 워크플로를 실행합니다. 각 OS에서 ZIP을 만들고 파일 목록 조회·다운로드·업로드를 검사합니다. 완료 후 실행 페이지의 `cli-release-<RID>` 아티팩트에서 ZIP을 받을 수 있습니다.

CLI는 `PublishAot=true`로 게시합니다. 검사 결과와 재현 명령은 [NativeAOT 검사](docs/nativeaot-audit.md)를 참고하세요.

# FishSyncClient.Gui

FishSyncServer와 동기화하는 GUI 앱입니다.

## 사용법

1. 계정 아이디, 비밀번호, 버킷 ID를 입력하고 **다음**을 누릅니다. 서버 주소는 로그인 화면에서 바꿀 수 있습니다(기본 `https://fish2.snowfrost.kr/api`).
2. 서버 파일을 실행 파일 옆 `buckets/<버킷ID>`로 내려받습니다. **다음을 누를 때마다 서버 기준으로 초기화하므로, 업로드하지 않은 로컬 수정은 덮어쓰고 로컬에만 있는 파일은 삭제합니다.**
3. 파일 탐색기가 열리면 파일을 편집합니다. 목록에서 초록은 추가, 빨강은 삭제, 노랑은 변경된 파일입니다.
4. **동기화**를 누르면 변경사항을 보여주고 로컬 상태를 서버에 반영합니다. 실패하거나 취소하면 일부만 반영되었을 수 있으니 다시 동기화하세요.

- 계정 아이디와 버킷 ID는 실행 파일 옆 `config/config.json`에 저장하므로 실행 폴더에 쓰기 권한이 필요합니다.
- Windows x64와 macOS에서는 **OS 키체인에 비밀번호 저장**을 선택할 수 있습니다. Windows에서는 Microsoft Visual C++ x64 런타임이 필요하고, 다른 플랫폼에서는 매번 직접 로그인합니다.
- `WarningPatterns`에 일치하는 파일은 업로드 전에 경고합니다. 경고일 뿐 업로드를 막지는 않습니다.
- 작업 화면에서 서버 용량·파일 수 사용률, 월 동기화 잔여 횟수, 만료일, 읽기 전용 여부를 볼 수 있습니다. 한도를 넘으면 업로드 전에 중단합니다.
- 실행할 때 새 버전을 확인하고, 있으면 다운로드 페이지를 열지 묻습니다. 자동으로 업데이트하지는 않습니다.

## 개발

```sh
dotnet run --project gui/gui.csproj
dotnet test test/FishSyncClientTest.csproj
dotnet test gui.tests/gui.tests.csproj   # 가짜 서버 사용, 실제 계정·파일은 건드리지 않음
```

`FISH_UI_SCREENSHOTS`에 폴더를 지정하면 GUI 테스트가 화면 PNG를 저장합니다.

## GUI 배포 ZIP 생성

[go-task](https://taskfile.dev/docs/installation)와 .NET SDK가 필요합니다. 배포판에 따라 `task` 대신 `go-task`일 수 있습니다.

```sh
task windows                 # Windows x64
task linux                   # Linux x64
task darwin ARCH=aarch64     # macOS Apple Silicon
task build                   # Windows/Linux/macOS x64
task build ARCH=aarch64      # Windows/Linux/macOS ARM64
task all                     # 전체 6개
```

버전은 `gui/gui.csproj`의 `ClientVersion`을 따르며, 결과는 `artifacts/releases/FishSyncClientGui-<os>-<arch>-<version>.zip`입니다(예: `FishSyncClientGui-windows-x64-2.0.0.zip`). ZIP에는 .NET 설치 없이 실행되는 단일 실행 파일과 기본 `config/config.json`이 들어갑니다. macOS 서명·공증은 별도로 해야 합니다.
