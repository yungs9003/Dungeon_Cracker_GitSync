# 용암 침강·분출 연출 조정

이 문서는 22:19 제작 당시 기록이다. 이후 사용자 시연에 따른 하강·가림 동기화와 분출 빈 구간 수정은 [최신 보완 안내](2026-10-15-lava-flow-sync.md)를 참조한다.

## 102-016 용암 침강

Visual의 현재 SpriteRenderer bounds를 기준으로 윗부분만 보이게 한다. 잠김 진행률이 0이면 전체, 0.5면 위 절반, 1이면 아무 부분도 표시하지 않는다. 마스크는 캐릭터의 크기와 위치를 따라가며 별도 SortingGroup 안에서 해당 Visual에만 적용한다.

`CliffTrap2D > Lava Visual Drop`은 2에서 **0.15**로 변경했다. 아래로 조금 이동하는 느낌만 남기고 실제 잠김은 발부터 차오르는 투명 마스킹으로 표현한다. 이 값을 0으로 설정하면 위치 변화 없이 마스킹만 적용할 수 있다.

`Lava Sink Duration=2`와 피해 분배·안전 지점 복귀는 유지한다. 완료하거나 침강 중 비활성화되면 원래 parent, 위치, maskInteraction, 조작·물리 상태를 복원한다. 일반 낭떠러지의 축소 연출은 유지한다.

## 102-017 예고

좌우에서 줄어드는 ShrinkingMask를 제거했다. `WarningBlendFloor/IdleFloor`의 alpha를 전체 타일에서 균일하게 줄이고 아래의 밝은 용암 바닥이 드러나도록 전환한다. `Warning Duration`은 기존 1초를 유지한다.

## 102-017 상승 분출

`LavaColumn`은 2칸 정지 Sprite 대신 다음 구조를 사용한다.

```text
LavaColumn (SortingGroup)
├─ TwoCellWindow (SpriteMask, 가로 1칸 × 높이 2칸)
├─ RisingSegment0
├─ RisingSegment1
└─ RisingSegment2
```

세 이미지 모두 기존 LavaColumn Sprite를 그대로 쓰며, 각각 높이 1칸으로 표시한다. 분출 시작 시 한 칸이 보이고 올라가는 동안 아래에서 다음 이미지가 이어진다. `Cell Rise Time=0.25`이므로 0.125초 후 첫 이미지는 0.5칸 상승하고, 0.25초 후 두 번째 높이에 도달하며 뒤의 이미지가 첫 번째 높이를 채운다.

상단을 벗어난 이미지는 바닥 쪽으로 재순환한다. SpriteRenderer 3개를 재사용하므로 매 프레임 오브젝트를 생성하거나 제거하지 않는다. 전체 표시는 2칸 창으로 제한하며, 원본 Sprite 픽셀은 변경하지 않았다.

`Rest Duration=2`, `Warning Duration=1`, `Eruption Duration=1`, `Damage Interval=0.5`와 해당 바닥 한 칸의 피해 판정은 유지한다. `Cell Rise Time`이 작을수록 상승 흐름이 빨라진다.

## 검증 자료

수정 전후 코드·프리팹 사본과 검증 자료는 `Docs/Rollback/2026-10-15-lava-visuals`에 보관한다. `Validation`의 sink-half.png, warning-half.png, eruption-0.png, eruption-125ms.png, eruption-250ms.png, eruption-375ms.png에서 실제 렌더를 비교할 수 있다.

원본 Assets 소스 6개 어셈블리 컴파일과 분리 Unity Play Mode의 시각/복구/피해 검사 24개를 통과했다. 실제 플레이어 Sprite의 진행률 50% 렌더는 가려진 아래 절반에서 플레이어 픽셀 0개, 보이는 위 절반에서 8,988개가 검출됐다. 크기 1.5배/옆 위치 진입도 검사했으며 일반 낭떠러지 모드·용암 총 피해 16·분출 피해 20을 유지했다. DB, 맵 JSON, 다른 함정, 원본 Sprite 및 프리팹 meta는 변경하지 않았다.

같은 분리 프로젝트에서 map_002의 70칸 생성과 기존 DB/피해/넉백/보호 연결 회귀 검사 45개도 통과했다. 원본 게임 씬을 직접 Play Mode로 실행한 결과와는 구분한다.
