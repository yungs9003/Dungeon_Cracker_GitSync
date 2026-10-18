# 튜토리얼 창 사용 안내

각 화면의 좌측 상단 물음표로 튜토리얼을 엽니다. 물음표는 기본 흰색이며 마우스를 올리거나 누르면 빨간색으로 바뀝니다. 좌우 화살표로 이동하고 슬라이드 아래 중앙의 닫기로 닫습니다. 첫 장의 이전 버튼과 마지막 장의 다음 버튼은 비활성화되며, 다시 열면 첫 장부터 시작합니다.

전체 화면 검은 배경은 **알파200/255(약78.4% 불투명도)**입니다. 슬라이드는 상태창과 같은 **1584×888 기준 크기와 위치**를 사용해 상태창을 완전히 덮습니다. PNG의 원본 종횡비를 유지하며 투명 부분이나 남는 여백은 불투명 흰색 SlideBackground로 가립니다. 화면이 좁거나 낮으면 상태창과 같은 비율로 축소합니다. 화살표는 슬라이드 양옆의 세로 중앙, 닫기는 슬라이드 아래 중앙에 배치합니다. 하단 여유가 매우 적으면 닫기 버튼 크기를 줄이고 화면 하단 안쪽으로 위치를 제한합니다.

튜토리얼이 열린 동안 뒤쪽 UI 클릭과 월드맵 이동/관성/확대 진행, 월드맵 Esc 및 스테이터스 Tab 입력을 막습니다. 닫으면 진행 중이던 월드맵 확대와 던전 정보창 닫기 후 원래 배율로 복귀하는 흐름을 이어갑니다. 게임의 일시정지 배율은 변경하지 않으므로 이미 일시정지된 상태에서도 페이지 조작이 가능합니다.

## PNG 교체·추가

1. 새 PNG를 `Assets/Sprites/UI/Tutorial/Slides`에 넣습니다. Unity Inspector에서 **Texture Type = Sprite (2D and UI)**, **Sprite Mode = Single**로 설정하고 **Apply**를 누릅니다. 큰 이미지가 축소되지 않도록 Max Size는 원본의 긴 변 이상으로 지정합니다.
2. Project 창의 `Assets/Resources/UI/Tutorial`에서 변경하려는 목록 에셋을 선택합니다.

   | 목록 에셋 | 연결 화면 | 초기 장수 |
   | --- | --- | ---: |
   | WorldMapTutorial | 월드맵 화면 | 5 |
   | DungeonInfoTutorial | 월드맵 던전 정보창 | 4 |
   | StatusTutorial | 공통 스테이터스 창 및 CheckpointScene 스테이터스 창 | 5 |
   | ModuleTutorial | 모듈 화면 | 3 |

3. Inspector의 **Slides** 배열에 PNG Sprite를 순서대로 드래그합니다. 배열의 Size/목록을 조절하면 장수를 바꿀 수 있습니다. 같은 이미지를 여러 칸에 넣을 수도 있습니다. 빈 칸은 표시하지 않습니다.
4. 실행 후 해당 물음표를 눌러 확인합니다. 실행 중 에셋을 변경했다면 창을 닫고 다시 열면 목록을 다시 읽습니다. PNG만 교체할 때 씬이나 프리팹을 재구성할 필요는 없습니다.

현재는 요청한 `info_worldmap_1.png`를 네 목록의 모든 칸에 등록했습니다. 이미지의 글자나 그림을 코드로 나누지 않고 PNG 전체를 그대로 표시합니다.

## 구성과 유지보수

- `Assets/Prefabs/UI/Tutorial/TutorialOverlay.prefab`: 모든 화면에서 사용하는 전체 화면 슬라이드 창입니다. Modal의 Image Color 알파로 배경 농도를 바꿀 수 있습니다.
- `Assets/Prefabs/UI/Tutorial/TutorialHelpButton.prefab`: 각 화면에 배치된 물음표 버튼입니다. 인스턴스의 Tutorial 필드가 해당 목록을 참조합니다.
- `Assets/Scripts/App/UI/Tutorial`: 슬라이드 목록, 공통 창, 물음표 동작 코드입니다.
- `Assets/Scripts/App/UI/Tutorial/TutorialWindowLayout.cs`: 슬라이드 크기·위치와 화살표/닫기 버튼의 반응형 배치입니다. Canvas 기준 해상도는 상태창과 같은1920×1080입니다.
- `Assets/Editor/TutorialSceneBuilder.cs`: **Tools > Dungeon > Configure Tutorial Windows** 재구성 메뉴입니다. WorldMap·Module·CheckpointScene과 SharedStatus에 도움말 버튼을 구성합니다. 이미 존재하는 네 목록의 이미지/장수는 유지합니다. 기존 씬/프리팹을 수정하므로 일반 이미지 교체에는 실행하지 않아도 됩니다.

SharedStatus를 사용하는 다른 씬에도 스테이터스 도움말이 함께 제공됩니다. CheckpointScene의 독립 스테이터스 창도 같은 StatusTutorial 목록을 사용합니다. 기존 UI 제작 도구로 화면을 새로 만든 뒤 물음표가 사라진 경우 위 재구성 메뉴로 복구할 수 있습니다.

## 스테이터스 창의 화면 배치

`Assets/Scripts/App/UI/StatusWindowLayout.cs`가 StatusPanel을 배치합니다. WorldMap·Guild·Shop·Module에서는 BottomNavigationCanvas의 실제 BottomPanel 위쪽 경계를 기준으로 남은 영역 중앙에 놓습니다. DUNGEON_001·CheckpointScene처럼 하단 바가 없는 씬은 전체 화면 중앙을 사용합니다. 기본 크기는1584×888이며 화면 크기와 비율에 맞춰 여백을 포함해 축소합니다. 에디터와 실행 중 해상도 변경 모두 같은 계산을 사용합니다. 페이지 내용과 팝업은 StatusPanel과 함께 이동합니다.

상태창과 튜토리얼은 `StatusWindowLayout.GetWindowRect` 계산을 공유하므로 위치·크기가 일치합니다. 실행 중 Slide의 RectTransform을 직접 변경하면 자동 배치가 다음 프레임에 다시 적용됩니다. 임시 수동 조정 시 Modal의 TutorialWindowLayout을 비활성화할 수 있으며, 비활성 상태에서는 치수 변경 콜백도 배치를 적용하지 않습니다. 수동으로 이동하면 상태창과 정확히 겹친다는 보장은 없어집니다.

## 검증 자료

`Docs/Rollback/2026-10-18-tutorial-ui`에 변경 전후 사본, 적용 파일 목록, 기존 씬/프리팹 보존 비교, 소스 컴파일 로그, 분리 Unity 실행 로그와 화면 렌더를 보관합니다. 분리 프로젝트의 UI만 실행해 실제 사용자 저장 서비스는 생성하지 않았습니다.

2026-10-18 배치 확대·하단 바 제외·알파200 수정 자료는 `Docs/Rollback/2026-10-18-status-tutorial-layout`에 있습니다.6개 씬과 여러 해상도의 배치 검사16개, 기존 튜토리얼 조작 검사31개를 통과했습니다.

상태창 완전 가림·닫기 하단 중앙 수정 자료는 `Docs/Rollback/2026-10-18-tutorial-cover-layout`에 있습니다. 배치 검사26개와 조작 검사32개를 통과했습니다. 일반 슬라이드와 완전히 투명한 세로 이미지 모두에서, 뒤 상태창을 표시/숨김했을 때 슬라이드 영역의 렌더 픽셀이 동일한 것을 추가로 확인했습니다.
