# 스테이지 맵 툴 작업 메모

맵툴 재작업을 이어가기 위한 현재 상태와 다음 작업 정리.
아키텍처·구현 순서는 `STAGE_MAP_TOOL_ARCH.md` 참고.
Unity 에디터 수기 배선(프리팹/버튼/참조 연결)은 `STAGE_MAP_TOOL_SCENE_SETUP.md` 참고.

---

## 현재 상태

- `StageMapToolController`는 UI 입력을 `StageMapToolState`에 연결하는 컨트롤러다.
- `StageMapTool/Core/`는 유지한다.
  - `StageMapToolState`: 현재 퍼즐 타입, 스테이지, 브러시, StageData 상태
  - `StageMapCellBrush`: 셀에 적용할 브러시 값
  - `StageMapJsonRepository`: StageData JSON 로드/저장
  - `StageMapValidator`: 저장 전 검증
- `Runtime`: `StageMapToolController` + 신규 그리드 뷰 `StageMapBoardView`/`StageMapCellView`(ARCH 1단계).
- `ToolScene`(에디터 전용 씬, `Assets/01_Scenes/ToolScene.unity`)은 존재한다. SceneEnum에는 미등록.
- 기존 Runtime 뷰/입력/패널 스크립트는 제거되고 `StageMapToolController`로 대체된 상태다.

### 구현 진행 (ARCH 1단계 — 코드 완료, 씬 배선 대기)
- `StageMapBoardView`: `Build(StageData)`로 격자 생성, `RefreshCell(x,y)` 부분 갱신, `OnCellClicked` 이벤트.
- `StageMapCellView`: 셀 타입 틴트 + 블럭ID 라벨, 클릭 시 (x,y) 전달.
- `StageMapToolController`: 시작/퍼즐타입 변경 시 `Repository.LoadOrCreate` → `Build`, 셀 클릭 → `State.PaintCell` → `RefreshCell`.
- **남은 작업(Unity 에디터)**: 셀 프리팹 제작 + `StageMapBoardView`/`_boardView` 참조 연결. 상세는 본 문서 하단 "씬 배선 체크리스트".

---

## 구현된 UI 흐름

### 퍼즐 타입 선택

- 버튼 콜백: `StageMapToolController.OnClickPuzzleType(string val)`
- 버튼 값:
  - `0`: `ThreeMatch`
  - `1`: `TapMatch`
  - `2`: `Link`
- 새 맵 데이터가 없으면 `PlayerPrefs`의 마지막 선택 타입을 복원한다.
- 맵 파일을 나중에 로드하는 경우는 파일의 퍼즐 타입을 우선 적용해야 한다.

### 편집 모드 선택

- 버튼 콜백: `StageMapToolController.OnClickEditMode(string val)`
- 버튼 값:
  - `0`: 셀 편집
  - `1`: 블럭 편집
  - `2`: 타일 편집
- 선택된 편집 모드에 따라 하위 패널 하나만 활성화한다.

---

## 다음 작업

1. 셀 편집 하위 버튼 연결 — **코드 완료, 씬 배선 대기**
   - `Normal`, `Close`, `Lock`, `Generator`
   - `StageMapToolController.OnClickCellType(string val)` → `StageMapCellBrush.cellType` 반영
   - 버튼 인덱스: `0`=Normal, `1`=Close, `2`=Lock, `3`=Generator (enum 값과 다름, `BrushCellTypes` 매핑)
   - 배선 절차: `STAGE_MAP_TOOL_SCENE_SETUP.md` 2단계
2. 블럭 편집 하위 버튼 연결
   - 현재 퍼즐 타입 Rule에서 block 목록 로드
   - 선택한 `blockId`를 `StageMapCellBrush.blockId`에 반영
3. 타일 편집 하위 버튼 연결
   - 우선 `panelId` 편집으로 시작
4. 맵 셀 클릭 적용
   - 현재 브러시를 선택 셀의 `CellData`에 반영
5. 마지막 단계에서 저장/불러오기/검증/테스트 연결

---

## UI 공통

- `UIButton`은 클릭 시 짧은 DOTween 스케일 피드백을 기본 제공한다.
- `UIButtonGroup`은 버튼 배열과 공통 normal/selected 스프라이트만 관리한다.
- 버튼 콜백은 `_root`, `_callbackName`, `_callbackValue`로 연결한다.

---

## 씬 배선 체크리스트 (ARCH 1단계)

1. **셀 프리팹** 생성: `Image`(배경) + `Button` + (선택)`TMP_Text`(라벨) 자식 구성, 루트에 `StageMapCellView` 부착 후 `_background`/`_button`/`_label` 연결. 권장 크기 64×64, 앵커/피벗 좌하단(0,0).
2. **셀 루트**: 보드 영역에 빈 `RectTransform`(예: `CellRoot`) 추가. 앵커/피벗 좌하단.
3. **보드 뷰**: 빈 오브젝트에 `StageMapBoardView` 부착 → `_cellPrefab`(1번), `_cellRoot`(2번), `_cellSize`(셀 크기) 연결.
4. **컨트롤러 연결**: `StageMapToolController._boardView`에 3번 보드 뷰 연결.
5. 신규 `.cs` 3종은 Unity 임포트 시 `.meta` 자동 생성됨.

검증: 플레이 시 8×8 격자가 그려지고(최상단 행 Generator=녹색), 셀 클릭 시 해당 칸이 현재 브러시(기본 Normal) 색으로 바뀌면 1단계 동작 확인.
