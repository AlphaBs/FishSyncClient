# .NET 10 / CLI NativeAOT 검사

2026-10-30, CLI 1.1.0을 Windows·Linux x64 및 macOS ARM64의 GitHub Actions 환경에서 검사했습니다.
로컬 CLI 단위 테스트는 Linux x64, .NET SDK 10.0.112에서 실행했습니다.
GUI는 .NET 10 일반 게시를 유지하고, CLI에만 `PublishAot=true`를 설정했습니다.

## 결과

| 검사 | 결과 |
|---|---|
| 솔루션 Release 빌드 | 성공, 경고 0개 / 오류 0개 |
| CLI·핵심 라이브러리 테스트 | 389개 통과, Windows 전용 12개 제외 |
| GUI 테스트 | 57개 통과, Windows/macOS 키체인 검사 1개 제외 |
| CLI Windows·Linux x64 및 macOS ARM64 NativeAOT 게시 | 모두 성공, 경고 0개 / 오류 0개 |
| 실제 AOT 실행 검사 | 각 OS에서 8개씩 통과 |
| CLI 배포 ZIP | 실행 파일, README.md, config/config.json 포함 |

실제 AOT 실행 파일을 임시 `.env`와 로컬 가짜 HTTP 서버로 검사했습니다.
실제 계정이나 외부 서버의 파일은 사용하지 않았습니다.

- 도움말 출력 및 계정 환경변수 누락 오류.
- `files` 명령으로 인증된 버킷 파일 목록을 조회하며 로컬 파일을 변경하지 않는지 확인.
- 환경변수·`.env` 설정을 통한 서버 인증과 MD5 검증을 포함한 파일 다운로드.
- 수정한 파일과 경고 대상 파일의 실제 HTTP 업로드 및 동기화 완료.
- `-y`·`--yes` 사용 시 경고 로그를 유지하고 계속 진행.
- 경고에서 n을 입력하면 서버 요청 전에 취소.
- 잘못된 옵션은 명령을 실행하지 않고 stderr 안내와 종료 코드 1 반환.
- 인증 토큰이 파일이나 출력에 남지 않는지 확인.

## 적용 패키지

- System.CommandLine **2.0.14**: 명령·옵션을 명시적으로 등록하고 파싱 결과를 직접 전달합니다.
- FishBucket **0.1.0** 및 FishBucket.ApiClient **0.1.0**: .NET 8 대상 어셈블리를 사용합니다.
- FishBucket.SyncClient **0.1.0**: CLI와 GUI 모두 갱신했습니다. NativeAOT 게시와 실제 파일 전송 검사를 통과했습니다.

이전 CommandLineParser 2.9.1과 FishBucket.ApiClient 0.0.1 조합의 AOT 경고 28개 및 JSON 리플렉션 실행 실패는 현재 조합에서 재현되지 않았습니다.
경고 억제나 JSON 리플렉션 강제 활성화는 적용하지 않았습니다.

## 코드 변경

- 명령 클래스의 CommandLineParser 특성과 `DynamicDependency` 보존 설정 제거.
- System.CommandLine에 선택적 버킷 ID, `--server`, `--root`, `--config`, `-y`·`--yes` 등록.
- 기존 `help`, `help pull`, `help push`, `--help`, `--version` 및 stdout/stderr·종료 코드 규칙 유지.
- CLI 자체 JSON 처리는 System.Text.Json 소스 생성 사용.
- SDK의 `Md5Checksum` 타입에 맞춰 CLI·GUI 업로드·다운로드 경계에서 변환.
- .NET 10에서 불필요한 패키지 참조와 nullable 경고 정리.
- Tmds.DBus.Protocol 0.21.3 및 테스트 SDK 17.11.1로 기존 보안 경고 정리.

## 재현

```sh
dotnet build FishSyncClient.sln -c Release -m:1
dotnet test test/FishSyncClientTest.csproj -c Release --no-build
dotnet test gui.tests/gui.tests.csproj -c Release --no-build
dotnet publish cli/cli.csproj -c Release -r linux-x64 -o artifacts/nativeaot/cli
python3 scripts/smoke-cli.py artifacts/nativeaot/cli/cli --report artifacts/nativeaot/smoke-cli.json
python3 scripts/release-cli.py --rid linux-x64
```

Windows에서는 Python 명령에 `artifacts/nativeaot/cli/cli.exe`를 전달합니다.
NativeAOT 게시에는 대상 OS와 일치하는 빌드 호스트 및 네이티브 도구 체인이 필요합니다.
세 OS의 실제 NativeAOT 게시와 파일 조회·다운로드·업로드 검사는
[CLI NativeAOT packages 실행](https://github.com/AlphaBs/FishSyncClient/actions/runs/40930935618)에서 모두 통과했습니다.
Windows는 windows-2022, Linux는 ubuntu-24.04, macOS는 macos-15 ARM64 호스트를 사용했습니다.

최종 빌드 로그와 실행 결과는 `artifacts/ci/40930935618/`에 내려받았습니다.
macOS ARM64 ZIP의 SHA256과 검증 요약은 `artifacts/nativeaot/release-1.1.0-macos-arm64.json`에 기록했습니다.
macOS 실행 파일의 ARM64 아키텍처와 `darwin/aarch64` 업데이트 채널 선택도 CI에서 확인했습니다.
배포물은 다음과 같습니다. 생성물은 Git에서 제외합니다.

- `artifacts/releases/FishSyncClientCli-win-x64-1.1.0.zip`
- `artifacts/releases/FishSyncClientCli-linux-x64-1.1.0.zip`
- `artifacts/releases/FishSyncClientCli-osx-arm64-1.1.0.zip`
