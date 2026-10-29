# FishSyncClient CLI

로컬 폴더와 FishSyncServer bucket을 동기화하는 CLI입니다. 실행 파일 하나로 동작하며 .NET 런타임은 필요 없습니다.

## 시작하기

1. ZIP을 전용 폴더에 풀고 터미널에서 그 폴더로 이동합니다. `config/config.json`과 `.env`는 현재 작업 폴더 기준으로 읽습니다.
2. 같은 폴더에 `.env`를 만듭니다. 환경변수로 지정해도 되고, 환경변수가 `.env`보다 우선합니다.

   ```dotenv
   FISH_USERNAME=my-account
   FISH_PASSWORD='my-password'
   ```

3. 실행합니다. `my-bucket`은 기존 bucket ID로 바꾸고, `--root`에는 **절대 경로**를 씁니다.

   ```sh
   ./cli pull my-bucket --root /absolute/path/files   # 서버 → 로컬
   ./cli push my-bucket --root /absolute/path/files   # 로컬 → 서버
   ```

   Windows PowerShell에서는 `.\cli.exe pull my-bucket --root 'C:\sync\files'`처럼 실행합니다.

**동기화 폴더는 이 폴더(실행 파일, config, `.env`)와 분리하세요.** root 아래의 모든 파일이 동기화 대상입니다.

버킷의 파일 목록만 보려면 `./cli files my-bucket`을 실행합니다. 파일 경로, 크기(바이트), MD5 체크섬을 출력합니다. 인증과 서버 설정은 동기화 명령과 같습니다.

## 주의

- `pull`은 로컬을 서버와 똑같이 맞춥니다. 로컬에서 바뀐 파일은 덮어쓰고 로컬에만 있는 파일은 삭제합니다. 업로드하지 않은 수정이 있으면 먼저 `push`하세요.
- `push`는 서버를 로컬과 똑같이 맞춥니다. 빈 폴더를 push하면 서버 파일이 모두 삭제됩니다.
- 변경은 즉시 적용되며 미리보기(dry-run)나 양방향 병합은 없습니다.
- config의 `WarningPatterns`에 일치하는 파일이 있으면 목록을 보여주고 `[Y/n]`으로 확인합니다. 이 패턴은 경고용이며 해당 파일을 동기화에서 빼 주지 않습니다. 무인 실행에서는 `-y`로 확인을 건너뜁니다.

## 설정

우선순위: **명령행 옵션 > 환경변수 > `.env` > `config/config.json` > 기본값**

| 항목 | 옵션 | 환경변수 | config 키 | 기본값 |
| --- | --- | --- | --- | --- |
| bucket ID | 위치 인자 | `FISH_BUCKET_ID` | `BucketId` | 없음 (필수) |
| 로컬 폴더 | `--root` | `FISH_ROOT` | `Root` | 현재 작업 폴더 |
| 서버 API | `--server` | `FISH_SERVER` | `Host` | `https://fish2.snowfrost.kr/api` |

다른 config 파일은 `--config FILE`로 지정합니다. 파일이 없으면 내장 기본값을 씁니다. 인증 토큰은 실행할 때마다 새로 받고 메모리에만 둡니다.

종료 코드는 `0` 성공, `1` 인자 오류·실패·취소입니다. 실패한 동기화도 일부 변경이 적용되었을 수 있습니다.

자세한 옵션은 `./cli pull --help`, `./cli push --help`에서 확인하세요.
