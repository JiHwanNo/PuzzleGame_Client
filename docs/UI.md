# PuzleBattleGame UI/도메인 시스템 참고 문서

UI, 팝업, 탭 관련 작업 시 이 문서를 참고.

## 콘텐츠와 C# 매핑

[전체 작업 지도](MAP.md) · [문서 목록](README.md)

파일 링크는 이 문서 기준 상대 경로이며, 마지막 열의 이름을 파일 안에서 검색하면 구현 위치를 찾을 수 있습니다.

| 찾는 콘텐츠 | 담당 C# 파일 | 주요 메서드·타입·멤버 |
|---|---|---|
| 팝업/탭 스택·뒤로 가기·전체 닫기 | [DomainManager.cs](../Assets/02_Scripts/Manager/DomainManager.cs) | `OpenPopup`, `OpenTab`, `Back`, `CloseAll` |
| 팝업/탭 공통 계약 | [IDomainNode.cs](../Assets/02_Scripts/Manager/IDomainNode.cs) | `IDomainNode` |
| 팝업 생성·파괴·씬별 등록 | [PopupController.cs](../Assets/02_Scripts/Manager/PopupController.cs) | `CreatePopup`, `DestroyPopup` |
| 팝업 열기/닫기 생명주기·애니메이션 | [PopupBase.cs](../Assets/02_Scripts/Manager/PopupBase.cs) | `Open`, `Close`, `OpenRoutine`, `CloseRoutine` |
| 팝업 버튼 처리용 기반 클래스 | [PopupHandler.cs](../Assets/02_Scripts/Manager/PopupHandler.cs) | `ClosePopup`, `BackPopup` |
| 탭 검색·활성화·비활성화 | [TabController.cs](../Assets/02_Scripts/Manager/TabController.cs) | `ActivateTab`, `DeactivateTab` |
| 탭 생명주기·애니메이션 확장 | [TabBase.cs](../Assets/02_Scripts/Manager/TabBase.cs) | `Activate`, `Deactivate`, `ActivateAnimation`, `DeactivateAnimation` |
| Inspector 버튼 콜백 연결 | [UIButton.cs](../Assets/02_Scripts/UI/UIButton.cs) | `OnClickEvent` |
| 게임 준비·스테이지·60초 대전·최근 리플레이 진입 | [PopupReady.cs](../Assets/02_Scripts/UI/Popup/PopupReady.cs) | `OnClickStart`, `OnClickBattle`, `OnClickReplay`, `OnClickClose` |
| Shared 공용 팝업 등록 | [SharedPopupController.cs](../Assets/02_Scripts/Manager/SharedPopupController.cs) | `ControllerName` |
| 로비 팝업 등록 | [LobbyPopupController.cs](../Assets/02_Scripts/Lobby/LobbyPopupController.cs) | `ControllerName` |
| 게임 팝업 등록 | [GamePopupController.cs](../Assets/02_Scripts/PuzzleCore/Controller/GamePopupController.cs) | `ControllerName` |
| 로비 탭 컴포넌트 (TabBase 상속) | [LobbyTabController.cs](../Assets/02_Scripts/LobbyTabController.cs) | `LobbyTabController` |
| 대전 전용 피벗 충전·활성 게이지 | [PivotGaugeView.cs](../Assets/02_Scripts/Battle/Pivot/PivotGaugeView.cs) | `PivotGaugeView`, `Refresh`, `Dispose` |

피벗 게이지는 대전 시작 시 기존 타이머 아래에 생성하며 같은 한글 폰트를 사용한다. 게임 종료 시 숨기고 컨트롤러 제거 시 UI를 정리한다. 상태 및 설정 규칙은 [INGAME.md](INGAME.md#대전-전용-피벗-모드)를 참고한다.

대전 시작은 현재 콤보 판정 버전과 Rule JSON의 피벗 설정이 일치하는 완주 기록만 상대로 선택한다. 호환되는 상대가 없으면 첫 기록 도전으로 진입한다. 이전 규칙의 리플레이는 재생할 수 있지만 승패 비교에서는 제외한다.

상대 탐색 중 동일 Rule/Stage 데이터는 요청 내에서 재사용하며, 캐시된 후보를 선택하면 실제 씬 주입 데이터도 해당 후보로 맞춘다. 상대 상태 UI는 표시할 상태·남은 초·점수·피벗 수치가 바뀔 때만 문자열을 생성한다.

---

## 도메인 시스템 개요

URL 방식 경로로 UI 상태를 추적 (예: `/Lobby/Shop/ItemDetail`).

```
DomainManager (SharedScene 싱글톤)
├─ 팝업: 스택 방식 (생성/파괴)
│   PopupController → PopupBase (프리팹) → PopupHandler (로직)
└─ 탭: 전환 방식 (활성/비활성)
    TabController → TabBase (UI)
```

---

## DomainManager API

### 열기
| 메서드 | 동작 |
|--------|------|
| `OpenPopup(name, callback?)` | 팝업 프리팹 로드 → 스택에 추가 → Open 애니메이션 |
| `OpenTab(name, callback?)` | 이전 탭 위의 팝업 모두 닫기 → 이전 탭 비활성화 → 새 탭 활성화 |

### 닫기
| 메서드 | 동작 |
|--------|------|
| `Close(name)` | 해당 도메인 + 그 위의 모든 도메인 닫기 |
| `Back()` | 최상위 도메인 하나 닫기 |
| `CloseAll()` | 전체 도메인 닫기 (씬 전환 시 Main에서 호출) |

### 조회
| 메서드 | 반환 |
|--------|------|
| `Peek()` | 최상위 IDomainNode |
| `IsOpen(name)` | bool |
| `GetDomain<T>(name)` | 타입 캐스팅된 도메인 인스턴스 |
| `CurrentPath` | URL 문자열 (예: "/Lobby/Shop") |

### 컨트롤러 등록
- `Register(PopupController)` / `Unregister(PopupController)`
- `Register(TabController)` / `Unregister(TabController)`
- OnEnable/OnDisable에서 자동 호출됨

---

## 팝업 생명주기

```
DomainManager.OpenPopup("ItemDetail")
  → _activePopupController.CreatePopup("ItemDetail")
    → AssetManager.LoadGameObject("Popup/ItemDetail")  ← Addressable 주소 패턴
      → 인스턴스 생성 → PopupBase 컴포넌트 확보
        → _domainStack에 추가
          → PopupBase.Open()
            → dim 페이드인 + content 스케일 (0→1, OutBack, 0.3초)
              → OnOpened 이벤트
```

```
PopupHandler.ClosePopup() 또는 DomainManager.Close(name)
  → PopupBase.Close()
    → content 스케일 (1→0, InBack) + dim 페이드아웃
      → OnClosed 이벤트
        → PopupController.DestroyPopup() → GameObject 파괴
```

### PopupBase 주요 멤버
| 멤버 | 용도 |
|------|------|
| `PopupName` / `DomainName` | 식별 이름 |
| `IsAnimating` | 애니메이션 중 여부 (true일 때 조작 차단) |
| `_dimBackground` (Image) | 딤 배경 (alpha 0→0.5) |
| `_contentPanel` (RectTransform) | 콘텐츠 패널 (스케일 애니메이션) |
| `_animationDuration` | 기본 0.3초 |
| `Open()` / `Close(callback?)` | 열기/닫기 애니메이션 |
| `RequestClose()` | `DomainManager.Close(PopupName)` 호출 |
| `RequestBack()` | `DomainManager.Back()` 호출 |

---

## 팝업 핸들러 (PopupHandler)

팝업별 로직을 구현하는 추상 클래스. PopupBase와 같은 GameObject에 부착.

```csharp
// 예: PopupReady.cs
public class PopupReady : PopupHandler
{
    private void OnClickStart() { /* 게임 데이터 준비 → GameScene 이동 */ }
    private void OnClickReplay() { /* 최근 리플레이 로드 → SetReplayData → GameScene 이동 */ }
    private void OnClickClose() { ClosePopup(); }
}
```

- `ClosePopup()`: `DomainManager.Close(PopupName)` (IsAnimating 중이면 차단)
- `BackPopup()`: `DomainManager.Back()` (IsAnimating 중이면 차단)
- 메서드는 `UIButton`의 `SendMessage`로 호출됨

---

## 탭 생명주기

```
DomainManager.OpenTab("ShopTab")
  → 이전 탭 위의 팝업 전부 Close
    → 이전 탭 Deactivate
      → _activeTabController.ActivateTab("ShopTab")
        → _tabRoot 하위에서 TabBase 검색 (비활성 포함)
          → TabBase.Activate()
            → OnActivateStart() → ActivateAnimation() → OnActivateComplete()
              → OnActivated 이벤트
```

### TabBase 주요 멤버
| 멤버 | 용도 |
|------|------|
| `TabName` / `DomainName` | 식별 이름 (기본값: GameObject.name) |
| `IsAnimating` | 애니메이션 중 여부 |
| `Activate()` | 활성화 (SetActive(true) + 애니메이션) |
| `Deactivate(callback?)` | 비활성화 (애니메이션 → SetActive(false)) |

### 오버라이드 포인트 (protected virtual)
```csharp
OnActivateStart()       // 활성화 시작 전
ActivateAnimation()     // IEnumerator - 커스텀 애니메이션
OnActivateComplete()    // 활성화 완료 후
OnDeactivateStart()     // 비활성화 시작 전
DeactivateAnimation()   // IEnumerator - 커스텀 애니메이션
OnDeactivateComplete()  // 비활성화 완료 후
```

---

## UIButton (입력 연결)

Inspector에서 설정하는 범용 버튼 컴포넌트.

| 필드 | 용도 |
|------|------|
| `_root` (MonoBehaviour) | 메시지를 받을 대상 (PopupHandler, TabBase 등) |
| `_unityButton` (Button) | Unity Button 컴포넌트 |
| `_callbackName` (string) | 호출할 메서드 이름 |
| `_callbackValue` (string) | 선택적 문자열 인자 |

**동작**: `Button.onClick` → `UIButton.OnClickEvent()` → `_root.SendMessage(_callbackName, _callbackValue?)`

---

## 씬별 컨트롤러

| 컨트롤러 | ControllerName | 위치 |
|----------|---------------|------|
| SharedPopupController | `"Shared"` | SharedScene (공용 팝업: 네트워크 에러 등) |
| LobbyPopupController | `"Lobby"` | LobbyScene |
| GamePopupController | `"Game"` | GameScene |
| LobbyTabController | — | Scripts 루트 (TabBase 상속) |

---

## 팝업 추가 방법 (체크리스트)

1. `Assets/03_Prefab/UI/Popup/`에 프리팹 생성
2. PopupBase 컴포넌트 부착 (`_dimBackground`, `_contentPanel` 연결)
3. PopupHandler 서브클래스 작성 (같은 GameObject)
4. Addressable 주소 등록: `Popup/{팝업이름}`
5. UIButton으로 버튼 → Handler 메서드 연결 (`_root` = Handler, `_callbackName` = 메서드명)
6. 호출: `DomainManager.Instance.OpenPopup("팝업이름")`

## 탭 추가 방법 (체크리스트)

1. TabController의 `_tabRoot` 하위에 GameObject 생성
2. TabBase 서브클래스 컴포넌트 부착
3. `TabName` 설정 (또는 GameObject 이름 사용)
4. 필요 시 `ActivateAnimation()` / `DeactivateAnimation()` 오버라이드
5. 호출: `DomainManager.Instance.OpenTab("탭이름")`

## 준비 팝업의 플레이 진입
- `Stage`: 기존 스테이지 시작. 퍼즐 종류를 랜덤 선택하고 목표 달성 또는 제한 시간으로 종료한다.
- `Battle 60s`: 가장 최근의 유효한 60초 완주 대전 기록을 상대로 선택한다. 기록이 없으면 혼자 60초를 플레이하여 첫 대전 기록을 만든다.
- `Replay`: 가장 최근 기록을 원래 종료 규칙으로 재생한다. 이전/스테이지 기록도 볼 수 있으며 대전 기록으로 변경하지 않는다.
- 버튼은 `PopupReady.prefab`의 기존 `UIButton` 메시지 연결 방식을 사용한다.
- 대전 점수 비교와 상대 상태 표시는 `GameScene.unity`에 연결된 TMP UI이며 상세는 [INGAME.md](INGAME.md#스테이지와-60초-대전)를 참고한다.
