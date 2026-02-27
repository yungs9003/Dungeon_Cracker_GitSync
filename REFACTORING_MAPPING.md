# 프로젝트 구조화 - 파일 이동 매핑표 (최신 버전)

생성일: 2026년 1월 29일  
업데이트: 최신 변경사항 반영

---

## 📋 목표 폴더 구조

```
Assets/
├── Scripts/
│   ├── Player/              # 플레이어 관련 (입력, 이동, 애니메이션, 체력)
│   ├── UI/                  # UI 및 화면 관리 (HUD, 페이드, 씬 전환)
│   ├── Traps/               # 함정 및 적 오브젝트
│   ├── Map/                 # 맵 빌더 및 레벨 관련
│   ├── System/              # 공용 시스템 및 유틸리티
│   ├── Core/                # 핵심 데이터 및 인터페이스
│   └── Manager/             # 게임 매니저 및 흐름 관리
│
├── Prefabs/                 # 프리팹 (변동 없음)
├── Resources/               # 리소스 (변동 없음)
└── [기타]
```

---

## 📁 파일 이동 매핑 (현재 위치 → 목표 위치)

### **1️⃣ Player 폴더**
| 현재 위치 | 목표 위치 | 역할 |
|----------|----------|------|
| `Assets/PlayerController.cs` | `Assets/Scripts/Player/PlayerController.cs` | 플레이어 입력 & 이동 |
| `Assets/PlayerAnimationDriver.cs` | `Assets/Scripts/Player/PlayerAnimationDriver.cs` | 애니메이션 제어 |
| `Assets/PlayerMoveDirectionTracker.cs` | `Assets/Scripts/Player/PlayerMoveDirectionTracker.cs` | 이동 방향 추적 |
| `Assets/PlayerSafePointTracker.cs` | `Assets/Scripts/Player/PlayerSafePointTracker.cs` | 안전 포인트 기록 |
| `Assets/Health.cs` | `Assets/Scripts/Player/Health.cs` | 체력 관리 |
| `Assets/Respawn.cs` | `Assets/Scripts/Player/Respawn.cs` | 리스폰 관리 |
| `Assets/SpeedController.cs` | `Assets/Scripts/Player/SpeedController.cs` | 속도 보너스/패널티 |
| `Assets/KnockbackReceiver.cs` | `Assets/Scripts/Player/KnockbackReceiver.cs` | 넉백 처리 |

---

### **2️⃣ UI 폴더**
| 현재 위치 | 목표 위치 | 역할 |
|----------|----------|------|
| `Assets/CircularHealthUI.cs` | `Assets/Scripts/UI/CircularHealthUI.cs` | 원형 체력 UI |
| `Assets/CircularDividersUI.cs` | `Assets/Scripts/UI/CircularDividersUI.cs` | 원형 UI 구분선 |
| `Assets/ScreenFaders.cs` | `Assets/Scripts/UI/ScreenFaders.cs` | 화면 페이드 효과 |
| `Assets/ButtonToNextScene.cs` | `Assets/Scripts/UI/ButtonToNextScene.cs` | UI 버튼 & 씬 로드 |
| `Assets/PortalNextMap.cs` | `Assets/Scripts/UI/PortalNextMap.cs` | 포탈 & 맵 전환 (이름 변경됨) |
| `Assets/PopupPanelController.cs` | `Assets/Scripts/UI/PopupPanelController.cs` | 팝업 패널 제어 |
| `Assets/DungeonPopupPanelController.cs` | `Assets/Scripts/UI/DungeonPopupPanelController.cs` | 던전 팝업 패널 |
| `Assets/WorldMapPausePopup.cs` | `Assets/Scripts/UI/WorldMapPausePopup.cs` | 월드맵 일시정지 팝업 |
| `Assets/ButtonPopupLink.cs` | `Assets/Scripts/UI/ButtonPopupLink.cs` | 팝업 연결 버튼 |
| `Assets/ButtonDungeonLink.cs` | `Assets/Scripts/UI/ButtonDungeonLink.cs` | 던전 연결 버튼 |
| `Assets/QuitGameButton.cs` | `Assets/Scripts/UI/QuitGameButton.cs` | 게임 종료 버튼 |

---

### **3️⃣ Traps 폴더**
| 현재 위치 | 목표 위치 | 역할 |
|----------|----------|------|
| `Assets/Slime.cs` | `Assets/Scripts/Traps/Slime.cs` | 슬라임 함정 (속도 감소) |
| `Assets/RollingBallTrap.cs` | `Assets/Scripts/Traps/RollingBallTrap.cs` | 굴러오는 바위 |
| `Assets/WallSpikeTrap.cs` | `Assets/Scripts/Traps/WallSpikeTrap.cs` | 벽 가시 함정 |
| `Assets/Thorn.cs` | `Assets/Scripts/Traps/Thorn.cs` | 가시 장애물 |
| `Assets/BallLauncher.cs` | `Assets/Scripts/Traps/BallLauncher.cs` | 공 발사기 |
| `Assets/Bow.cs` | `Assets/Scripts/Traps/Bow.cs` | 화살 발사 함정 |
| `Assets/Arrow.cs` | `Assets/Scripts/Traps/Arrow.cs` | 화살 투사체 |
| `Assets/PitTile.cs` | `Assets/Scripts/Traps/PitTile.cs` | 낭떠러지 |
| `Assets/SwitchStopper002002.cs` | `Assets/Scripts/Traps/SwitchStopper002002.cs` | 스위치 정지기 |
| `Assets/TShapeSensorTrigger_PlayerOnly.cs` | `Assets/Scripts/Traps/TShapeSensorTrigger_PlayerOnly.cs` | T자 센서 |

---

### **4️⃣ Map 폴더 (부분적으로 이미 정렬됨)**
| 현재 위치 | 목표 위치 | 역할 |
|----------|----------|------|
| `Assets/JsonBlockBuilder3D.cs` | `Assets/Scripts/Map/JsonBlockBuilder3D.cs` | JSON 맵 빌더 |
| `Assets/FloorCode.cs` | `Assets/Scripts/Map/FloorCode.cs` | 타일 타입 정의 |
| `Assets/Scripts/Map/BlockRegistry.cs` | `Assets/Scripts/Map/BlockRegistry.cs` | ✅ 이미 정렬됨 |
| `Assets/Scripts/Map/BlockCoord.cs` | `Assets/Scripts/Map/BlockCoord.cs` | ✅ 이미 정렬됨 |

---

### **5️⃣ Core 폴더 (데이터 & 인터페이스)**
| 현재 위치 | 목표 위치 | 역할 |
|----------|----------|------|
| `Assets/Scripts/Map/Interfaces/ITrapToggle.cs` | `Assets/Scripts/Core/ITrapToggle.cs` | 함정 토글 인터페이스 |
| `Assets/DungeonDatabase.cs` | `Assets/Scripts/Core/DungeonDatabase.cs` | 던전 데이터베이스 |
| `Assets/DungeonModels.cs` | `Assets/Scripts/Core/DungeonModels.cs` | 던전 데이터 모델 |

---

### **6️⃣ Manager 폴더 (게임 흐름 관리)**
| 현재 위치 | 목표 위치 | 역할 |
|----------|----------|------|
| `Assets/GameFlowManager.cs` | `Assets/Scripts/Manager/GameFlowManager.cs` | 게임 흐름 관리 |
| `Assets/MapSequenceManager.cs` | `Assets/Scripts/Manager/MapSequenceManager.cs` | 맵 시퀀스 관리 |
| `Assets/SaveManager.cs` | `Assets/Scripts/Manager/SaveManager.cs` | 저장 데이터 관리 |

---

### **7️⃣ System 폴더 (공용 유틸리티)**
| 현재 위치 | 목표 위치 | 역할 |
|----------|----------|------|
| `Assets/FrameLimiter.cs` | `Assets/Scripts/System/FrameLimiter.cs` | 프레임 제한/부스트 |
| `Assets/ReplaceSelfWithPrefabOnDisable.cs` | `Assets/Scripts/System/ReplaceSelfWithPrefabOnDisable.cs` | 프리팹 자동 교체 |
| `Assets/LoadSceneByNameButton.cs` | `Assets/Scripts/System/LoadSceneByNameButton.cs` | 씬 로드 유틸리티 |
| `Assets/SceneButtonLoader.cs` | `Assets/Scripts/System/SceneButtonLoader.cs` | 씬 버튼 로더 |
| `Assets/helloworld.cs` | `Assets/Scripts/System/HelloLogger.cs` (권장 개명) | 테스트 로거 |

---

## 🏷️ 네임스페이스 규칙 (권장)

```csharp
// Player
namespace Dungeon.Game.Player { ... }

// UI
namespace Dungeon.Game.UI { ... }

// Traps
namespace Dungeon.Game.Traps { ... }

// Map
namespace Dungeon.Game.Map { ... }

// Core (데이터, 인터페이스)
namespace Dungeon.Game.Core { ... }

// Manager (게임 흐름)
namespace Dungeon.Game.Manager { ... }

// System (유틸리티)
namespace Dungeon.Game.System { ... }
```

---

## ✅ 체크리스트

- [O] 모든 폴더 생성 (`Assets/Scripts/Player`, `Assets/Scripts/UI` 등)
- [O] 파일 이동 (유니티에서 이동하면 `.meta` 자동 유지됨)
- [ ] 네임스페이스 추가
- [ ] `using` 선언 업데이트 (필요 시)
- [ ] 씬 및 프리팹 Inspector 재연결 확인
- [ ] 컴파일 검증

---

## 📝 추가 권장사항

1. **Assembly Definition 파일 (`.asmdef`) 추가** (선택)
   - `Assets/Scripts/Scripts.asmdef` (통합)
   - 또는 카테고리별: `Assets/Scripts/Player/Player.asmdef` 등

2. **인터페이스 분리**
   - `ITrapToggle`를 `Map/Interfaces`에서 `Core`로 이동

3. **파일 개명 (선택)**
   - `helloworld.cs` → `HelloLogger.cs` (PascalCase 규칙)

4. **백업**
   - 리팩토링 전 전체 `Assets` 폴더 백업 권장

---

## 다음 단계

1. 이 매핑표를 확인하고 동의하기
2. 리팩토링 계획 수립 (파일 이동 순서, 스크립트 작성 등)
3. 자동화 스크립트 생성 (선택)
4. 실행 및 검증
