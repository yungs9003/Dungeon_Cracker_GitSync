# 밀치기·당기기 함정 프로젝트 적용

2026-10-08 사용자의 검토 승인에 따라 두 함정과 정상 작동에 필요한 공통 보호 연결을 원본 프로젝트에 추가했다.

최초 적용 이후 사용자가 코드를 101-014/101-015로 변경하고 WallVisual을 교체했다. 아래 표는 현재 프리팹 기준이며, 초기 적용 기록과 검토용 ZIP은 제작 당시 상태로 보관한다.

| 프리팹 | 감지 | 이동 |
| --- | --- | --- |
| Assets/Resources/Prefabs/101-014.prefab | 벽 전방 1~2칸, 폭 0.6칸 | 발동 위치 + 벽 반대 방향 3칸 |
| Assets/Resources/Prefabs/101-015.prefab | 벽 전방 1~4칸, 폭 1칸 | 벽 바로 앞 1칸 중심 |

루트 로컬 +Y가 전방이다. 루트를 벽 타일 중심에 두고 방향을 회전한다. Resources/Prefabs 로드 규칙과 기존 맵 direction 규칙을 사용할 수 있다. Player 태그의 Rigidbody를 자동 연결하며 여러 플레이어를 사용하는 경우 `playerBody`를 직접 지정한다.

추가된 런타임 코드:

- Assets/Scripts/Core/PlayerEffectLock2D.cs
- Assets/Scripts/GamePlay/Player/ForcedMovementReceiver2D.cs
- Assets/Scripts/GamePlay/Traps/WallForceTrap2D.cs

원본 PJ-005는 기존 GUID로 참조하고 마스크만 Assets/Sprites/Traps/WallForceClip.png로 추가했다. 기존 코드 11개에 공통 보호 상태를 연결하고 Assets/Resources/Data/db.json의 traps 배열에 102-014/102-015를 등록했다. 검토용 씬은 원본 Assets에 추가하지 않았으며 실제 맵 JSON과 게임 씬에는 배치하지 않았다.

원본 코드·DB 12개 파일의 적용 전 사본과 변경 목록은 Docs/Rollback/2026-10-08-wall-force-traps-applied에 보관했다. 적용된 파일과 검토본은 같은 코드/프리팹 내용이다. 이 폴더의 README 및 ZIP은 적용 전 검토 기록이다.

검증 결과는 위 롤백 폴더의 Validation에 보관했다. 원본 Assets 소스로 Core, GamePlay, App, System, 기존 EditorTests와 Editor 도구 6개 어셈블리가 모두 컴파일됐다. 실제 추가된 프리팹 사본을 별도 Unity 프로젝트에서 재생성 없이 import하고 101개 동작 검사를 통과했다. 기존 맵/씬/플레이어 프리팹/저장 파일 및 기존 DB 레코드의 보존도 확인했다. 원본 프로젝트의 기존 게임 씬을 Play Mode로 실행한 결과와는 구분한다.

현재 이동·애니메이션 조정:

- 밀치기는 플레이어의 발동 시 옆 위치를 유지하며 직선으로 이동한다. 팔의 접근/이동/회수도 벽 전방 축을 유지하도록 수정했다. 감지 폭은 `101-014.prefab`의 `laneWidthCells=0.6`으로 조절하며, 플레이어 몸 중심이 중심선에서 좌우 0.3칸 안쪽에 있어야 발동한다. 전방 거리는 2칸이다.
- 당기기의 감지 폭/거리와 전방 1칸 중심 목적지는 유지한다. 두 함정 모두 이동 도중 벽/일반 장애물을 만나면 그 앞에서 멈춘다.
- PlayerAnimationDriver2D는 강제 이동 보호 중 `IsMoving`/`Knockback`을 false로 유지하여 Idle을 사용한다. 보호 중에도 현재 위치를 기록하므로 해제 직후 지난 이동으로 걷기를 시작하지 않으며 일반 이동 시 Run이 다시 작동한다.
- JsonBlockBuilder2D의 바닥 함정 발견 Sprite 교체 제외 코드를 101-014/101-015로 갱신했다. 사용자 변경 WallVisual과 DB·맵·씬은 보존한다.
- 이번 수정 전 사본과 경로·벽 충돌·Animator 검사 결과는 `Docs/Rollback/2026-10-08-wall-force-trap-polish`에 보관한다.

쿨타임 재발동:

- 두 함정 모두 팔 회수가 끝나면 Inspector의 `cooldown` 시간만큼 기다린 뒤 현재 감지 범위를 다시 판정한다. 플레이어가 계속 범위 안에 있어도 재발동하며, 이탈/재진입을 요구하지 않는다. 현재 두 프리팹에 사용자가 설정한 4초를 유지했다.
- 당기기 목적지에 이미 서 있거나 벽 때문에 이동 거리가 0이어도 팔 동작과 보호 사이클은 반복한다. 벽 관통은 하지 않는다. 다른 강제 이동이 플레이어를 보호 중이면 해당 보호가 풀린 뒤 발동한다.
- 작동 중 비활성화하면 보호를 해제하고, 재활성화 후 쿨타임을 거쳐 범위를 다시 검사한다. 쿨타임은 활성 상태에서 진행된다.
- 이번 원본 코드 2개의 수정 전후 사본과 반복 발동 검사는 `Docs/Rollback/2026-10-08-wall-force-trap-repeat`에 보관한다.
