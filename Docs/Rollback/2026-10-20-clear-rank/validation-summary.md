# 정수 초·클리어 랭크 검증

2026-10-20 KST. 사용자 요청에 따라 구현했으며 사용자 활성 savefile.test.json은 복사/실행/직접 수정하지 않았다.

## 최종 결과

- 원본 소스6개 어셈블리(Core/GamePlay/App/System/EditorTests/Editor) 컴파일 통과. Validation/Compile에 응답 파일·출력·로그 보관.
- NUnit 관련69개 전부 통과(unit-results.xml/unit.log): 신규 랭크/정수 시간27개 + 기존 시간19개·기록16개·저장소7개. 기존 시간 테스트의 소수 기대값은 승인된 정수 초 정책으로 수정했고 버전 검사는 CurrentSaveVersion 상수를 사용한다.
- 실제 ClearScene의 별도 생성 데이터/저장소 실행31개 전부 통과(runtime-checks.txt/runtime.log). 버전4의127.54초를 정수127 및02:07 표시로 이행하고, 현재/체크포인트/최단 기록의 실제 JSON 정수값을 확인했다. 네 랭크 이미지의 실물 Sprite 연결/전환, 현재 결과로 평가하면서 기존 최고 기록/횟수를 재갱신하지 않는 점, 실제 미완료/DB 기준 미설정 던전의 임의 랭크 방지, 귀환 버튼 비가림을 확인했다.
- 실제 ClearScene 기반1920×1080 렌더 clear-S/A/B/C.png를 보관하고 S/C를 열어 시각 확인했다. 촬영 시에만 격리 캐시의 Canvas를 화면 카메라 모드/렌더 텍스처로 전환했으며 원본 씬의 카메라·캔버스 설정은 변경하지 않았다.
- 실제 실행 첫 시도에서는 시간 표시/이행/정수 저장8개를 통과한 뒤 랭크 Sprite 참조 검사에 실패했다(runtime-first-pass.*). 네 텍스처는 단일 Sprite 모드1인데 메타데이터에 남은 과거 다중 Sprite 내부 ID를 사용했던 것이 원인이었다. 실제 단일 Sprite ID21300000으로 원본 ClearScene 참조를 고치고 최종31개를 통과했다. 텍스처/메타데이터는 수정하지 않았다.
- 전체 Assets/Packages/ProjectSettings SHA-256: 기존13개 수정, 신규4개, 삭제0개,1550개 동일. 최종 적용17개 파일은 격리 검증 사본과 해시가 같다. 검증용 하네스 오브젝트는 격리 씬에만 추가해 실행했으며 검증 후 해당 캐시 씬을 원본 적용 사본으로 복원해 비교했다.

## 적용 범위

기존 수정13개: DB, ClearScene, ClearResultUI, Core의 DungeonProgressMigration/DungeonProgressService/GameDatabase/PlayTimeSession/StatusSaveData/StatusSaveDefaults/StatusSaveFile, 기본 savefile.json, 기존 DungeonProgressTests/PlayTimeTests. 추가4개: DungeonClearRankCalculator.cs·DungeonClearRankTests.cs와 각각의 meta. 다른 모든 원본 씬·프리팹·이미지/스프라이트 메타데이터·맵·패키지/설정·사용자 활성 세이브는 기준과 같다.

새 게임 템플릿은 버전5이고 기존 세이브는 다음 정상 Load에서 소수 던전 시간을 버린 후 정수로 이행한다. 총 플레이타임 잔여 초·매분 저장 정책과 기존 던전 클리어 보수/기록 원자성은 유지한다. 이번 랭크는 현재 결과 화면에서 계산하며 별도 최고 랭크/전체 모험가 랭크/길드 조건·보수 저장을 추가하지 않았다. 미래 던전에는 DB의 양수 기준2개를 설정해야 랭크가 표시된다.

## 보관/재검증

Before는13개 수정 전 사본, After는17개 최종 파일, Diff와 before/after-project-sha256.json 및 change-manifest.json은 전체 변경 범위를 나타낸다. Validation/Tools에는 격리 실행·렌더 도구와 단위 테스트 필터를 보관했다. 실제 작업용 캐시는 Library/CodexPlayTime/Unity이고 새 도구 작업 폴더는 Library/CodexClearRank다. 활성 사용자 파일을 이 캐시에 복사하지 않는다.

게임 종료 후 복구를 검토할 때 Before의13개를 대응 위치에 복원하고 신규4개만 제거하는 범위로 제한한다. 버전5로 정상 저장한 활성 데이터는 이전 코드와 함께 무조건 재사용하지 말고 별도 보관한 기존 버전 백업과 검토해야 한다. 개발 기록은 보존한다.

관련 검사만 실행했으며 전체 회귀가 모두 통과했다고 주장하지 않는다. 이전 작업에서 수정 전부터 실패한 SaveDataNotification_ContinuesAfterOneListenerFails(기대 로그)와 CliffTrapDetection_ExtendsOnlyTowardAdjacentPit(편집 모드 판정), 사용자 테스트 세이브에 의존하는 ModuleSystemTests는 이번 대상에서 제외했다. 이들 기존 구현/기대 로그 검사는 이번 변경 범위에서 수정하지 않았다. 정수 시간·점수 경계·현재 런과 최고 기록 분리·저장 이행 및 UI 연결에 대한 검사 범위는 위 결과와 구분한다.
