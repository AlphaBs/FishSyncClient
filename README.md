# FishSyncClient

FishSyncClient 는 파일 동기화를 위한 .NET 라이브러리입니다. 

## 주요 기능

- 파일 목록 비교
- 파일 내용 비교 (파일 크기, 체크섬 비교)
- 동기화를 위한 파일 복사, 삭제

checksum을 생략하면 대상 파일의 존재 여부만 확인합니다. 제공된 checksum의 검증 규칙과 비교기별 책임은 [checksum 계약](docs/checksum-policy.md)에 정리되어 있습니다.

기대 Metadata는 생성 시 고정하는 불변 record이며, HTTP 응답 크기는 Metadata를 변경하지 않고 진행률에 반영합니다. API 변경사항과 동등성 규칙은 [불변 Metadata와 HTTP 진행률](docs/immutable-metadata.md)을 참고하세요.

## FishSyncServer 연동

- 서버에서 파일 목록, 내용 비교
- PULL: 동기화가 필요한 파일 서버에서 다운로드
- PUSH: 동기화가 필요한 파일 서버로 업로드

# FishSyncClient.Cli

FishSyncServer 와 동기화를 위한 CLI 툴

## PULL

`pull <bucket-id> --server <server-endpoint> --root <directory-to-sync>`

`pull "my-bucket" --server https://localhost:7128/api --root /home/syncroot`

## PUSH

`push <bucket-id> --server <server-endpoint> --root <directory-to-sync>`

`pull "my-bucket" --server https://localhost:7128/api --root /home/syncroot`

# FishSyncClient.Gui

FishSyncServer 와 동기화를 위한 GUI 툴

## 작업 흐름

1. 계정 아이디, 비밀번호, 버킷 ID를 입력하고 **다음**을 누릅니다. 기본 서버는 `https://fish.snowfrost.kr/api`이며 로그인 화면의 서버 API 주소에서 변경할 수 있습니다.
2. 서버 파일을 비교하고 실행 파일 옆 `buckets/<버킷ID>`로 내려받습니다. **다음을 누를 때마다 서버 기준으로 초기화하며, 업로드하지 않은 로컬 수정은 덮어쓰고 로컬에만 있는 파일은 삭제합니다.** 다운로드와 체크섬 검증에 실패하면 삭제를 시작하지 않습니다.
3. 완료하면 파일 탐색기가 열리고 파일 리스트가 나타납니다. 파일 감시 이벤트와 5초 주기 재탐색으로 갱신합니다. 초록은 추가, 빨강은 삭제 예정, 노랑은 실제 체크섬 또는 크기가 다른 파일입니다. 삭제 예정 파일은 서버 반영 전까지 리스트에 남습니다.
4. **동기화**를 누르면 변경사항 화면에서 최신 서버 목록과 로컬 체크섬을 비교합니다. 추가·삭제·갱신 목록이 나타나고 바로 로컬 상태를 서버에 반영합니다. 이름 변경은 삭제와 추가로 표시됩니다. 변경사항이 없으면 서버 반영 요청을 보내지 않습니다.
5. 전송용 복사본의 체크섬을 검증한 뒤 업로드합니다. 비교 이후 추가된 파일이나 전송 중 편집한 파일은 다음 동기화 대상으로 남습니다. 실패하거나 취소하면 일부 작업이 반영되었을 수 있으므로 다시 동기화합니다.

계정 아이디와 버킷 ID는 실행 파일 옆 `config/config.json`에 저장합니다. 비밀번호와 로그인 토큰은 저장하지 않습니다. 실행 파일 폴더에 쓰기 권한이 필요합니다. 다른 위치의 기존 로컬 폴더를 자동으로 가져오지는 않습니다.

작업 화면은 서버 용량과 파일 개수의 사용률 바, 월 동기화 잔여 횟수, 만료일까지 남은 기간, 읽기 전용 여부를 표시합니다. 진입 시, 동기화 완료 후, 60초마다, 새로고침 버튼으로 조회합니다. 조회 실패 시 마지막 값과 실패 상태를 유지합니다. 바는 로컬 총 용량과 파일 수를 서버의 각 한도로 나눈 비율이며, 로컬 목록 갱신 시 함께 갱신합니다. 한도를 초과한 바는 빨간색으로 표시하고 한도 이내로 돌아오면 원래 색으로 복원합니다. 로컬 파일 수·총 용량·파일당 크기가 양수로 지정된 제한을 초과하면 비교 후 업로드 전에 중단하고 하단에 오류를 표시합니다. 제한과 정확히 같은 경우는 허용합니다. 제한값의 특수값은 무제한으로 추정하지 않고 반환된 값을 표시하며 최종 제한 검증은 서버가 수행합니다.

파일 리스트와 동기화 모두 실제 MD5 체크섬을 비교하며 수정 시각으로 변경 여부를 판단하지 않습니다. 초기 PULL은 적용 전후 로컬 체크섬을 다시 검사하고, 도중에 변경된 파일을 최대 3회 재처리합니다. 임시 다운로드는 버킷과 같은 볼륨의 별도 폴더에 짧은 이름으로 보관합니다. 서버 업로드 성공 후 목록 조회·갱신이 실패해도 성공 결과는 유지하고 갱신 문제를 별도로 표시합니다.

버킷 폴더의 심볼릭 링크·정션과 버킷 밖 경로는 허용하지 않습니다. 폴더는 별도 항목으로 표시하지 않으며 파일의 상대 경로를 한 줄씩 표시합니다. 화면은 라이트 테마로 고정합니다.

## 실행 및 검증

```powershell
dotnet run --project gui/gui.csproj
dotnet test gui.tests/gui.tests.csproj
dotnet test test/FishSyncClientTest.csproj
dotnet build gui/gui.csproj -c Release
```

GUI 테스트는 가짜 HTTP 서버와 Avalonia Headless를 사용하므로 실제 계정이나 서버 파일을 변경하지 않습니다. `FISH_UI_SCREENSHOTS` 환경 변수에 출력 폴더를 지정하면 로그인·작업·미리보기 화면 PNG도 생성합니다.

## 플랫폼별 GUI 배포 빌드

[go-task](https://taskfile.dev/docs/installation)와 .NET SDK를 설치한 뒤 저장소 루트에서 실행합니다.
배포판에서 실행 파일 이름이 `go-task`인 경우 아래 명령의 `task`를 `go-task`로 바꿉니다.
ZIP 생성은 빌드를 실행하는 OS에 따라 Linux·macOS에서는 `zip`, Windows에서는 기본 Windows PowerShell의 `Compress-Archive`를 사용합니다. Linux·macOS에는 `zip` 명령이 필요합니다.

```sh
task windows          # Windows x64
task linux            # Linux x64
task darwin           # macOS Intel x64
task build            # 위 세 플랫폼을 순서대로 빌드
task darwin ARCH=arm64 # macOS Apple Silicon
```

모든 플랫폼은 기본 `ARCH=x64`이며 `ARCH=arm64`로 변경할 수 있습니다. 결과는 `artifacts/publish/<RID>/`에 두 파일로 생성됩니다 (`win-x64`, `linux-x64`, `osx-x64` 등).

- 단일 실행 파일: Windows는 `gui.exe`, Linux와 macOS는 `gui`
- `gui-<RID>.zip`: 실행 파일과 `config/config.json`을 포함하며, 압축을 풀면 실행 파일 옆에 `config/` 디렉토리가 생성됩니다. Linux·macOS에서 `zip`으로 패키징하면 실행 권한도 보존합니다. Windows에서 패키징한 Linux·macOS 바이너리는 압축 해제 후 `chmod +x gui`가 필요할 수 있습니다.

저장소의 `config/config.json`이 배포용 기본 설정입니다. 서버 주소와 경고 패턴을 여기서 변경할 수 있으며, 계정 정보와 토큰은 비워 둡니다. 빌드 중간 파일은 `artifacts/staging/<RID>/`에 생성합니다.

빌드는 .NET 런타임과 네이티브 라이브러리를 포함한 self-contained 단일 실행 파일입니다. 별도 .NET 설치는 필요하지 않으며, 네이티브 라이브러리는 실행 시 임시 폴더에 추출됩니다. 디버그 심볼은 실행 파일에 포함합니다. 실행 후 생성되는 `config/`와 `buckets/`는 실행 파일 옆에 저장됩니다.
