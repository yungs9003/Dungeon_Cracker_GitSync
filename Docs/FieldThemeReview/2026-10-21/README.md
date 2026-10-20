# 필드 테마 이미지 매칭 검토 — 2026-10-21

검토 당시 상태: **사용자 매칭 확인 대기. 게임에 적용하지 않음.** 이후 사용자가 현재 대응안의 구현을 승인했으며, 아래 표는 당시 검토 기록으로 보존한다.

기존 `Assets/Sprites/Tiles`의 PNG 32종과 제공 이미지 44장(ice 22장·fire 22장)을 색상보다 모서리·통로·테두리 구조를 기준으로 비교했다. 기존 21종에 대응하는 ice/fire 이미지 42장을 매칭했고, 나머지 2장은 기존에 없는 구멍 모양이다. 기존 11종에는 제공된 테마 이미지가 없다.

모든 비교표는 왼쪽부터 **기존 / ice / fire**다. 원본 100×100 이미지를 최근접 방식으로 2배 확대했다. 이미지 원본과 프로젝트의 Assets/Packages/ProjectSettings는 수정하지 않았다.

## 그림 비교표

- [벽 Element 0–6](01-walls-0-6.png)
- [벽 Element 7–13](02-walls-7-13.png)
- [바닥·출구·장애물·함정](03-floor-portals-traps.png)
- [구멍 5종 및 추가 1종](04-pits.png)
- [테마 대응 이미지가 없는 기타 8종](05-no-theme-counterparts.png)
- [IMG_4944 원본 방향 및 정렬 비교](06-rotation-detail.png)

## 대응안

아래 표의 IMG 파일은 모두 제공 폴더 `C:\Users\yoong\Desktop\졸작플젝\6_IMG_4914.png_260818` 안에 있다. 기존 파일은 `Assets/Sprites/Tiles` 기준이다.

| 기존 PNG | ice PNG | fire PNG | 비고 |
| --- | --- | --- | --- |
| 001-001-0.png | IMG_4921.png | IMG_4945.png | 벽 Element 0 |
| 001-001-1.png | IMG_4929.png | IMG_4951.png | 벽 Element 1 |
| 001-001-2.png | IMG_4920.png | IMG_4944.png | fire만 반시계 90° 정렬 필요 |
| 001-001-3.png | IMG_4914.png | IMG_4939.png | 벽 Element 3 |
| 001-001-4.png | IMG_4916.png | IMG_4941.png | 벽 Element 4 |
| 001-001-5.png | IMG_4915.png | IMG_4940.png | 벽 Element 5 |
| 001-001-6.png | IMG_4930.png | IMG_4952.png | 벽 Element 6 |
| 001-001-7.png | IMG_4917.png | IMG_4942.png | 벽 Element 7 |
| 001-001-8.png | 없음 | 없음 | 기존 그림 유지 제안 |
| 001-001-9.png | 없음 | 없음 | 기존 그림 유지 제안 |
| 001-001-10.png | IMG_4924.png | IMG_4948.png | 벽 Element 10 |
| 001-001-11.png | IMG_4927.png | IMG_4949.png | 벽 Element 11 |
| 001-001-12_0.png | 없음 | 없음 | 기존 그림 유지 제안 |
| 001-001-13_0.png | IMG_4918.png | IMG_4943.png | 벽 Element 13 |
| 000-000-0.png | IMG_4931.png | IMG_4938.png | 바닥 |
| 001-002.png | IMG_4923.png | IMG_4947.png | 001-002/003/004 프리팹이 같은 Sprite 사용 |
| 001-005.png | KakaoTalk_20261017_171447644.png | KakaoTalk_20261017_171447644_01.png | 장애물 |
| 101-001-0.png | IMG_4928.png | IMG_4950.png | 화살 함정 |
| 101-007-0.png | IMG_4922.png | IMG_4946.png | 구체 함정 |
| 100-000-0.png | IMG_4932.png | IMG_4953.png | 구멍 Element 0 |
| 100-000-1.png | IMG_4933.png | IMG_4954.png | 구멍 Element 1 |
| 100-000-2.png | IMG_4934.png | IMG_4955.png | 구멍 Element 2 |
| 100-000-3.png | IMG_4935.png | IMG_4956.png | 구멍 Element 3 |
| 100-000-4.png | IMG_4936.png | IMG_4957.png | 구멍 Element 4 |
| 기존 대응 없음 | IMG_4937.png | IMG_4958.png | 상·하 테두리, 좌·우 열림. 기존 번호 미배정 |

그 밖에 기존 `002-002-0.png`, `002-002-1.png`, `102-002-0.png`, `102-005-0.png`, `102-009-0.png`, `102-010-0.png`, `102-011-0.png`, `103-012-0.png`에는 대응 이미지가 없다.

## 확인받을 적용 범위

1. 위 대응안과 IMG_4944의 반시계 90° 방향 정렬을 사용한다.
2. 대응 이미지가 없는 11종은 기존 그림을 유지한다.
3. 추가 구멍 한 쌍은 임의로 기존 번호에 배정하지 않고 보류한다.
4. 확인 후에 맵 JSON의 `field_effect.type`이 `ice`이면 파란색, `fire`이면 회색/붉은색을 선택하도록 구현한다. 현재 FieldEffectManager는 `burn`만 인식하므로, 구현 시 `fire`를 인식하고 기존 `burn` 호환도 유지한다. 사용자가 입력한 현재 map_002.json의 `fire`는 그대로 둔다.
5. 최초 맵 생성뿐 아니라 함정 노출/해체·주변 벽 재연결 후에도 승인된 테마가 유지되도록 적용한다. assetNo와 direction, 피해/주기 등 게임 정책은 유지한다. 이전에 조사한 0501 해체 후 기둥 개형 문제는 이번 적용 범위에 포함하지 않는다.

## 검증 및 보관

- `proposed-mapping.json`: 대응안과 보류 항목. 상태는 `awaiting_user_mapping_review`.
- `mapping.csv`: 기존 32종과 추가 1종을 모은 검토용 목록.
- `source-sha256.json`: 기존 32장·제공 44장 원본 해시.
- `before-project-sha256.json`: 조사 시작 시 게임 파일 기준 해시.
- `review-manifest.json`: 원본 보존·전체 게임 파일 비교·제공 이미지 중복/누락 검사 결과.
- `structure-score-candidates.json`, `rotated-structure-candidates.json`: 구조 비교 보조값. 의미적 정답의 확률이나 확정 점수가 아니다.
- `Tools/create_comparison_sheets.py`, `Tools/verify_review.py`: 그림 생성 및 읽기 전용 검증 도구. Docs 검토 산출물만 쓴다.

비교표 6장을 열어 파일명·형태·방향·미매칭 표시를 확인했다. 게임 코드/에셋 적용이나 Unity 실행 검증은 수행하지 않았다. 프로젝트 개발 기록에 읽기 전용 조사로 기록하고 지정 바탕화면 사본과 동기화한다.

## 사용자 승인 이후

현재 매칭대로 ice/fire 자동 선택 구현과 격리 Unity 검증을 완료했다. [구현·검증 기록](../../Rollback/2026-10-21-field-themes/README.md)과 [추가 이미지 연결 안내](추가이미지연결.md)를 참고한다. 위의 미적용/확인 대기 설명과 proposed-mapping.json은 구현 전 검토 시점의 기록이다.
