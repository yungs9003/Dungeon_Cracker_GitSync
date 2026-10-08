# 벽 이동 함정 적용 전 백업

사용자가 2026-10-08 검토본 적용을 승인한 뒤 보관한 원본이다.

- Before: 원본 코드 11개 및 Assets/Resources/Data/db.json 적용 전 사본.
- before-project-sha256.json: 적용 직전 Assets/Packages/ProjectSettings 전체 파일 목록 및 SHA-256.
- applied-manifest.json: 추가 13개 파일과 수정 12개 파일의 적용 직후 해시.
- Validation: 실제 적용된 Assets 소스의 컴파일과 별도 Unity import/실행 검사 결과.

이번 적용은 새로운 프리팹/마스크/코드 및 기존 보호 연결과 DB 레코드 두 개에 한정한다. 기존 게임 씬, 맵 JSON, 플레이어 프리팹 및 저장 데이터는 변경하지 않는다.

롤백이 필요하면 Before 사본과 현재 파일의 후속 변경 여부를 비교한 후 기존 코드/DB를 복원하고, 신규 자산은 참조 여부를 확인해 제거한다. 이 문서는 복구 안내이며 자동 복원/삭제 명령을 실행하지 않는다.
