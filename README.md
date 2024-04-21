# FishSyncClient

FishSyncClient 는 파일 동기화를 위한 .NET 라이브러리입니다. 

## 주요 기능

- 파일 목록 비교
- 파일 내용 비교 (파일 크기, 체크섬 비교)
- 동기화를 위한 파일 복사, 삭제

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
