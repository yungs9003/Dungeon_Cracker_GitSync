# Status 저장/UI 생명주기 개선 롤백

이 폴더는 2026-09-16 저장 완료 이벤트와 Status 모듈 UI 생명주기 개선을
적용하기 직전의 파일을 보관합니다.

문제가 있을 때 다음 파일을 원래 위치로 복원하면 이번 변경을 되돌릴 수 있습니다.

- `StatusSaveFile.cs.before`
  - 복원 위치: `Assets/Scripts/Core/StatusSaveFile.cs`
- `StatusModulePageUI.cs.before`
  - 복원 위치: `Assets/Scripts/App/UI/StatusModulePageUI.cs`
- `ModuleSystemTests.cs.before`
  - 복원 위치: `Assets/Tests/Editor/ModuleSystemTests.cs`
- `StatusSaveStorageTests.cs.before`
  - 복원 위치: `Assets/Tests/Editor/StatusSaveStorageTests.cs`

이번 변경에서는 프리팹, 씬, DB 및 `savefile.json`/`savefile.test.json`을 수정하지 않았습니다.
