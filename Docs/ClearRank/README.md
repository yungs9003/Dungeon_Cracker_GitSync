# 던전 소요시간·클리어 랭크

2026-10-20 구현. [사용자 제공 공유 대화](https://chatgpt.com/s/t_6ad75b3d72ac8191ae229e0f2b4f1c9a)의 명시된 계산식을 사용한다. 웹 조회 도구에서는 열리지 않아 인앱 브라우저의 공유 본문으로 확인했다.

## 시간 표시와 저장

클리어 화면의 소요시간은 MM:SS다. 예:127.54초→127초→02:07. 반올림하지 않는다. 현재 clear_time_seconds, 던전별 checkpoint의 run_time_seconds, dungeon_records의 best_clear_time_seconds는 모두 정수형이며 JSON에도 정수 초로 기록한다.

작은 프레임 시간은 타이머 내부에서 계속 누적하고 완성된 초만 데이터에 반영한다.0.6초마다 저장하더라도 실행 중1.2초가 지났으면1초가 되어야 하므로 프레임마다 deltaTime을 버리지는 않는다. 초 미만 던전 시간을 별도 저장 필드에 넣지는 않는다. 체크포인트 대기 중 정지/복귀 재개와 클리어 후 정지는 유지된다. 총 플레이타임의 분 단위 저장·잔여 초 합산 정책은 별개로 유지했다.

저장 버전은5다. 기존 버전4 이하의 소수 시간은 로드 시 JObject 단계에서 명시적으로 버린 후 정수로 읽는다. Json.NET의 숫자 변환 반올림에 맡기지 않는다. 기존 기록·체크포인트·인벤토리/골드 등은 보존하고, 이행 전 정상 파일은 기존 원자적 저장의 직전 백업에 남긴다. 사용자 실제 활성 세이브는 개발 도구로 수정하지 않았으며 다음 정상 실행에서 이행한다.

## DB 기준값

Assets/Resources/Data/db.json의 dungeons 배열에서 sceneName으로 던전을 찾는다. Dungeon_001은 기존 DUNGEON_001 항목을 사용한다.

```json
{
  "sceneName": "DUNGEON_001",
  "number": 1,
  "name": "버려진 채굴장",
  "mapResourcePath": "Maps/DUNGEON_001",
  "rank_reference_hit_count": 20,
  "rank_reference_time_seconds": 120
}
```

다른 던전에도 위 두 양수 기준을 추가하면 같은 계산식을 사용한다. 기준이 없거나0/음수인 던전에는 임의의 랭크를 부여하지 않는다.

## 공통 계산식

H는 DB 기준 피격, T는 DB 기준 초, h와 t는 이번 플레이의 실제 피격/정수 초다.

- 피격 점수 =100×clamp((3H−h)/(2H),0,1). 기준 이하100,3배 이상0, 사이 선형 감소.
- 시간 점수 =100×clamp((2T−t)/T,0,1). 기준 이하100,2배 이상0, 사이 선형 감소.
- 합계 =피격 점수×0.6+시간 점수×0.4.
- S≥90, A≥75, B≥50, C<50. 반올림 전 점수를 사용한다.89.96점은A다.

계산은 공통 DungeonClearRankCalculator에서 decimal로 처리한다. 서로 다른 플레이의 최소 시간/최소 피격을 조합하지 않고 현재 완료한 런의 clear_time_seconds/hit_count를 사용한다. 미클리어 기본값으로 랭크를 얻지 않도록 실제 완료한 런인지 먼저 확인한다.

| 피격 | 시간 | 합계 | 랭크 |
| ---: | ---: | ---: | :---: |
|20|120초|100|S|
|24|127초|약91.67|S|
|20|150초|90|S|
|20|195초|75|A|
|40|180초|50|B|
|60|240초|0|C|

## 클리어 화면

ClearScene의 Canvas/UIPanel/RightBoxPanel에 표시한다. ClearRankImage는340×340, 위치(-45,35)이며 뒤쪽 오른쪽 아래의 DungeonCompletedImage는330×330, 위치(65,-25)다. DungeonCompletedImage는 Assets/Sprites/UI/Guild/DungeonCompleted.png를 직접 참조하고 원본 PNG에 포함된 투명도를 그대로 사용한다. 문양 전체가 패널 안에 들어오며 마스킹하지 않는다. 두 Image는 비율 유지·Raycast 비활성이고 귀환 버튼과 겹치지 않는다. 랭크가 표시될 때 문양도 함께 표시된다. ClearResultUI의 sRankSprite/aRankSprite/bRankSprite/cRankSprite는 Assets/Sprites/UI/Window의 S_RANK_0/A_RANK_0/B_RANK_0/C_RANK_0를 직접 참조한다. 현재 텍스처가 단일 Sprite 모드이므로 참조 FileID는21300000이다. 과거 다중 Sprite 메타데이터의 내부 ID를 사용하지 않는다.

현재 결과 화면에 랭크를 계산·표시하는 기능이다. 별도 최고 랭크 저장/모험가 전체 랭크/길드 보수/도전과제 조건은 추가하지 않았다. 기존 클리어 보수 계산은 그대로다.

## 검증

- 원본6개 어셈블리 컴파일 통과.
- 관련 NUnit69개 통과: 새 랭크/정수 시간27개, 기존 시간19개·던전 기록16개·저장소7개.
- 실제 ClearScene 격리 실행31개 통과:127.54→127/02:07, 실제 저장 JSON 정수, 최초 완료 집계, 네 Sprite 참조/이미지 전환, 귀환 버튼 비가림, 현재 결과와 최고 기록 분리, 미완료/미설정 던전 랭크 미부여, 재갱신 중복 방지.
- 1920×1080의 S/A/B/C 렌더를 보관하고 S/C 화면을 열어 시각 확인했다. 사용자 활성 세이브로 실험하지 않았다.

변경 전/후·해시·차이·단위/실행 로그·렌더는 Docs/Rollback/2026-10-20-clear-rank. 실제 적용 범위와 기존 미해결 회귀는 validation-summary.md를 참고한다.

후속 배경 배치 검증: Docs/Rollback/2026-10-20-clear-rank-backdrop. 표시 어셈블리 컴파일과 실제 ClearScene 검사38개 및 네 랭크 렌더를 확인했다.
