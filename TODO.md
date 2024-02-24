# FiSH Client

.NET Client for FiSH Server (File Syncing and Hosting Server)

- [x] 경로 구분자 하나만 사용하도록 PathHelper 수정
- [x] updateblacklists 에서 디렉토리도 설정 가능하게 수정
- [x] 버전 확인
- [ ] string -> ReadOnlySpan<char> 적용
- [x] FishFileComparer 양쪽 모두 FishPath 사용 가능하게 수정, 동시에 root 인수 제거 
- [x] Alphabet 이랑 FishServer 랑 공통되는 부분만 따로 뽑아내기
- [ ] RootedPath 절대경로?
- [x] ServerSyncer 에서 SyncIncludes 이랑 DeletedFiles 합치기
- [ ] 배열에서 다른 인터페이스로 전환 
- [x] Fish prefix 전부 제거하기 
- [x] FishPath -> SyncFile, FishFileMetdata -> SyncFileMetadata, FishServerFile -> SyncServerFile
- [ ] CancellationToken 지원 

# Update Process

## Source -> Target

- source: 업데이트 원본 파일
- target: 업데이트 할 파일들

업데이트의 목표는 target 파일들의 내용을 source 와 동일하게 만드는 것이다. 이상적으로는 source 파일의 내용과 target 파일의 내용 전체를 비교하여 달라진 부분이 있으면 source 파일을 target 파일로 복사하는 등 파일 내용을 같게 만들어야 하지만, 현실적으로는 아래와 같은 문제가 있기 때문에 그렇게 하지 않는다. 

1. 성능: 항상 모든 파일을 비교하면 시간이 오래 걸린다. 따라서 source 집합에 버전을 부여하고 버전이 바뀐 경우에만 모든 파일을 비교한다. 버전이 같다면 파일의 메타데이터(파일 크기 등)만을 비교하여 빠르게 업데이트한다. 이때 무결성 유지가 중요한 파일들은 따로 목록을 만들어 버전과 관계 없이 항상 파일을 비교하도록 한다. 

2. 설정 파일: 일반적인 시나리오에서 source 는 초기 설정 파일을 가지고 있으며, 업데이트 이후 유저는 target 의 설정 파일을 수정하기를 원할 것이다. 하지만 항상 모든 파일을 비교한다면 유저가 직접 수정한, 즉 의도적인 파일 수정 조차도 허용하지 않기 때문에 업데이트 할 때마다 설정이 초기화될 것이다. 따라서 최초 파일 다운로드 이후 업데이트에서 제외할 파일 목록을 만들어 설정 파일은 업데이트 되지 않도록 한다.

## Files

### 일반 파일 - source 버전이 업데이트되었을 때

- 추가된 파일: 항상 다운로드
- 중복 파일: 파일 체크섬 비교 후 파일이 달라졌으면 다시 다운로드
- 삭제된 파일: 삭제

### 일반 파일 - source 버전과 target 버전이 같을 때

- 추가된 파일: 항상 다운로드
- 중복 파일: 파일 크기만 비교, 달라진 파일은 다시 다운로드
- 삭제된 파일: 유지

### SyncIncludes

패턴과 일치하는 파일은 sources 와 targets 를 항상 동일하게 동기화. 

- 추가된 파일: 항상 다운로드
- 중복 파일: 항상 체크섬 검사해서 다르면 다시 다운로드
- 삭제된 파일: 항상 삭제

### SyncExcludes

패턴과 일치하는 파일은 업데이트 하지 않음.

- 추가된 파일: 항상 다운로드
- 중복 파일: 검사하지 않음, 업데이트하지 않음
- 삭제된 파일: 삭제하지 않음

## Process

1. sources 의 파일 경로와 targets 의 파일 경로를 비교
    - sources 에만 존재하는 경로 (추가된 파일)
        - 추가된 파일, **다운로드 대상**
    - sources 와 targets 모두 존재하는 경로 (중복 파일)
        - SyncExcludes 패턴과 비교
            - 패턴과 일치하는 경로는 파일을 비교하지 않음 (`options.txt`, `saves/**`, `screenshots/**` 등)
            - 패턴과 일치하지 않는 경로들은 파일 비교 대상
                1. source 버전과 target 버전 비교
                    - target 버전이 source 버전과 일치하는 경우
                        - SyncInclude 패턴과 비교
                            - 패턴과 일치하는 경로는 `ChecksumComparer` 사용하여 파일 비교 (`mods/**` 처럼 무결성 유지가 중요한 파일들)
                            - 패턴과 일치하지 않는 경로는 빠른 파일 비교를 위하여 `SizeComparer` 사용하여 파일 비교
                    - target 버전이 source 와 일치하지 않는 경우, 즉 source 버전이 업데이트 된 경우
                        - 모든 파일을 `ChecksumComparer` 을 사용하여 파일 비교
                2. 선택한 Comparer 를 이용하여 파일 비교 수행
                    - 같은 파일은 유지
                    - 다른 파일은 새로 업데이트된 파일, **업데이트 대상**
    - targets 에만 존재하는 경로, (삭제된 파일)
        - source 버전과 target 버전 비교
            - target 버전이 source 버전과 일치하는 경우
                - SyncIncludes 패턴과 비교
                    - 패턴과 일치하는 경로는 **삭제 대상** (`mods/**` 처럼 무결성 유지가 중요한 파일들)
                    - 패턴과 일치하지 않는 경우는 삭제하지 않음 (버전이 바뀔 때 삭제)
            - target 버전이 source 버전과 일치하지 않는 경우
                - SyncExcludes 패턴과 비교
                    - 패턴과 일치하는 경로는 삭제하지 않음 (`options.txt`, `saves/**`, `screenshots/**` 등)
                    - 패턴과 일치하지 않는 경로는 **삭제 대상**
2. target 의 버전을 source 의 버전으로 설정
