# 100-000 인접 구멍 판정 롤백

이 폴더는 2026-09-16 인접 구멍 경계 판정 개선을 적용하기 직전의
`CliffTrap2D.cs`를 보관합니다.

롤백할 때는 다음 파일을 원래 위치로 복사합니다.

- 원본 보관 파일: `CliffTrap2D.cs.before`
- 복원 위치: `Assets/Scripts/GamePlay/Traps/CliffTrap2D.cs`

이번 변경에서 추가된 테스트까지 완전히 되돌리려면
`Assets/Tests/Editor/TrapSystemTests.cs`에서 아래 두 테스트와 관련 helper를 제거합니다.

- `CliffTrapDetection_ExtendsOnlyTowardAdjacentPit`
- `CliffTrapFallLock_AllowsOnlyOneTrapPerPlayer`
- `InvokeInsideDetection`, `InvokeFallLock`, `InvokePrivate`

프리팹과 씬에는 변경을 적용하지 않았습니다.
