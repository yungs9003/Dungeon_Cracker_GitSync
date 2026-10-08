# 벽 밀치기 / 당기기 함정 검토본

**2026-10-08 적용 완료:** 검토 승인 후 두 프리팹과 필요한 연결 코드/DB 레코드를 원본 프로젝트에 추가했습니다. 현재 사용 방법과 적용 범위는 [APPLIED.md](APPLIED.md)에 있습니다. 아래 내용과 ZIP은 적용 전 검토 시점의 기록입니다.

이 폴더는 **미적용 검토본**입니다. 원본 `Assets`, `Packages`, `ProjectSettings`, 씬, DB, 저장 파일은 변경하지 않습니다. `New/Assets`에는 새 코드와 실제 Unity 프리팹, `Integration/After`에는 기존 코드에 연결할 수정본이 있습니다. 변경 범위는 `Integration/integration.patch`로 비교할 수 있습니다.

## 동작과 기본값

| 항목 | 102-014 밀치기 | 102-015 당기기 |
| --- | --- | --- |
| 감지 | 벽 전방 1~2칸, 폭 1칸 | 벽 전방 1~4칸, 폭 1칸 |
| 도착 | 발동 위치 + 전방 3칸 | 벽 중심 + 전방 1칸의 중심 |
| 피해 / 일반 피격 넉백 | 0 / 0 | 0 / 0 |
| 파괴 | 불가 | 불가 |
| 팔 접근 → 플레이어 이동 → 팔 회수 | 0.12초 → 0.35초 → 0.18초 | 동일 |
| 보호 | 접근 시작부터 팔 회수 완료까지 | 동일 |
| 다시 발동 | 0.8초 쿨타임 후 범위를 벗어났다 재진입 | 동일 |

첨부 표의 “발동 위치 기준 세 타일 뒤”는 벽에서 멀어지는 방향으로 3칸 이동하는 것으로 해석했습니다. 감지 범위는 원형 반경이 아니라 **정면 한 줄**이며, 플레이어 Rigidbody 중심이 해당 칸에 들어오면 발동합니다. 밀치기는 발동 시점 위치를 유지한 채 3칸 더합니다. 당기기는 벽 바로 앞 타일 중심에 정렬합니다. 이 부분과 동작 시간은 검토 후 조정할 수 있습니다.

1칸 = 1 Unity unit, 루트 로컬 +Y가 전방입니다. 맵 direction 규칙(00 위 / 01 오른쪽 / 10 왼쪽 / 11 아래)에 맞습니다. 감지 앞 경계 0.5칸은 포함하며, 끝 경계 2.5/4.5칸과 옆 경계 ±0.5칸은 제외합니다. 루트 scale은 (1,1,1)을 권장하고 칸 크기는 `cellSize`로 조절합니다.

## 프리팹 구조

```text
102-014 또는 102-015                 ← 벽 타일 중심
├─ BoxCollider2D (0.9 × 0.9, solid)  ← 평소 벽 충돌
├─ WallForceTrap2D                  ← 감지/상태 전환/파괴 금지
├─ WallVisual / SpriteRenderer      ← 기존 001-001 기본 벽 Sprite
└─ ArmAssembly / SortingGroup       ← 프리팹마다 마스크 범위 분리
   ├─ FrontWindow / SpriteMask      ← 벽 전방 0.5칸 이후만 노출
   └─ MovingTip                     ← 숨은 끝점: 전방 0.62칸
      └─ PJ-005 / SpriteRenderer    ← 원본 Sprite 참조, VisibleInsideMask
```

팔의 오른쪽 끝이 전방을 향하도록 원본 PJ-005를 90도 회전하고, 끝점에 피벗을 맞춥니다. 뒤쪽은 마스크로 잘려 벽 뒤에서 비치지 않으며 평소 약 0.12칸의 끝부분만 나옵니다. 팔의 길이는 최대 밀치기 도달 범위를 덮도록 6칸으로 맞췄고 두께는 기존 Sprite 두께를 유지했습니다. 원본 PNG와 import 설정은 수정하지 않습니다.

`WallForceClip.png`는 Unity 코드로 생성한 흰색 사각 마스크입니다. 새 그림이 아니라 렌더링 범위를 정하는 도형입니다. 벽의 모양은 `WallVisual.sprite`에서 교체할 수 있습니다. 이번 초안은 기본 벽 Sprite 한 종류를 사용하고 wall assetNo 변형 연결은 포함하지 않습니다.

## 코드 역할

- `PlayerEffectLock2D` (Core): 소유자 한 명만 획득 가능한 보호 잠금. `Rigidbody2D.simulated`를 잠시 끄고 완료/중단 시 원래 상태로 복구합니다. 일반 피격 무적 옵션이 꺼져 있어도 동작합니다.
- `ForcedMovementReceiver2D` (GamePlay/Player): 기존 넉백을 취소하고 팔이 지정한 위치로 플레이어를 이동시킵니다. 플레이어의 실제 충돌 크기를 기록하여 이동 전과 각 이동 단계에 BoxCast로 벽을 검사합니다.
- `WallForceTrap2D` (GamePlay/Traps): Idle → Reaching → Moving → Retracting → Cooldown → WaitingForExit 상태로 동작합니다. 좌표 감지 방식이라 보호 중 물리가 꺼진 플레이어도 자기 이동만 추적할 수 있습니다. 현재 파괴 시스템의 대상으로 등록되지 않도록 `Destructible` 컴포넌트를 넣지 않았으며 향후 인터페이스 연동에도 파괴 불가를 반환합니다.

새 함정이 처음 발동할 때 플레이어 루트 Rigidbody에 `ForcedMovementReceiver2D`와 필수 `PlayerEffectLock2D`를 자동 추가합니다. 기존 플레이어 프리팹을 수정하지 않아도 연결할 수 있습니다. 여러 플레이어가 있으면 각 함정의 `playerBody`를 직접 연결합니다. 비워두면 Player 태그의 루트 Rigidbody를 찾습니다.

### 기존 경로에 필요한 연결 변경 11개 파일

| 파일 | 보호 상태에서 차단하는 경로 |
| --- | --- |
| PlayerController2D | 일반 입력 MovePosition이 강제 위치를 덮어쓰는 것 |
| KnockbackReceiver2D | 다른 효과의 넉백 추가 |
| PlayerSafePointTracker2D | 이동 중간 위치를 안전 지점으로 저장하는 것 |
| Respawn2D | 함정 복귀/추가 낙사 피해 |
| Health | 일반 무적과 별도로 직접 피해, 직접 HP 차감, 새 출혈, 로컬 출혈 tick |
| TrapDamageEvent | DB 피해/넉백 및 외부 피격 이벤트 전달 |
| CliffTrap2D | 충돌과 무관한 좌표 기반 추락 감지 및 중복 추락 잠금 |
| PitTile2D | 구멍 진입 피해/복귀 |
| ElectricTileTrap2D | 캐시된 플레이어 목록에 대한 피해/피해 쿨타임 소비 |
| DamageSystem | 새 출혈 적용. 기존 지속 피해는 Health의 직접 피해 보호로 차단 |
| JsonBlockBuilder2D | 두 벽형 함정을 기존 바닥 함정의 Sprite 숨김/발견 교체에서 제외 |

물리가 꺼지면 연결된 Collider가 Trigger, 충돌, Overlap/Raycast 감지 대상에서 빠집니다. 따라서 화살, 벽가시 센서, 레이저/마법구 감지 및 구르는 돌 충돌도 플레이어를 대상으로 처리하지 않습니다. 이미 작동 중인 다른 함정의 애니메이션과 자동 발사 주기는 계속 진행합니다. 해당 플레이어가 새 발동을 유발하거나 효과를 받는 것이 차단됩니다.

기존에 받은 출혈/필드 효과 자체를 삭제하지 않고 보호 중 피해만 생략합니다. 강제 이동은 SpeedController 속도와 독립적이므로 기존 감속으로 느려지지 않습니다. 지속 효과의 남은 시간은 자연스럽게 흐르고 보호 종료 후 유효한 효과는 다시 작동합니다.

물리 제외 중에도 벽/일반 장애물은 별도 BoxCast로 막습니다. 함정 코드 100~109의 충돌체와 이름에 코드가 없는 RollingBallTrap2D 본체는 건너뛰며 이 초안의 벽형 함정 본체는 벽으로 취급합니다. 벽이 있으면 그 앞에서 이동을 끝내고 팔을 회수합니다. `obstacleMask` 기본값은 모든 레이어이며 지형 충돌 정책에 맞춰 좁힐 수 있습니다.

보호는 영구 무적이 아닙니다. 이동을 마친 위치가 구멍이면 **보호가 끝난 이후 정상 낙사 규칙이 다시 적용**됩니다. 목적지가 구멍일 때의 착지 금지/안전 타일 보정은 요청 범위에 없으므로 넣지 않았습니다. 함정을 이동 중 비활성화/파괴하면 현재 위치에서 즉시 잠금과 물리를 복구하고 재진입을 기다립니다.

## 검토와 적용 순서

1. `Validation/review-idle.png`, `review-moving.png`와 이 문서의 범위/목적지/타이밍을 검토합니다.
2. 실제 프리팹은 `New/Assets/Resources/Prefabs/102-014.prefab`, `102-015.prefab`입니다. `New` 아래는 원본 Unity 프로젝트에서 import되지 않으므로 검토 중 기존 게임에 영향이 없습니다.
3. Unity Hub에서 별도 `Temp/WallForceTrapReview` 프로젝트를 열면 `Assets/Scenes/WallForceTrapReview.unity`에서 두 함정과 원본 이미지 참조를 확인할 수 있습니다. Play 시 자동 발동합니다. 이 프로젝트에는 원본 세이브/DB가 없습니다.
4. 승인 후 `New/Assets`의 코드·prefab·mask를 원본의 대응 위치로 옮기고 `.meta`도 함께 복사합니다. `Integration/After` 수정본을 Before와 비교해 반영합니다. 자동 적용 스크립트는 제공하거나 실행하지 않았습니다.
5. 원본 파일이 그 사이 변경됐다면 `Integration/manifest.json`의 before SHA-256을 비교하고 변경 내용을 병합합니다. 오래된 After 전체를 무조건 덮어쓰지 않습니다.
6. 신규 씬은 선택적으로 테스트 전용으로 가져옵니다. 게임 맵 JSON에 코드를 배치하는 것은 마지막 단계입니다. 프리팹은 `Resources/Prefabs/<code>` 로드 규칙에 맞췄습니다.
7. DB 메타데이터가 필요하면 `Integration/new-trap-records.json`의 두 레코드만 기존 `traps` 배열에 병합합니다. 전체 db.json을 교체하지 않습니다. 이 함정은 자체 피해가 없어 실행 시 DB 피해 조회를 하지 않습니다.

## 검증 자료

- `Validation/Compile`: 원본 Unity 6000.2.14f1 Bee 참조/Roslyn으로 Core, GamePlay, App, System의 제안된 코드 전체를 컴파일한 결과입니다.
- `Validation/unity-checks.txt`: 실제 Unity에서 프리팹 생성/역직렬화와 반사 호출을 통한 고정 시간 단계 검증 결과입니다. 물리도 `Physics2D.Simulate`로 실행합니다.
- `Validation/unity.log`: 격리 프로젝트 실행 로그입니다.
- `Validation/original-project-sha256.json`: 검토본 제작 전 원본 Assets/Packages/ProjectSettings의 SHA-256 기준입니다.

최종 결과: 관련 어셈블리 4개 컴파일 성공, 실제 Unity 검증 86개 통과. 제작 전후 원본 파일 1,349개의 내용과 파일 목록을 비교하여 수정/추가가 없음을 확인했습니다. `wall-force-traps-review.zip`에는 새 자산, 연결 변경안, 검토 문서, 검증 결과와 재현 도구가 담겨 있습니다.

검증은 Editor 안에서 결정적인 시간 단위로 코드를 실행하는 방식입니다. 원본 프로젝트의 Play Mode, 실제 플레이어 애니메이션, 모든 맵 배치, URP 화면은 아직 검증하지 않았습니다. 승인 후 원본에서 방향 4종, 길목 낭떠러지/전격/구르는 돌, 중간 Disable, 재진입을 확인합니다.

Unity API 근거: [Rigidbody2D.simulated](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Rigidbody2D-simulated.html), [SpriteMask](https://docs.unity3d.com/6000.2/Documentation/Manual/sprite/mask/sprite-mask-reference.html).
