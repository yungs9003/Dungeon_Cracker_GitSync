# 월드맵 던전 정보 팝업

2026-10-18, `Group 142.svg` 기준 UI. 참고 원본은 Reference/DungeonInfo.svg에 보관한다.

- 프리팹: Assets/Prefabs/UI/WorldMap/DungeonInfoPopup.prefab
- 적용 씬: Assets/Scenes/WorldMap.unity
- 화면: SVG에서 추출한 초록 프레임·정보 이중 테두리·입장 그림·MOPS 하단, 동적 번호/이름/클리어 수/진행도, 우측 상단 도움말.
- 배치: 포인트를 기준으로 오른쪽 위30도 방향에 표시한다. 이전45도 배치의 가로 위치는 유지하고 세로 상승량만 tan(30도)만큼으로 낮췄다. 포인트와 겹치지 않게 간격을 두며 기존776×484 기준에서0.8배(620.8×387.2)와 반응형 축소를 유지한다. DungeonInfoPopupLayout이 편집/실행 중 배치를 계산한다. 선택 포인트가 없는 단독 미리보기에서는 화면 중앙을 사용한다.
- 확대: 포인트가 하단 바를 제외한 지도 화면에서 왼쪽1/3·위에서3/4(아래에서1/4)에 오도록 이동한다. 확대 중/확대 후 화면 크기가 바뀌어도 같은 비율로 다시 맞춘다. 기존 지도 이미지 경계 제한과 드래그 관성은 유지한다.
- 조작: 포인트 클릭→맵 확대→팝업. 팝업 외부 클릭/ESC→닫기와 확대 전 지도 위치/기본 배율 복귀. 바깥의 선택 포인트나 하단 내비게이션을 눌러도 첫 클릭은 닫기로 소비하며 뒤 버튼을 동시에 실행하지 않는다. 팝업은 정렬100의 전체 화면 투명 입력 Canvas를 사용하고 도움말 Canvas는 그 위에 표시한다. 카드 내부 빈 영역은 닫기 대상이 아니다. 도움말이 열리면 뒤 팝업의 입장/닫기가 차단된다.
- 도움말: 기존 Assets/Resources/UI/Tutorial/DungeonInfoTutorial.asset의 4장을 사용한다. 다른 튜토리얼과 같은 슬라이드/화살표/닫기/배경 구조다.
- 번호/이름은 현재 기존 Data/dungeons.json의 id/name을 사용한다. sceneName은 빌드에 등록된 실제 씬 이름으로 대소문자를 확인해 연결한다. 미등록 씬은 입장을 비활성화한다.

## 아직 연결하지 않은 부분

팝업을 열 때 클리어 횟수 0회와 진행도 0/?층을 표시한다. **실제 저장 기록을 읽거나 수정하지 않는 초기 표시값**이다. 현재 DB·저장 구조·체크포인트/클리어 처리에는 변경이 없다. 진행도 테스트 렌더의 6/6·4/6은 표시 함수에 임시 값을 넣은 화면이며 저장된 기록이 아니다.

후속 데이터 공급자는 DungeonInfoPopupUI.ApplyRecordDisplay(clearCount, completedMaps, totalMaps)를 호출하면 된다. 내부에서 음수/범위를 보정하고 최초 클리어 전에는 분모를 ?로 표시한다. Present를 호출할 때 초기값으로 돌아가므로 실제 데이터 연결은 Present 이후에 한다.

저장·DB 변경의 권장 구조와 검토 사항은 DevelopmentProposal.md에 정리했다. 승인 후 별도 구현한다.

## 재구성/검증 자료

Tools/Dungeon/Configure WorldMap Dungeon Popup 메뉴는 이 프리팹을 다시 만들고 WorldMap의 기존 DungeonPopupPanels 자식 패널을 교체한다. 직접 프리팹을 꾸민 뒤 이 메뉴를 실행하면 기본 디자인으로 재생성되므로 커스텀 변경은 빌더에도 반영한다. TutorialSceneBuilder는 새 우측 도움말 위치를 보존한다.

Docs/Rollback/2026-10-18-worldmap-dungeon-popup에 Before/After·기준 해시·변경 목록·컴파일/격리 Unity 검증·화면 렌더를 보관한다. 검증에서는 사용자 저장 서비스 없이 월드맵 조작과 빈 던전 스텁 입장만 수행했다.
