# 스테이지 맵 툴 — Unity 에디터 수기 배선 가이드

`ToolScene`에서 맵툴을 동작시키기 위해 **Unity 에디터에서 직접 해야 하는 작업**만 모았다.

> **모델 전환(2026-06-19)**: 브러시 모델 → **선택+인스펙터 모델**(빈 격자에서 `+`로 셀 생성, 셀 선택해 인스펙터로 편집). 상세 `STAGE_MAP_TOOL.md`.
> **1단계(그리드 뷰)는 완료**(프리팹/씬 배선 + 검증). 아래 1단계는 완료 기록으로 남기며, 2단계 이후는 신모델 기준으로 갱신함.

- 대상 씬: `Assets/01_Scenes/ToolScene.unity` (에디터 전용, `SceneEnum` 미등록)
- 관련 아키텍처: `STAGE_MAP_TOOL_ARCH.md`
- 진행 메모: `STAGE_MAP_TOOL.md`

---

## 1단계. 그리드 뷰 배선 (✅ 완료 — `Assets/01_Scenes/ToolPrefab/MapCell.prefab`)

> 완료 결과: 셀 프리팹 `MapCell`(Image+Button+`StageMapCellView`+`Label`(`+`,44pt)+`Selection`(노랑 하이라이트)), `Canvas/CellRoot`+`BoardBackground`(어두운 판), `StageMapToolRoot/BoardView`(`StageMapBoardView`, `_cellPrefab`/`_cellRoot`/`_cellSize=64`/`_cellSpacing=6`), 컨트롤러 `_boardView` 연결. 아래는 절차 기록.

### 1-1. 셀 프리팹 제작
1. `StageMapCellView` 한 칸을 표현할 프리팹을 만든다. 권장 크기 **64×64**, 앵커/피벗 **좌하단 (0, 0)**.
2. 자식 구성:
   - `Image` (배경 틴트용)
   - `Button` (클릭 수신)
   - (선택) `TMP_Text` (블럭ID/좌표 라벨)
3. 프리팹 루트에 `StageMapCellView` 컴포넌트 부착 후 인스펙터 연결:

| 필드 | 연결 대상 |
|------|-----------|
| `_background` | 배경 `Image` |
| `_button` | `Button` |
| `_label` | `TMP_Text` (없으면 비워도 됨) |

### 1-2. 셀 루트 배치
- 보드 영역에 빈 `RectTransform`(예: `CellRoot`)을 추가한다.
- 앵커/피벗 **좌하단**. 셀들은 이 오브젝트 자식으로 좌하단 원점 기준 배치된다.

### 1-3. 보드 뷰 부착
- 빈 오브젝트에 `StageMapBoardView` 컴포넌트 부착 후 연결:

| 필드 | 연결 대상 |
|------|-----------|
| `_cellPrefab` | 1-1에서 만든 셀 프리팹 |
| `_cellRoot` | 1-2의 `CellRoot` |
| `_cellSize` | 셀 픽셀 크기(기본 64) |

### 1-4. 컨트롤러 연결
- `StageMapToolController`의 `_boardView`(BOARD COMPONENT 헤더)에 1-3 보드 뷰를 연결한다.

### 1단계 검증 (완료)
> 플레이 시 **9×9 빈 격자**(전부 `+`)가 그려지고, `+` 클릭 시 그 칸이 셀(Normal=흰색)로 생성되며, 생성된 셀 클릭 시 노랑 하이라이트가 표시되면 OK.
> 퍼즐타입 `Link` 선택 시 보드가 헥사(짝수 열 반 칸 아래)로 배치되면 헥사 경로 확인.
> (검증은 부트스트랩이 SharedScene으로 전환되므로 `execute_code`로 보드 빌드를 직접 실행해 확인함.)

---

## 2단계. 인스펙터 패널 배선 (Phase 2 — 미착수)

> 모델 전환으로 "브러시 버튼"은 폐기. 대신 **셀을 선택하면 그 셀을 편집하는 인스펙터 패널**을 띄운다.
> 셀 선택 시 `EditButtonPanelRoot`(빈 컨테이너) 아래 패널을 노출하고, 버튼은 **선택된 셀**의 값을 직접 바꾼다.

### 2-1. 편집 패널 3종 배치
- `EditButtonPanelRoot` 아래 `_cellEditPanel` / `_blockEditPanel` / `_tileEditPanel` 3개 패널 생성(편집 모드 버튼이 하나만 토글).
- 컨트롤러 동명 필드(`_cellEditPanel`/`_blockEditPanel`/`_tileEditPanel`)에 연결.

### 2-2. 셀 상태변화 버튼 (셀 편집 패널)
- `Normal` / `Lock` / `Generator` / `삭제(→+)` 버튼. 콜백은 **선택된 셀**에 적용된다(`OnClickCellType` 재활용 + 삭제는 `RemoveCell`).
- ⚠️ `CellType` 내부 값(Close=0, Normal=1, Lock=2, Generator=3)과 버튼 인덱스가 다름 → 컨트롤러 `BrushCellTypes` 매핑 사용.
- `UIButtonGroup` + 컨트롤러 `_cellTypeButtonGroup` 연결(기존 코드 유지).

### 2-3. 블럭 / 판넬 (Phase 2.5)
- 블럭ID는 Rule 로드(`StageMapRuleProvider` 신규)로 팔레트 구성, 판넬ID는 우선 직접 입력/증감으로 시작.

### 2단계 검증
> 셀을 선택한 상태에서 `Lock` → 그 셀이 파란색으로, `삭제` → 다시 `+`로 돌아가면 OK.
> 선택 강조(노랑)와 인스펙터 표시가 선택 셀과 일치하면 완료.

---

## 참고: 아직 코드가 없는 다음 단계 (배선 불필요)

| 단계 | 내용 | 선행 코드 |
|------|------|-----------|
| 3 | Rule 로드 + 블럭 팔레트 + 타일(panelId) | `StageMapRuleProvider` 신규 작성 필요 |
| 4 | 저장/로드/검증 UI (StageId 선택, Save/Load, 결과 표시) | 컨트롤러에 저장 파이프라인 연결 필요 |
| 5 | 편집 스테이지로 게임 실행 테스트 | `StageInjection.MakeGameSpec` 연동 |

3단계 이후는 코드부터 추가한 뒤 배선 항목을 본 문서에 이어서 정리한다.
