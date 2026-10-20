# 지정 타일 맵 방향 보정

2026-10-20 KST. 기본 Sprite가 위(+Y)를 본다는 조건으로 주변 통로와 방향을 맞췄다.

- 전체 맵 JSON의 grid/3F에서 대상25곳을 확인했다. 모두 인접 바닥(000-/102-)이 한 방향으로만 열려 있었다.
- 방향 필드22곳을 수정/명시하고 이미 맞던3곳은 유지했다. 실제 방향 변경21곳과 기본값00을 명시한1곳이다.
- 수정 맵9개: CheckpointScene/checkpoint, DUNGEON_001/map_001~005, DUNGEON_002/map_002~004. DUNGEON_002/map_001은 검사만 했다.
- 방향은00위 / 01오른쪽 / 10왼쪽 / 11아래다. 코드·좌표·assetNo·legacy hidden·relation·player·field_effect·3F 등 다른 JSON 값과 원래 형식은 유지했다.

## 101-007 발사 기준 정합

기존 BallLauncher2D는 오른쪽(transform.right)을 발사 전방으로 사용했다. JSON만 Sprite의 위쪽 기준으로 보정하면 그림과 실제 공 발사가 직각으로 어긋나므로 transform.up으로 함께 맞췄다. 기본 생성 오프셋을 Y축으로 옮겼고, 기존101-007 프리팹 오프셋(-1.7,0)을(0,-1.7)로 회전해 크기를 보존했다. 속도3·쿨타임2·밀어내기/충돌/피해 로직은 유지한다.

## 타일별 확인 목록

| 맵 | 코드 | 좌표 | 이전 | 현재 | 처리 |
| --- | --- | --- | --- | --- | --- |
| CheckpointScene/checkpoint.json | 001-004 | 0308 | 01 | 11 | 수정 |
| DUNGEON_001/map_001.json | 001-003 | 0700 | 10 | 00 | 수정 |
| DUNGEON_001/map_001.json | 101-007 | 0804 | 11 | 10 | 수정 |
| DUNGEON_001/map_002.json | 001-002 | 0700 | 10 | 00 | 수정 |
| DUNGEON_001/map_003.json | 001-002 | 1401 | 00 | 10 | 수정 |
| DUNGEON_001/map_004.json | 101-007 | 0c0b | 11 | 10 | 수정 |
| DUNGEON_001/map_004.json | 101-007 | 0009 | 00 | 01 | 수정 |
| DUNGEON_001/map_004.json | 001-002 | 0c01 | 10 | 10 | 유지 |
| DUNGEON_001/map_005.json | 101-007 | 1117 | 11 | 10 | 수정 |
| DUNGEON_001/map_005.json | 101-007 | 0016 | 00 | 01 | 수정 |
| DUNGEON_001/map_005.json | 101-007 | 1115 | 11 | 10 | 수정 |
| DUNGEON_001/map_005.json | 101-007 | 0010 | 00 | 01 | 수정 |
| DUNGEON_001/map_005.json | 101-007 | 110f | 11 | 10 | 수정 |
| DUNGEON_001/map_005.json | 101-007 | 000e | 00 | 01 | 수정 |
| DUNGEON_001/map_005.json | 101-007 | 1109 | 11 | 10 | 수정 |
| DUNGEON_001/map_005.json | 101-007 | 0008 | 00 | 01 | 수정 |
| DUNGEON_001/map_005.json | 101-007 | 1107 | 11 | 10 | 수정 |
| DUNGEON_001/map_005.json | 001-002 | 1101 | 10 | 10 | 유지 |
| DUNGEON_002/map_001.json | 001-002 | 0700 | 00 | 00 | 유지 |
| DUNGEON_002/map_002.json | 001-002 | 0403 | 미지정(00) | 10 | 수정 |
| DUNGEON_002/map_003.json | 101-007 | 030a | 미지정(00) | 11 | 수정 |
| DUNGEON_002/map_003.json | 001-002 | 0709 | 미지정(00) | 10 | 수정 |
| DUNGEON_002/map_004.json | 101-007 | 1612 | 미지정(00) | 11 | 수정 |
| DUNGEON_002/map_004.json | 001-002 | 1B0E | 미지정(00) | 10 | 수정 |
| DUNGEON_002/map_004.json | 101-007 | 0A01 | 미지정(00) | 00 | 수정 |

## 검증

- 수정된 원본 게임 소스를 복사한 격리 Unity 프로젝트에서 컴파일과 실제 실행 검사200개가 통과했다.
- 대상25곳의 JSON 방향·실제 프리팹·원본 Sprite·위쪽 전방·인접 통로를 확인했다.
- 101-00715곳 모두 실제 공을 발사해 속도3 유지·그림 전방과 이동 방향 일치·전방 생성·옆으로 치우치지 않는 생성 위치를 확인했다. 실제 배치에 상하좌우 네 방향이 모두 포함된다.
- 맵 JSON의 의미 비교에서 지정한22개 direction 필드 외 다른 값이 바뀌지 않았음을 확인했다.
- 전체 게임 해시에서 JSON9개·발사 코드1개·프리팹1개만 수정됐고 추가/삭제는 없다. 적용11개는 격리 검증 사본과 동일하다.
- 사용자 실제 저장을 복사/실행하지 않았다. 검증용 저장/씬/카메라는 Library 캐시에만 추가했다. 새 제품 단위 테스트는 추가하지 않았다.
- Validation의 map001-portal.png·map001-ball-launcher.png·checkpoint-exit.png를 열어 배치를 시각 확인했다.
- Validation/runtime-checks.txt·runtime.log·data-checks.txt에 결과를 보관했다.

## 백업과 복원

Before에 수정 전11개, After에 최종11개, Diff에 차이, 전체 전/후 해시와 change-manifest.json에 적용 목록을 보관했다. 현재 프로젝트는 수정본 적용 상태다.

Unity를 종료한 뒤 프로젝트 루트 PowerShell에서 실행한다.

```powershell
# 복원 대상 미리보기
& '.\Docs\Rollback\2026-10-20-map-directions\Restore.ps1'
# 이번 방향 변경만 실제 복원
& '.\Docs\Rollback\2026-10-20-map-directions\Restore.ps1' -Apply
```

추가 사용자 수정이 발견되면 해시 검사로 중단한다. 이전 타일 재연결 기능은 이 백업의 시작 상태 그대로 유지하며 Docs는 복원 대상에 포함하지 않는다.
