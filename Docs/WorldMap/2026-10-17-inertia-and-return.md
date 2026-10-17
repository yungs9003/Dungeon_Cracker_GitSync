# WorldMap 팝업 복귀·이미지 경계·관성

2026-10-17 후속 수정. `Assets/Scenes/WorldMap.unity`에 적용했다.

## 현재 동작

- 원래 크기 버튼을 제거했다. 지점 선택 후 기존 던전 진입 팝업을 닫으면 배율 1과 확대 직전 탐색 위치로 부드럽게 돌아간다. 던전 입장 버튼의 기존 씬 전환은 유지한다.
- 이동 가능한 범위는 `(지도 표시 크기 - Viewport 크기) / 2`다. 지도의 가장자리가 화면 안으로 넘어와 바깥 배경을 드러내지 않도록 제한한다. 지도보다 Viewport가 큰 축은 지도를 중앙에 고정하고 해당 축 드래그를 제한한다. 실제 이미지 바깥에서는 새 드래그가 시작되지 않는다.
- 지도는 기존 원본 크기 3172×1984, 배율 1로 시작한다. 하단 메뉴는 지도 Viewport에서 제외된다.
- 손을 놓으면 이동 방향을 따라 조금 더 움직이며 감속한다. 경계에서는 해당 축이 즉시 멈춘다. 새 누름·지점 확대·비활성화는 남은 관성을 지운다. 이동을 멈춘 채 놓으면 이전 드래그 속도가 남지 않는다.
- 지점 선택의 기본 배율 1.6과 확대 시간 0.5초는 유지한다. 가장자리 지점은 바깥 배경이 드러나지 않는 범위까지 중앙 쪽으로 이동한다. 정확한 중앙 정렬보다 이미지 경계를 우선한다.
- 모든 이동과 복귀는 unscaled 시간을 사용하므로 timeScale이 0이어도 완료된다.

## Inspector 조절

`WorldMapViewport > WorldMapPanZoomUI`:

| 항목 | 기본값 | 의미 |
| --- | ---: | --- |
| Inertia Damping | 8 | 클수록 손을 놓은 뒤 빨리 멈춘다. |
| Max Inertia Speed | 1500 | 관성 초기 속도의 상한. 기본 Canvas 기준 픽셀/초다. |
| Focus Duration | 0.5 | 지점 확대와 팝업 닫힘 복귀 시간(초). |
| Max Zoom | 3 | 지점 확대 배율의 상한. |

각 지점의 `WorldMapFocusTargetUI > Focus Zoom`은 기존처럼 해당 지점의 목표 배율이다. `On Focused`는 확대 완료 뒤 기존 팝업을 연다.

`DungeonPopupPanelController > On Closed`에 `WorldMapPanZoomUI.ReturnFromFocus`를 연결했다. 이미 숨겨진 팝업을 다시 Close해도 이벤트를 반복 호출하지 않는다. 다른 씬의 팝업은 이벤트가 비어 있어 기존 동작을 유지한다.

`Tools > Dungeon > Configure WorldMap Pan and Zoom`을 재실행하면 기존 ResetMapView를 제거하고 팝업 닫힘 이벤트를 연결한다. 같은 리스너는 중복 연결하지 않는다. 코드의 공개 `ResetView()`는 유지하지만 화면에 호출 버튼은 없다.

## 수정 범위와 검증

기존 코드 4개(`WorldMapPanZoomUI`, `WorldMapFocusTargetUI`, `DungeonPopupPanelController`, `WorldMapViewBuilder`)와 WorldMap 씬만 수정했다. 신규 게임 에셋은 없다. 원본 PNG·Importer·다른 씬·공통 프리팹·DB·저장 설정은 기존 해시와 동일하다.

분리 Unity에서 씬을 구성하고 원본 소스 6개 어셈블리 컴파일과 Play Mode 검사 23개를 통과했다. 원본 크기, 픽셀 단위 드래그, 관성 진행/감속/중단, 이미지 경계, 실제 팝업 컨트롤러 Close와 복귀, timeScale=0, 확대 중 드래그 취소, 작은 이미지의 입력 제한을 확인했다. 1280×720/1920×1080/2560×1080 렌더와 경계/복귀 PNG도 확인했다.

검증은 원본 씬에서 지도 UI만 분리해 수행했으며 저장 서비스를 생성하지 않았다. 실제 사용자 저장 파일을 읽거나 쓰는 전체 게임 실행 검증과는 구분한다.

`Docs/Rollback/2026-10-17-worldmap-inertia-boundary`에 Before/After 사본, 전체 프로젝트 기준 해시, 변경 범위 manifest, 씬 보존 비교, 컴파일/Unity 로그, 검사 목록과 PNG를 보관했다. Unity가 씬 저장 시 자동으로 덧붙인 기존 저장 컴포넌트의 JSON 캐시는 제외하여 해당 블록은 원본 그대로 유지했다. 임시 제작·검증 도구는 `Temp/WorldMapPolish`에 있다.
