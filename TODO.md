# FiSH Client

.NET Client for FiSH Server (File Syncing and Hosting Server)

- [x] 경로 구분자 하나만 사용하도록 PathHelper 수정
- [x] updateblacklists 에서 디렉토리도 설정 가능하게 수정
- [x] 버전 확인
- [] string -> ReadOnlySpan<char> 적용
- [] API 단순화
- [x] FishFileComparer 양쪽 모두 FishPath 사용 가능하게 수정, 동시에 root 인수 제거 
- [] Alphabet 이랑 FishServer 랑 공통되는 부분만 따로 뽑아내기