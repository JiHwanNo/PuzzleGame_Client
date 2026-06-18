# 스테이지 맵 툴 아키텍처

ToolScene(에디터 전용)의 스테이지 맵 편집기 아키텍처와 구현 순서.
클래스 레퍼런스는 `DATA_STAGE.md`, 진행 상태/작업 메모는 `STAGE_MAP_TOOL.md` 참고.

---

## 1. 목적과 범위

- 모드별 스테이지 JSON(`Resources/Stage/{모드}/Stage_NNN.json`)을 시각적으로 편집·검증·저장하는 **에디터 전용 도구**.
- 런타임 게임 빌드에 포함되지 않는다. `ToolScene`은 `SceneEnum`에 등록하지 않는다.
- 인게임 보드/뷰와 **로직을 공유하지 않는다**. 편집 전용 경량 그리드 뷰를 별도로 둔다(게임 로직 결합 회피).

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

[편집]   브러시 버튼 → state.SetBrush(...)
  → 셀 클릭(x,y) → state.PaintCell(x,y) → boardView.RefreshCell(x,y)

[저장]   저장 버튼
  → validator.Validate(stageData, stageId, ruleBlocks) → result
  → result.IsValid() ? repository.SaveToResources(...) : 오류 표시
```

핵심 불변식: **뷰는 상태를 그리기만 하고, 편집은 항상 `State.PaintCell`을 거친다.** 뷰가 `CellData`를 직접 수정하지 않는다.

---

## 4. 전용 그리드 뷰 설계 (신규)

- `StageMapBoardView`
  - `Build(StageData)`: 기존 셀 뷰 제거 후 `stage_width × stage_height`만큼 `StageMapCellView` 생성·배치.
  - `RefreshCell(x, y)`: 한 셀만 다시 그림(브러시 적용 직후 부분 갱신).
  - `event Action<int,int> OnCellClicked`: 셀 클릭을 Controller로 전달.
  - 좌표/셀 크기는 인게임 `GetLocalPos`를 쓰지 않고 툴 자체 규칙(좌하단 원점, 고정 셀 크기)으로 단순화.
- `StageMapCellView`
  - 셀 타입(Normal/Close/Lock/Generator) 틴트, 초기 블럭 스프라이트, 패널 표시, 좌표 라벨.
  - 클릭 시 자신의 `(x, y)`로 `OnCellClicked` 발화.
- Controller가 `OnCellClicked` 구독 → `state.PaintCell` → `RefreshCell`.

---

## 5. Rule 로드 설계 (신규)

- `StageMapRuleProvider.Load(ruleAddress)` → 현재 규칙의 `List<BlockData>` 반환.
- 사용처: ① 블럭 편집 팔레트(선택 가능한 blockId 목록) ② `StageMapValidator`의 `ruleBlocks` 인자.
- 로드 경로: 프로젝트 규약(Addressables/`AssetManager`)을 따른다. 에디터 동기 로드가 필요하면 `05_Table/Rule/{ruleAddress}.json` 직접 로드를 폴백으로 둔다(택1은 구현 시 확정).

---

## 6. 구현 순서 (의존성 순)

> 각 단계는 독립 동작·검증 가능한 단위. 1번부터 진행한다.

1. **그리드 뷰 + 셀 클릭 → PaintCell** (편집의 뼈대)
   - `StageMapBoardView`/`StageMapCellView` 신규 작성.
   - Controller: 시작 시 `Repository.LoadOrCreate`로 StageData 확보 → `state.SetStage` → `boardView.Build`.
   - 셀 클릭 → `state.PaintCell` → `RefreshCell`. (이 단계에서 Repository 로드 경로가 연결됨)
2. **셀 타입 브러시 버튼** — Normal/Close/Lock/Generator → `Brush.cellType`. (Rule 불필요)
3. **Rule 로드 + 블럭 팔레트 + 타일** — `StageMapRuleProvider` 작성 → Block 브러시(`Brush.blockId`)·Generator 목록, Tile(`Brush.panelId`).
4. **저장/로드/검증 파이프라인** — StageId 선택 + Load/Save 버튼, `Validator.Validate` → 통과 시 `Repository.Save*`, 오류/경고 UI 표시.
5. **게임 실행 테스트 진입** — 편집 스테이지로 `StageInjection.MakeGameSpec(ruleAddress, puzzleType, stageId)` 호출해 플레이 검증.

---

## 7. 확장 지점

- 새 셀 타입: `CellType` enum + 브러시 버튼 + `StageMapCellView` 시각 + `Validator` 규칙.
- 새 검증 규칙: `StageMapValidator`에 `AddError`/`AddWarning` 추가(경고는 저장 비차단).
- 저장 대상 추가: `StageMapJsonRepository`에 경로 메서드 추가(현재 Resources/다운로드 2종).
