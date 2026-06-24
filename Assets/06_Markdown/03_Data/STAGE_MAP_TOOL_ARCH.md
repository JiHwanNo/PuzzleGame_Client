# 스테이지 맵 툴 아키텍처

ToolScene(에디터 전용)의 스테이지 맵 편집기 아키텍처와 구현 순서.
클래스 레퍼런스는 `DATA_STAGE.md`, 진행 상태/작업 메모는 `STAGE_MAP_TOOL.md` 참고.

---

## 1. 목적과 범위

- 모드별 스테이지 JSON(`Resources/Stage/{모드}/Stage_NNN.json`)을 시각적으로 편집·검증·저장하는 **에디터 전용 도구**.
- 런타임 게임 빌드에 포함되지 않는다. `ToolScene`은 `SceneEnum`에 등록하지 않는다.
- 인게임 보드/뷰와 **로직을 공유하지 않는다**. 편집 전용 경량 그리드 뷰를 별도로 둔다(게임 로직 결합 회피).

### 편집 모델 (선택 + 인스펙터)
- 맵 미로드 시 **모든 칸이 빈 `+`**(최대 9×9). `+` 클릭 → 셀 생성(`Normal`). 생성된 셀 클릭 → 선택(하이라이트) → **인스펙터로 편집**(블럭/판넬/셀 상태변화/삭제→`+`).
- **빈 칸 `+` ≡ `CellType.Close`**: 편집 중엔 `cells` 부재(존재=셀), 저장 시 9×9 전 좌표를 채우되 빈 칸은 `Close`로 내보낸다(인게임 로더 호환). 로드 시 `Close`/부재 → `+`.
- **보드 모양**: `PuzzleType.Link`면 헥사(Even-Q Flat-Top), 그 외 사각. 짝수 열 반 칸 아래(인게임 `PuzzleBoardView` 규칙).

---

## 2. 레이어 구조

```
ToolScene (에디터 전용 씬)
└─ StageMapToolController : MonoBehaviour          [Runtime]  UI 입력 → 상태/뷰 오케스트레이션
   ├─ StageMapBoardView : MonoBehaviour            [Runtime]  ★신규 — StageData를 그리드로 렌더, 셀 클릭 발화
   │   └─ StageMapCellView : MonoBehaviour         [Runtime]  ★신규 — 단일 셀 시각(타입/블럭/패널) + 클릭(x,y)
   ├─ StageMapToolState                            [Core]     편집 상태 모델 (PuzzleType/StageId/Rule/StageData/Brush)
   ├─ StageMapCellBrush                            [Core]     칠할 값 (cellType/blockId/panelId/generatorIds)
   ├─ StageMapJsonRepository                       [Core]     StageData JSON Load/Save (UnityEditor 의존)
   ├─ StageMapValidator → StageMapValidationResult [Core]     저장 전 정합성 검증
   └─ StageMapRuleProvider                         [Core]     ★신규 — RuleAddress로 현재 규칙 BlockData 목록 로드
```

| 레이어 | 책임 | 비고 |
|--------|------|------|
| Scene | UI 하이어라키, 패널/버튼 배치 | `Assets/01_Scenes/ToolScene.unity` |
| Runtime (MonoBehaviour) | 입력 수신, 뷰 렌더, 상태/IO 호출 조율 | Controller + 신규 View 2종 |
| Core | 편집 상태·브러시·JSON IO·검증·규칙 로드 | 순수 C# 우선, IO/Provider는 Unity 의존 허용 |

**Core 순수성 규약**: 에디터 전용 도구이므로 인게임 Model의 `UnityEngine` 금지 규칙은 적용하지 않는다. 단 `StageMapToolState`/`StageMapValidator`/`StageMapCellBrush`는 테스트 가능성을 위해 `UnityEngine` 의존 없이 유지하고, Unity 의존(파일 IO·Addressable·`AssetDatabase`)은 `StageMapJsonRepository`/`StageMapRuleProvider`에 격리한다.

---

## 3. 데이터 흐름

```
[로드]   퍼즐타입/StageId 선택
  → StageMapRuleProvider.Load(ruleAddress)        → List<BlockData> (블럭 팔레트·검증 공용)
  → StageMapJsonRepository.LoadOrCreate(type, id) → StageData
  → state.SetStage(stageData) → boardView.Build(stageData)

[편집]   빈 칸(+) 클릭 → state.CreateCell(x,y) (Normal 생성)
  → 생성된 셀 클릭 → boardView.Select(x,y) → 인스펙터 노출
  → 인스펙터 값 변경(state.PaintCell 등) / 삭제(state.RemoveCell) → boardView.RefreshCell(x,y)

[저장]   저장 버튼
  → validator.Validate(stageData, stageId, ruleBlocks) → result
  → result.IsValid() ? repository.SaveToResources(...) : 오류 표시
```

핵심 불변식: **뷰는 상태를 그리기만 하고, 편집은 항상 `State`(`CreateCell`/`RemoveCell`/`PaintCell`)를 거친다.** 뷰가 `CellData`를 직접 수정하지 않는다.

---

## 4. 전용 그리드 뷰 설계 (신규)

- `StageMapBoardView`
  - `Build(StageData, hexLayout)`: 기존 셀 뷰 제거 후 `stage_width × stage_height` **전체 격자(빈 칸 포함)**를 `StageMapCellView`로 생성·배치.
  - `RefreshCell(x, y)`: 한 셀만 선택 상태 반영해 다시 그림.
  - `Select(x,y)`/`ClearSelection()`: 선택 셀 하이라이트 관리(이전 선택 해제).
  - `event Action<int,int> OnCellClicked`: 셀 클릭을 Controller로 전달.
  - 좌표는 인게임 `GetLocalPos`를 쓰지 않고 툴 자체 규칙(좌하단 원점, `_cellSize`+`_cellSpacing` 스텝). 헥사면 짝수 열 반 칸 아래.
- `StageMapCellView` — **3-state**
  - `+`(빈 칸, `cell==null` 또는 `Close`): 어두운 타일 + 밝은 `+` 라벨(클릭 affordance).
  - 생성됨: 셀 타입(Normal/Lock/Generator) 틴트 + 블럭ID 라벨.
  - 선택: `_selectionOutline` 하이라이트 ON.
  - `Bind(x,y,cell,onClicked)` / `Refresh(cell,selected)`. 클릭 시 자신의 `(x,y)`로 발화.
- Controller가 `OnCellClicked` 구독 → 빈 칸이면 `state.CreateCell` → `RefreshCell`, 그 후 `boardView.Select`.

---

## 5. Rule 로드 설계 (신규)

- `StageMapRuleProvider.Load(ruleAddress)` → 현재 규칙의 `List<BlockData>` 반환.
- 사용처: ① 블럭 편집 팔레트(선택 가능한 blockId 목록) ② `StageMapValidator`의 `ruleBlocks` 인자.
- 로드 경로: 프로젝트 규약(Addressables/`AssetManager`)을 따른다. 에디터 동기 로드가 필요하면 `05_Table/Rule/{ruleAddress}.json` 직접 로드를 폴백으로 둔다(택1은 구현 시 확정).

---

## 6. 구현 순서 (의존성 순)

> 각 단계는 독립 동작·검증 가능한 단위. 1번부터 진행한다.

> 재설계(2026-06-19): 브러시 모델 → 선택+인스펙터 모델. 진행 상태는 `STAGE_MAP_TOOL.md` 참고.

1. **Phase 1 — 기반 전환 (✅ 완료)**: 빈 격자(9×9, 전부 `+`) + `+`클릭→`CreateCell`(Normal) + 셀 선택(하이라이트) + Link 헥사 + 셀 간격. `StageMapBoardView.Build(stage,hex)`/`Select`, `StageMapCellView` 3-state, `state.CreateCell/RemoveCell`, `Repository.CreateEmptyStage`. 씬: `MapCell` 프리팹(+`Selection`), `CellRoot`(+`BoardBackground`), `BoardView`.
2. **Phase 2 — 인스펙터 패널**: 선택 셀 편집 — ①셀 상태변화(Normal/Lock/Generator + 삭제→`+`) ②블럭ID ③판넬ID. `EditButtonPanelRoot` 활용, 기존 `OnClickCellType`/`Brush`/`PaintCell` 재활용.
3. **Phase 2.5 — Rule 로드**: `StageMapRuleProvider` 작성 → 블럭 팔레트(`Brush.blockId`)·Generator 목록.
4. **Phase 3 — 저장/로드/검증**: 저장 시 빈 칸→`Close` 9×9 채움 + `Validator.Validate` → `Repository.Save*`, StageId/Load/Save UI, 오류·경고 표시.
5. **게임 실행 테스트 진입** — 편집 스테이지로 `StageInjection.MakeGameSpec(ruleAddress, puzzleType, stageId)` 호출해 플레이 검증.

---

## 7. 확장 지점

- 새 셀 타입: `CellType` enum + 브러시 버튼 + `StageMapCellView` 시각 + `Validator` 규칙.
- 새 검증 규칙: `StageMapValidator`에 `AddError`/`AddWarning` 추가(경고는 저장 비차단).
- 저장 대상 추가: `StageMapJsonRepository`에 경로 메서드 추가(현재 Resources/다운로드 2종).
