# 프리팹 구조 규칙 (Prefab Structure Convention)

UI/위젯 프리팹의 **계층·컴포넌트 배치·입력 배선** 표준. 신규/리팩터링 프리팹은 이 규칙을 따른다.

> 입력 처리(`UIButton`) 상세는 [`UI.md`](../04_UI/UI.md) "UIButton (입력 연결)" 절 참고.

---

## 1. 계층

- **Root = 제어 스크립트(View/Controller)만.** Root에는 상호작용 그래픽(Image/Button)을 직접 붙이지 않는다. 컴포넌트는 `RectTransform` + 제어 스크립트뿐.
- **시각/입력 요소는 Root 직속 형제로 평탄하게 둔다.** 메인 이미지의 child로 중첩하지 않는다(종속되면 다루기 번거롭다). 겹침은 자식 순번으로 제어한다.

```
[Root]                  ← 제어 스크립트 (예: StageMapCellView)
├─ SelectionImage       ← 선택/상태 표시(뒤쪽 렌더), SetActive 토글  [RaycastTarget off]
├─ CellImage            ← Image(메인 비주얼) + Button + UIButton     [RaycastTarget on]
├─ Label (TMP)          ← 텍스트                                     [RaycastTarget off]
└─ Icon (Image)         ← 추가 비주얼                                [RaycastTarget off]
```

- 클릭을 받는 **메인 이미지에만** `RaycastTarget = on`. 나머지는 off.
- 선택 표시를 **메인 이미지 뒤로** 깔려면 앞 순번에 두고, 메인 이미지보다 **약간 크게** 만들어 가장자리가 후광처럼 보이게 한다.

---

## 2. 입력 배선 (UIButton)

`UIButton`은 클릭이 일어나는 노드(메인 이미지)에 **Button과 함께** 붙인다.

| UIButton 필드 | 설정 |
|---------------|------|
| `_unityButton` / `_buttonImage` | 같은 노드의 `Button` / `Image` |
| `_root` | Root의 제어 스크립트 |
| `_callbackName` | Root에서 호출할 public 메서드명 |
| `_callbackValue` | 선택 인자(문자열), 없으면 비움 |

- `Button.onClick`에 **영구 호출 1개**: Target = 같은 노드 `UIButton`, Method = `OnClickEvent`.
- 동작: `Button.onClick` → `UIButton.OnClickEvent()` → `_root.SendMessage(_callbackName, _callbackValue?)`.
- 좌표 등 다중 인자는 문자열 파싱 대신 **Root가 상태를 보관**하고 인자 없는 콜백에서 처리.
- 런타임 생성 시: `UIButton.SetCallback(root, callbackName, callbackValue)`.

---

## 3. 선택/상태 표시

- **단일 Image**를 메인 이미지의 형제로 두고 `SetActive`로 토글한다(`_selectionOutline.SetActive(selected)`).
- 하이라이트 형태(테두리/반투명 등)는 **스프라이트·알파로** 표현한다 — 구조로 풀지 않는다.

---

## 4. 체크리스트

1. Root 생성 → 제어 스크립트 부착 (Image/Button 금지).
2. `CellImage`(메인 이미지) → `Image` + `Button` + `UIButton`, `Button.onClick`에 `OnClickEvent` 등록.
3. `UIButton` 필드 배선(2절), Root에 `_callbackName`과 같은 public 콜백 메서드 작성.
4. Label/Icon/Selection은 Root 직속 형제, `RaycastTarget = off`.
5. 프리팹은 `Assets/Resources/Prefab/...`에 저장(프리팹=Resources, 데이터=Addressables).

---

## 5. 예시 — MapCell

```
MapCell                 ← StageMapCellView
├─ SelectionImage       ← _selectionOutline, 뒤쪽 렌더(+4px 후광), SetActive 토글
├─ CellImage            ← Image(_background) + Button + UIButton (_root=StageMapCellView, _callbackName=OnCellClicked)
├─ Label (TMP)
└─ BlockIcon
```

`StageMapCellView`는 `Bind(x, y, ...)`에서 좌표를 보관하고 `public void OnCellClicked()`에서 `(x, y)` 콜백을 호출한다.
