# PuzleBattleGame UI/도메인 시스템 참고 문서

UI, 팝업, 탭 관련 작업 시 이 문서를 참고.

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
