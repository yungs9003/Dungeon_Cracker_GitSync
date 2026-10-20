# field_effect 기반 ice/fire Sprite 자동 선택

2026-10-21 KST. 사용자가 승인한 기존21종의 ice/fire 매칭을 구현하고 격리 Unity에서 검증했다.

## 동작

- 맵 JSON의 `field_effect.type`이 `ice`이면 파란색, `fire` 또는 기존 `burn`이면 회색/붉은색을 선택한다. 대소문자와 양끝 공백을 정리한다.
- 효과가 없거나 `none`/알 수 없는 값이면 원본 그림이다. 피해량이 0이어도 테마는 적용된다.
- `Assets/Resources/Data/MapSpriteThemes.asset`에 원본32종과 테마 대응표를 저장한다. 현재21종의 Ice/Fire를 연결하고, 나머지11종은 비워 원본을 유지한다.
- 최초 grid/3F 생성과 자식 장식, 함정 발견/재숨김, 해체 후 바닥/주변 벽/숨김 벽/WallVisual, 기존 해체 대체 바닥, 스위치 대체 바닥에 테마를 적용한다. 테마는 빌더/타일별로 보관한다.
- assetNo/direction, 공격 방향, 발견 상태, 콜라이더, 피해량/주기/감속 설정은 유지한다. `fire`는 기존 Burn 처리로 연결한다. 기존0501 해체 개형 solver는 수정하지 않았다.

## 파일 범위

기존 코드6개 수정:

1. Assets/Scripts/GamePlay/Map/AssetVariantSprite2D.cs
2. Assets/Scripts/GamePlay/Map/FieldEffectManager.cs
3. Assets/Scripts/GamePlay/Map/JsonBlockBuilder2D.cs
4. Assets/Scripts/GamePlay/Map/JsonBlockBuilder2D.Retile.cs
5. Assets/Scripts/GamePlay/Traps/Destructible.cs
6. Assets/Scripts/System/ReplaceSelfWithPrefabOnDisable.cs

게임 파일91개 추가: MapSpriteThemeCatalog.cs와 meta, MapSpriteThemes.asset와 meta, 테마 PNG42장과 meta84개, 폴더meta3개. 기존1579개 중6개 수정·1573개 동일·삭제0개이며 최종1670개다.

제공 이미지41장은 바이트 그대로 복사하고, IMG_4944 한 장의 프로젝트용 복사본만 반시계90도 정렬했다. 모든42장의 픽셀을 원본/승인된 회전과 대조했다. 기존 Tiles32장과 제공44장 원본은 모두 보존했다. 씬·기존 프리팹·맵 JSON·DB·사용자 저장은 변경하지 않았다.

## 검증

- Unity6000.2.14f1의 격리 프로젝트에서 컴파일과 실제 실행 검사 **1186개 통과 / 실패0개**, 프로세스 종료 코드0.
- 원본32종의 카탈로그 참조·매칭/빈 칸 fallback·Sprite 크기/PPU/Pivot·원본 복원, 미래 이미지 빈 참조 연결 후 즉시 선택을 확인했다. 미래 연결 검사는 카탈로그 사본에서만 수행했다.
- ice→fire→burn→none→효과생략→알 수 없는 값→ice 맵 전환과 grid/3F·출구3종·장애물 자식/하이라이트·함정 발견/재숨김을 확인했다.
- ice/fire 각각 실제 Destructible 해체·새 바닥·주변 벽·인접 숨김 벽·force WallVisual·공격 metadata 보존·카운트1회·3F Registry 보존을 확인했다. 기존 방식의 해체 바닥과 선택형 스위치 교체 컴포넌트도 실제 호출했다. 스위치 검사는 현재 낭떠러지 프리팹에 해당 컴포넌트가 없어 격리 인스턴스에 직접 추가했다.
- map_002의 ice/fire 격리 JSON으로0501 해체 전/후 렌더4장을 만들고 열어 확인했다. 사용자 맵 JSON을 수정하거나 실제 사용자 게임을 실행한 것은 아니다. 원래 맵에 배치된 얼음/용암/기타 함정 전용 이미지는 승인된 대응표에 없으면 원본을 유지한다.
- 게임 코드/이미지/카탈로그/meta97개가 격리 검증본과 바이트 동일하다. Assets/StreamingAssets 전체를 격리 복사에서 제외하고 격리 내부에서 StatusSaveDefaults로 새 검증 저장을 생성했다. 실제 저장 복사/실행0회.
- 최초 검증 도구는 Destroy 완료 전 옛 인스턴스를 함께 검사하고 비전 마스크를 타일로 세었으며, 낭떠러지 프리팹에 선택형 교체 컴포넌트가 있다고 가정해 실패했다. 게임 코드를 바꾸지 않고 도구의 프레임 대기·타일 집계·컴포넌트 fixture를 수정했다. 최초 결과는 Validation/Attempts/01에 보관했다. 최종 결과는 Validation/runtime-checks.txt와 runtime.log다. 기존 전체 단위 테스트를 재실행했다고 주장하지 않는다.

## 후속 이미지 추가

[추가 이미지 연결 안내](../../FieldThemeReview/2026-10-21/추가이미지연결.md)의 카탈로그 빈 칸에 Sprite를 연결하면 코드 수정 없이 적용된다. 현재 누락11종과 기존에 없는 추가 구멍 IMG_4937/4958은 후속 에셋/번호 결정 대기다.

Before에 기존6파일, After에 최종97파일, Diff에 코드 변경, change-manifest.json에 전/후 해시와 범위를 보관했다. 백업은 이번 테마 변경 시작 상태이며 이전 방향 정합/타일 재연결 기능을 포함한다. 개발 기록은 프로젝트와 지정 바탕화면 사본에 동기화한다.
