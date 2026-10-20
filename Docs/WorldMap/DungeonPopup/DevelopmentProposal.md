# 던전별 기록·진행도 구현 명세

> 2026-10-20 시간 누적 개선으로 당시 저장 버전은4이며 후속 정수 초/클리어 랭크 작업으로 현재는5다. 아래 던전 기록/진행도 정책은 유지하며 새 총 시간 필드와 정기 저장 통합은 Docs/PlayTime/README.md를 참고한다.

2026-10-19 KST. 최초 설계안에 대한 사용자 검토 내용을 반영한 구현이다. 미클리어 기본값은 유지하고, 월드맵 입장은 저장된 체크포인트를 자동으로 이어간다. 별도 새 플레이 버튼은 추가하지 않았다.

## 던전 메타데이터

Assets/Resources/Data/db.json의 dungeons 배열을 공통 원본으로 사용한다.

```json
"dungeons": [
  { "sceneName": "DUNGEON_001", "number": 1, "name": "버려진 채굴장", "mapResourcePath": "Maps/DUNGEON_001" }
]
```

번호/이름은 sceneName으로 조회한다. Dungeon_001 같은 기존 표기는 DUNGEON_001로 정규화한다. 기존 Data/dungeons.json은 포인트 ID와 부가 설명을 연결하는 호환 어댑터이며, 번호/이름은 공통 DB를 우선한다. 월드맵 씬을 단독 실행해 GameDatabase 오브젝트가 없어도 같은 DB를 조회한다. 미래 던전은 DB 항목·씬/빌드 등록·해당 Maps 폴더를 함께 추가하면 된다.

DungeonMapCatalog가 팝업/입장과 MapSequenceManager2D의 맵 목록을 공유한다. map_숫자 이름의 TextAsset만 숫자 순서로 읽는다. 현재 DUNGEON_001은5개, DUNGEON_002 리소스는4개다. DUNGEON_002 씬은 아직 원본 프로젝트에 등록되어 있지 않으며 이번 작업에서 씬을 새로 만들지 않았다.

## savedata 버전3

기존 골드/인벤토리/장비/현재 결과는 보존한다. 아래는 일부 필드 예시이며 실제 미플레이 기본 totalMaps는0이고 입장 시 실제 개수를 기록한다.

```json
{
  "save_version": 3,
  "dungeon_records": {
    "DUNGEON_001": {
      "clear_count": 0,
      "best_clear_time_seconds": 3599,
      "best_hit_count": 399,
      "max_broken_traps": 0,
      "last_committed_clear_run_id": ""
    }
  },
  "checkpoint": {
    "DUNGEON_001": {
      "mapIndex": 2,
      "totalMaps": 5,
      "resumeRequested": true,
      "completed": false,
      "broken_traps_snapshot": 3,
      "run_id": "플레이 고유 ID",
      "run_time_seconds": 90,
      "run_hit_count": 4,
      "run_broken_traps": 3
    }
  },
  "active_run": {
    "sceneName": "DUNGEON_001",
    "run_id": "플레이 고유 ID",
    "pending_clear": false
  }
}
```

- dungeon_records는 실제 클리어 결과만 보관한다. 기본3599초(59:59)/399회/해체0/클리어0을 유지한다. 미클리어 상태에서 기본값을0으로 바꾸지 않는다.
- 첫 실제 클리어는 기본값보다 나쁜 결과여도 실제값을 저장한다. 이후 시간/피격은 최소, 해체는 최대를 독립적으로 비교한다.
- checkpoint.mapIndex는 체크포인트로 확정한 완료 맵 수이자 다음 맵의0 기반 인덱스다. 예를 들어2개 통과한 체크포인트는2이며 추가로+1/-1 하지 않는다.
- totalMaps는 실제 목록의 개수다. 첫 클리어 전에도 내부에 저장하지만 화면의 분모는?로 가린다.
- completed/resumeRequested가 완료 표시N/N과 마지막 맵 직후의 재개 가능한 체크포인트N/N을 구분한다.
- 던전별 run_id/현재 결과를 보관해 다른 던전에 다녀와도 시간/피격/해체가 섞이지 않게 한다. 최고 기록과 현재 플레이의 최상위 clear_time_seconds/hit_count/broken_traps는 별개의 값이다.
- active_run은 현재 선택한 던전과 완료 전환 상태다. 저장은 임시 파일/검증/원자적 교체를 사용하며 기록 갱신 실패 시 메모리도 이전 상태로 되돌린다.

## 입장과 체크포인트

| 상황 | 입장 버튼/진행도 처리 |
| --- | --- |
| 0/? 또는0/N, 재개할 체크포인트 없음 | 첫 맵으로 진입한다. 기존 미완료 플레이의 시간/피격은 이어서 보관한다. |
| n/? 또는n/N, 재개 가능한 체크포인트 있음 | 선택 던전의 현재 결과를 복원하고 CheckpointScene에 진입한다. 출구에서 저장된 다음 맵 인덱스로 진행한다. 이전 클리어 횟수는 이 판정에 영향을 주지 않는다. |
| 클리어 직후N/N, completed=true | 완료 표시는 유지한다. 다음 입장은 첫 맵에서 진행도0/N으로 시작하며 클리어 횟수는 증가하지 않는다. 별도 새 플레이 버튼은 없다. |
| 마지막 맵 체크포인트N/N, completed=false | 체크포인트에서 이어간다. 출구를 나가 실제 ClearScene으로 이동할 때 한 번 완료한다. |

팝업을 열거나 도움말을 보는 것만으로 기록을 쓰지 않는다. 체크포인트를 거치지 않고 맵만 진행하면 저장 진행도는 증가하지 않는다. 이후 체크포인트 도착 시2→4 등 실제 완료 수를 반영한다. 사망 시 해체 수만 기존 스냅샷으로 복원하고 시간/피격은 기존 정책대로 누적한다. 체크포인트가 없는 사망은 해체0으로 되돌리며 다른 던전의 저장 지점은 지우지 않는다.

진행도 표시는 clear_count==0이면 mapIndex/?, 1 이상이면 mapIndex/totalMaps다. 예시 N=6에서는0/?→2/?→6/6, 완료 후 입장0/6→체크포인트2/6→4/6으로 보인다. 실제 현재 첫 던전의 분모는5다.

## 완료 집계와 보수

일반 마지막 맵과 마지막 체크포인트 복귀 경로를 SaveManager.CompleteDungeon으로 모았다. 시간을 먼저 flush하고 원래 던전/run_id/결과를 pending_clear로 저장한 뒤 ClearScene으로 전환한다. 진입 시 같은 run_id를 한 번만 확정한다. ClearResultUI.Refresh의 재시도나 씬 재로드로 횟수가 중복 증가하지 않는다. 완료 시 mapIndex=N, completed=true, resumeRequested=false로 저장한다.

저장 실패 시 체크포인트/완료 전환을 진행하지 않는다. 완료 전환 요청이 저장된 뒤 프로그램이 종료된 경우, 다음 입장에서 ClearScene을 먼저 열어 미완료 집계를 복구한다. 유효한 완료 요청 없이 ClearScene만 직접 열면 기록/보수를 지급하지 않는다. 기존 보수 계산식과 보수 수령 중복 방지는 유지한다.

## 길드 조건 검사 시 사용할 값

기본값은 실제 성과가 아니다. 개별 길드 도전과제 구현 시에는 아래처럼 실제 클리어 여부부터 확인한다.

```csharp
if (!DungeonProgressService.TryGetClearedRecord(save.Data, sceneName, out var record))
    return false;
return record.best_hit_count <= allowedHits;
```

시간 과제는 best_clear_time_seconds, 피격 과제는 best_hit_count, 해체 과제는 max_broken_traps, 반복 클리어 과제는 clear_count를 사용한다. 최상위 현재 플레이 결과를 최고 기록으로 검사하지 않는다. 길드의 기존 미리보기 인증 시스템은 그대로이며, 실제 개별 조건/보수/수령 기록 연결은 아직 별도 작업이다.

## 기존 저장 파일 이행

- 버전2의 단일 checkpoint 객체는 역직렬화 전에 sceneName별 사전으로 변환한다. 원래 mapIndex, 재개 여부, 해체 스냅샷과 현재 결과를 보존한다. 기존 sceneName 대소문자도 정규화한다.
- 과거 dungeon_status 과제 플래그나 한 번의 플레이 결과로 누적 클리어/최고 기록을 추정하지 않는다. 신규 기록은 기본값부터 시작한다.
- 실행 시 활성 저장 파일을 읽고 버전3으로 원자적 저장한다. 기존 파일은 저장소의 백업에 남는다. Editor에서는 StreamingAssets/savefile.test.json이 활성 파일이며 Library/DungeonSaveBackups에 직전 정상본을 보관한다.
- Assets/StreamingAssets/savefile.json은 새 게임 기본 템플릿만 갱신했다. 사용자 실제 savefile.test.json은 개발 도구로 수정하지 않았으며 다음 실행에서 정상 로드 경로로 이행한다.
- 더 높은 미지원 저장 버전은 역직렬화 검증에서 거부한다. 저장소의 기존 복구 정책에 따라 원본 사본을 보존하고 검증된 백업/기본본으로 복구한다.

검증 자료: Docs/Rollback/2026-10-18-dungeon-records. 임시 검증 도구: Temp/DungeonRecords. 원본 씬/프리팹/지도 배치에는 변경하지 않고 공용 코드 연결로 적용했다.
