# 스테이지 맵 툴 — Unity 에디터 수기 배선 가이드

`ToolScene`에서 맵툴을 동작시키기 위해 **Unity 에디터에서 직접 해야 하는 작업**만 모았다.
코드(컨트롤러/뷰/브러시 콜백)는 작성 완료 상태이며, 아래 인스펙터 참조 연결과 프리팹/버튼 배치만 남았다.

- 대상 씬: `Assets/01_Scenes/ToolScene.unity` (에디터 전용, `SceneEnum` 미등록)
- 관련 아키텍처: `STAGE_MAP_TOOL_ARCH.md`
- 진행 메모: `STAGE_MAP_TOOL.md`

---

## 1단계. 그리드 뷰 배선 (ARCH 1단계)

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

### 1단계 검증
> 플레이 시 **8×8 격자**가 그려지고 최상단 행이 Generator(녹색)로 표시되면 OK.
> 셀 클릭 시 해당 칸이 현재 브러시 색(기본 Normal=흰색)으로 바뀌면 클릭→PaintCell→RefreshCell 경로 동작 확인.

---

## 2단계. 셀 타입 브러시 버튼 배선 (ARCH 2단계)

> 코드 추가 완료: `StageMapToolController.OnClickCellType(string val)` + `_cellTypeButtonGroup`(CELL BRUSH COMPONENT 헤더).
> 브러시 = "셀에 칠할 셀 타입". 버튼으로 타입을 고르고 격자 칸을 클릭하면 그 타입으로 칠해진다.

### 2-1. 버튼 4개 배치
- 셀 편집 패널(`_cellEditPanel`) 안에 `UIButton` 4개를 배치한다.
- **버튼 순서(인덱스)와 콜백 값**은 아래 표를 반드시 지킬 것. (enum 값이 아니라 배열 인덱스 기준)

| 버튼 인덱스 (`_callbackValue`) | 셀 타입 | 셀뷰 틴트 색 |
|------|---------|------|
| `0` | Normal (일반 바닥) | 흰색 |
| `1` | Close (막힌 구역) | 어두운 회색 |
| `2` | Lock (잠긴 상태) | 파란색 |
| `3` | Generator (블럭 생성기) | 녹색 |

> ⚠️ `CellType` enum 내부 값은 Close=0, Normal=1, Lock=2, Generator=3 으로 **버튼 인덱스와 다르다**.
> 컨트롤러의 `BrushCellTypes` 배열이 인덱스→타입을 매핑하므로, 버튼에는 **위 표의 인덱스 값**만 넣으면 된다.

### 2-2. 각 버튼 콜백 연결
- 각 `UIButton`의 콜백을 컨트롤러로 연결한다(기존 퍼즐타입/편집모드 버튼과 동일 패턴):
  - `_root` → `StageMapToolController`가 붙은 오브젝트
  - `_callbackName` → `OnClickCellType`
  - `_callbackValue` → `0` / `1` / `2` / `3` (2-1 표)

### 2-3. 버튼 그룹 연결
- 셀 편집 패널에 `UIButtonGroup` 컴포넌트를 두고 위 버튼 4개를 `_buttons` 배열에 **표 순서대로(0=Normal … 3=Generator)** 등록한다.
- `_normalSprite` / `_selectedSprite` 지정(선택 강조용).
- 컨트롤러 `_cellTypeButtonGroup`에 이 그룹을 연결한다.
- 시작 시 컨트롤러가 `ApplyCellType(Normal)`로 0번을 자동 선택한다.

### 2단계 검증
> Lock 버튼 클릭 → 격자 칸 클릭 시 그 칸이 파란색으로 바뀌면 OK.
> Close 클릭 후 칠하면 해당 칸의 block_id/생성목록이 비워진다(PaintCell 규칙).
> 타입 버튼을 바꿔가며 칠했을 때 선택 버튼 강조와 칸 색이 일치하면 완료.

---

## 참고: 아직 코드가 없는 다음 단계 (배선 불필요)

| 단계 | 내용 | 선행 코드 |
|------|------|-----------|
| 3 | Rule 로드 + 블럭 팔레트 + 타일(panelId) | `StageMapRuleProvider` 신규 작성 필요 |
| 4 | 저장/로드/검증 UI (StageId 선택, Save/Load, 결과 표시) | 컨트롤러에 저장 파이프라인 연결 필요 |
| 5 | 편집 스테이지로 게임 실행 테스트 | `StageInjection.MakeGameSpec` 연동 |

3단계 이후는 코드부터 추가한 뒤 배선 항목을 본 문서에 이어서 정리한다.
