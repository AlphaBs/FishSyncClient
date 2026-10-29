# FishSyncClient CLI

기존 FishSyncServer bucket과 로컬 폴더를 동기화하는 CLI입니다. 이 ZIP에는 Release NativeAOT 실행 파일이 들어 있으며 .NET 런타임을 별도로 설치할 필요가 없습니다. ZIP 파일명에 표시된 OS와 아키텍처에서 실행하세요.

## 준비

1. ZIP을 전용 폴더에 풀고 터미널에서 그 폴더로 이동합니다. 아래 명령은 모두 이 폴더에서 실행합니다.
2. 사용할 계정, 기존 bucket ID, 동기화할 로컬 폴더의 절대 경로를 준비합니다. bucket 조회·생성 명령은 제공하지 않습니다. push에는 해당 bucket의 쓰기 권한이 필요합니다.
3. 이 폴더에 `.env` 파일을 만들고 계정 정보를 입력합니다.

```dotenv
FISH_USERNAME=my-account
FISH_PASSWORD='my-password'
```

프로세스 환경변수로 설정해도 됩니다. 기존 환경변수가 `.env`보다 우선하며 `.env`는 현재 작업 폴더에서만 읽습니다. 각 명령은 자동으로 인증하고 토큰은 메모리에서만 사용합니다. `login` 명령은 없습니다.

**동기화할 폴더는 실행 파일·config·인증용 `.env`를 보관하는 폴더와 분리하세요.** 로컬 루트 아래의 모든 파일이 동기화 대상이며 `.env`도 자동 제외하지 않습니다.

## Linux / macOS

`my-bucket`을 실제 bucket ID로, `/absolute/path/files`를 실제 로컬 경로로 바꿉니다. 로컬 폴더를 먼저 만드세요.

```sh
chmod +x ./cli
mkdir -p /absolute/path/files
./cli --help
./cli pull my-bucket --root /absolute/path/files
```

내려받은 파일을 편집한 뒤 로컬 상태를 서버에 반영하려면 실행합니다.

```sh
./cli push my-bucket --root /absolute/path/files
```

## Windows (PowerShell)

`my-bucket`과 `C:\sync\files`를 실제 값으로 바꿉니다.

```powershell
New-Item -ItemType Directory -Force 'C:\sync\files'
.\cli.exe --help
.\cli.exe pull my-bucket --root 'C:\sync\files'
```

내려받은 파일을 편집한 뒤 서버에 반영합니다.

```powershell
.\cli.exe push my-bucket --root 'C:\sync\files'
```

## 동기화 방향과 자동화

- `pull`: 서버를 기준으로 로컬 파일을 추가·덮어쓰기·삭제합니다. 서버는 변경하지 않습니다. 업로드할 로컬 수정이 있다면 pull을 먼저 실행하지 마세요.
- `push`: 로컬을 기준으로 서버 파일을 추가·덮어쓰기·삭제합니다. 빈 로컬 폴더를 push하면 서버 파일이 삭제될 수 있습니다.
- 양방향 병합이나 미리보기는 제공하지 않으며 변경을 즉시 반영합니다.
- `WarningPatterns`에 일치하는 경로는 콘솔에 출력하고 `[Y/n]`을 묻습니다. Enter 또는 Y는 진행, n 또는 입력 종료는 취소합니다. 패턴은 파일 제외 규칙이 아닙니다.
- 무인 실행 시 명령 끝에 `-y` 또는 `--yes`를 붙이면 경고를 기록하고 확인 입력을 생략합니다. 모든 파일이 동기화 대상인지 확인한 뒤 사용하세요.
- 종료 코드 `0`은 성공, `1`은 인자 오류·실패·취소입니다. 실패 시 일부 변경이 반영되었을 수 있으므로 결과를 확인하고 재시도하세요. PowerShell에서는 `$LASTEXITCODE`, sh에서는 `$?`로 확인합니다.

## 서버와 설정

기본 API 주소는 `https://fish2.snowfrost.kr/api`입니다. 다른 서버는 `--server https://your-server.example/api`로 지정합니다. 계정 정보가 없으면 `FISH_USERNAME`, `FISH_PASSWORD`를 설정하라는 오류가 발생합니다. 인증·권한 오류 시 계정, API 주소와 bucket 권한을 확인하세요.

`config/config.json`은 현재 작업 폴더 기준으로 읽습니다. `--config`로 다른 파일을 지정할 수 있으며 파일이 없으면 내장 기본 설정을 사용합니다. config에 비밀번호를 넣지 마세요.

| 설정 | 우선순위 |
| --- | --- |
| 서버 | `--server` → `FISH_SERVER` → config `Host` → 기본 서버 |
| bucket | 위치 인자 `bucket-id` → `FISH_BUCKET_ID` → config `BucketId` (필수) |
| 로컬 폴더 | `--root` → `FISH_ROOT` → config `Root` → 현재 작업 폴더 |

각 환경변수는 프로세스 환경변수 → `.env` 순서로 읽습니다. 의도하지 않은 폴더를 동기화하지 않도록 `--root`는 항상 절대 경로로 명시하는 것을 권장합니다.

명령별 자세한 옵션은 `./cli pull --help`, `./cli push --help`로 확인합니다. Windows에서는 `./cli` 대신 `.\cli.exe`를 사용합니다.
