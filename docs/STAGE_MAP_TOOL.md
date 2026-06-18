# 스테이지 맵 툴 작업 메모

맵툴 재작업을 이어가기 위한 현재 상태와 다음 작업 정리.

---

## 현재 상태

- `StageMapToolController`는 UI 입력을 `StageMapToolState`에 연결하는 컨트롤러다.
- `StageMapTool/Core/`는 유지한다.
  - `StageMapToolState`: 현재 퍼즐 타입, 스테이지, 브러시, StageData 상태
  - `StageMapCellBrush`: 셀에 적용할 브러시 값
  - `StageMapJsonRepository`: StageData JSON 로드/저장
  - `StageMapValidator`: 저장 전 검증
- `Runtime`은 현재 `StageMapToolController`만 유지한다.
- `ToolScene`(에디터 전용 씬, `Assets/01_Scenes/ToolScene.unity`)은 존재한다. SceneEnum에는 미등록.
- 기존 Runtime 뷰/입력/패널 스크립트는 제거되고 `StageMapToolController`로 대체된 상태다.

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

1. 셀 편집 하위 버튼 연결
   - `Normal`, `Close`, `Lock`, `Generator`
   - `StageMapCellBrush.cellType`에 반영
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
