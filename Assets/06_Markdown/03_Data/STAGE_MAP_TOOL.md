# 스테이지 맵 툴 작업 메모

맵툴 재작업을 이어가기 위한 현재 상태와 다음 작업 정리.
아키텍처·구현 순서는 `STAGE_MAP_TOOL_ARCH.md` 참고.
Unity 에디터 수기 배선(프리팹/버튼/참조 연결)은 `STAGE_MAP_TOOL_SCENE_SETUP.md` 참고.

> **편집 모델 전환(2026-06-19)**: 기존 "브러시로 칠하기" → **"빈 격자에서 `+`로 셀을 찍고, 셀을 선택해 인스펙터로 편집"** 모델로 재설계.
> 기획자가 맵을 직접 찍는 흐름이 목표. 기존 브러시 기반 서술이 남아 있던 문서는 이 모델 기준으로 갱신함.

---

## 편집 모델 (선택 + 인스펙터)

1. 맵을 불러오지 않으면 **모든 칸이 빈 `+`** 상태로 시작한다(최대 9×9).
2. `+` 칸을 클릭하면 그 자리에 **셀이 생성**된다(기본 `Normal`).
3. 생성된 셀을 클릭하면 **선택(하이라이트)** 되고, 그 셀을 편집하는 **인스펙터 버튼**(블럭/판넬/셀 상태변화/삭제)이 노출된다. *(Phase 2)*
4. 셀 삭제 시 다시 `+` 빈 칸으로 돌아간다.

### 확정 설계 결정
- **빈 칸 `+` ≡ `CellType.Close`**. 편집 중에는 `StageData.cells`에서 부재(존재=셀)로 다룬다. 로드 시 `Close`/부재는 다시 `+`로 환원.
- **저장 시 "내용 기준 정규화(trim)"** — 실제 찍은 셀들의 **바운딩 박스로 잘라내고 좌표를 (0,0)부터 재매핑**, `stage_width/height`를 내용 크기로 설정, 박스 내부의 빈 칸은 `Close`로 채운다. → 인게임 `PuzzleBoardView`가 `stage_width×stage_height`를 중앙 정렬하므로 **기획자가 격자 어디에 찍든 인게임 출력은 항상 화면 중앙**에 온다(빈 테두리로 인한 쏠림 방지).
- **보드 모양**: `PuzzleType.Link`이면 **헥사(Even-Q Flat-Top)**, 그 외는 **사각**. 헥사는 짝수 열을 반 칸 아래로 배치(인게임 `PuzzleBoardView`와 동일 규칙).
- 기본/최대 신규 맵 크기 **9×9**(유지).
- **툴 격자 중앙정렬**: `BoardView.GetCellPosition`이 격자를 CellRoot 원점 기준으로 중앙 배치(크기 무관 자동). `CellRoot`는 캔버스 중앙 앵커(0.5,0.5), `anchoredPosition=(0,-160)`.

---

## 핵심 모듈

- `StageMapTool/Core/`
  - `StageMapToolState`: 퍼즐 타입/스테이지/StageData/브러시 상태. **`CreateCell(x,y)`**(빈 칸→Normal, Close면 되살림), **`RemoveCell(x,y)`**(→빈 칸), `GetCell`, `PaintCell`.
  - `StageMapCellBrush`: 셀에 적용할 값(인스펙터 편집 값으로 재활용 예정).
  - `StageMapJsonRepository`: StageData JSON 로드/저장. **`CreateEmptyStage`**(빈 9×9), 기본 크기 9×9.
  - `StageMapValidator`: 저장 전 검증.
- `StageMapTool/Runtime/`
  - `StageMapBoardView`: **`Build(StageData, hexLayout)`**로 빈 칸 포함 전체 격자 생성, `RefreshCell(x,y)`(선택 반영), **`Select`/`ClearSelection`**, 헥사 오프셋·셀 간격(`_cellSpacing`). `OnCellClicked` 이벤트.
  - `StageMapCellView`: **3-state**(`+`빈칸 / 생성됨 / 선택). `Bind(x,y,cell,onClicked)`, `Refresh(cell,selected)`, `_selectionOutline` 하이라이트.
  - `StageMapToolController`: **시작/퍼즐타입 변경 시 `Repository.CreateEmptyStage`로 빈 9×9 맵 생성**(`BuildNewStage`). 기존 스테이지 편집은 별도 불러오기(Phase 3)에서 처리 — 자동 로드 안 함. 셀 클릭 → 빈 칸이면 `CreateCell`, 그 후 `Select`. `IsHexLayout()`로 Link 헥사 적용.
  - ⚠️ `Resources/Stage/{모드}/Stage_NNN.json`이 모드별로 존재하므로, 시작 시 `LoadOrCreate`를 쓰면 기존 스테이지(셀·블럭)가 로드된다. 맵툴은 만드는 도구이므로 **시작은 항상 빈 맵**(`CreateEmptyStage`).
- `ToolScene`(에디터 전용, `Assets/01_Scenes/ToolScene.unity`). SceneEnum 미등록.

---

## 진행 상태 (재설계 3 Phase)

### Phase 1 — 기반 전환 ✅ 완료 (2026-06-19)
- 코드: 위 핵심 모듈 전부 신모델로 전환(3-state 셀뷰, 빈 격자 빌드, 생성+선택, 헥사 Link, 9×9, 셀 간격).
- 씬/프리팹 배선: `MapCell` 프리팹(`Assets/01_Scenes/ToolPrefab/MapCell.prefab`) + `Selection` 하이라이트, `Canvas/CellRoot` + `BoardBackground`(어두운 판), `BoardView` 참조, 컨트롤러 `_boardView` 연결.
- 검증(execute_code): 빈 9×9=81 전부 `+`, `+`클릭→Normal, 선택 하이라이트, Link 헥사 오프셋 모두 통과.

### Phase 2 — 인스펙터 패널 (셀 상태 ✅ 완료 / 블럭·타일 미착수)
- **셀 선택 시 인스펙터 노출** — 선택 없으면 모든 편집 패널 숨김(`RefreshInspector`). 컨트롤러에 선택 상태(`_hasSelection`/`_selectedX/Y`) 추가.
- **셀 상태변화 패널(완료)**: `EditButtonPanelRoot/CellEditPanel`에 버튼 4개 — `일반`(OnClickCellType 0)/`잠금`(1)/`생성기`(2)/`삭제`(OnClickDeleteCell). `OnClickCellType`은 **선택된 셀**에 `state.SetCellType` 적용, `삭제`는 `state.RemoveCell`→`+`. `UIButtonGroup`(3타입) + `_cellTypeButtonGroup` 연결. 셀 선택 시 그 셀 타입으로 버튼 강조 동기화(`SyncCellTypeButtons`).
  - `BrushCellTypes`={Normal, Lock, Generator}(Close=빈칸은 삭제 버튼이 담당, 인덱스 0~2).
  - 검증: `state.SetCellType`/`RemoveCell` 동작 + 버튼 4개 렌더(일반/잠금/생성기/삭제) 확인.
- **블럭 편집 패널(`BlockEditPanel`) — ✅ 완료(Phase 2.5)**: `StageMapRuleProvider.LoadBlocks(ruleAddress)`(`AssetManager.LoadAsset<TextAsset>`→`JsonUtility.FromJson<GameRuleContainer>`→`.blocks`)로 현재 룰 블럭을 로드 → `BuildBlockPalette`가 `_blockButtonPrefab`(Button 프리팹) 인스턴스를 블럭 수만큼 생성, 각 버튼에 블럭 젬 스프라이트(`SetIconSprite`) + 콜백(`SetCallback(this,"OnClickBlock",blockId)`). 버튼 클릭 → `OnClickBlock(blockId)` → 선택 셀 `state.SetBlockId` → RefreshCell(젬 표시). Awake/퍼즐타입변경 시 재생성. `BlockEditPanel`에 `GridLayoutGroup`(64×64). `UIButton`에 `SetCallback`/`SetIconSprite` 추가. 검증: ThreeMatchRule 6블럭 → 버튼 6개, 100-1~5 젬 표시(200-1 폭탄은 Block_200-1 스프라이트 부재로 빈 버튼).
  - **가로 스크롤 팔레트**: `BlockEditPanel` = `ScrollRect`(가로 전용, Clamped) + `RectMask2D`(뷰포트=자신). 버튼은 자식 `Content`(`HorizontalLayoutGroup`(childControl off) + `ContentSizeFitter` 가로 PreferredSize)에 생성 → 한 줄 가로 정렬, 폭이 뷰포트(790) 넘으면 잘리고 가로 스크롤. 컨트롤러 `_blockPaletteContent`(=Content)에 생성.
  - **버튼 규격 = 스프라이트 크기 영향**: 고정값 아님. `BuildBlockPalette`가 행 높이(`_blockPaletteContent.rect.height`)에 맞추고 **폭 = 행높이 × 스프라이트 종횡비**로 버튼 `sizeDelta` + `LayoutElement` 설정(블럭 젬은 256×256 정사각 → 행높이 75면 75×75). childControlWidth=false라 버튼 자체 RectTransform 크기가 레이아웃 폭이 됨.
- **타일 편집 패널(`TileEditPanel`) — 미착수**: `panel_id` 편집. 현재 빈 플레이스홀더.

### Phase 3 — 저장/로드/검증 (미착수)
- **저장 시 내용 기준 정규화(trim)**: 찍은 셀 바운딩 박스로 잘라 (0,0) 재매핑 + `stage_width/height` 갱신 + 내부 빈 칸 `Close` → 인게임 중앙 출력 보장.
- `Validator.Validate` 연결, StageId 선택/Save/Load UI.
- 편집 스테이지로 게임 실행 테스트(`StageInjection.MakeGameSpec`)로 **인게임 중앙정렬 확인**.
- **불러오기(Load)는 `StageMapJsonRepository.LoadOrCreate`/`CreateDefaultStage`를 재사용** — 현재 편집 모델 전환으로 호출처가 빠졌지만(빈 맵 시작) Phase 3 Load UI에서 다시 연결할 예정이므로 **삭제 금지**.

---

## 코드 리뷰 반영 (2026-06-24, 커밋 전 검수)

**수정 완료**
- `StageMapToolState.SetCellType`: 생성기 → 일반/잠금 전환 시 `generator_block_ids`를 `Clear()`. 미처리 시 stale 생성기 ID가 저장 JSON에 남아 인게임에서 일반 셀이 블럭을 생성하는 오동작 발생.
- `StageMapBoardView.FindCell`: `_stageData`/`cells` null 가드 복원. `Build`에서 가드가 빠져 cells 없는 데이터로 호출 시 NRE(보드 빈 화면) 재노출 위험.
- `AssetManager.LoadAsset`: location 선검사에서 `typeof(T)` 필터 제거(주소 등록 여부만 검사). 카탈로그 `ResourceType`이 T와 정확히 일치하지 않는(예: Sprite로 임포트된 Texture) 유효 에셋이 누락되던 문제 방지. 프로젝트 전역 동기 로드 경로.

**알려진 사항(Phase 3 클린업 후보)**
- **헥사 오프셋 중복**: `IsHexLayout()`(PuzzleType==Link 하드코딩) + `BoardView.GetCellPosition` Even-Q 수식이 인게임 `PuzzleBoardView`와 복제됨. 인게임 규칙 변경 시 툴 프리뷰가 조용히 어긋날 수 있음 → 공용화 검토.
- **`"Block_"+id` 매직 프리픽스**가 `StageMapCellView`/`StageMapToolController`/`PuzzleBlockView` 3곳에 중복 → 공용 상수/헬퍼로 단일화 검토.
- **`AssetManager.LoadAsset` 더블 location 조회**: 성공 경로에서 선검사 + `LoadAssetAsync` 내부 조회로 2회 해석(경미한 동기 stall). InvalidKeyException try/catch 또는 해석된 `IResourceLocation` 재사용으로 개선 가능.
- **데드코드**: `StageMapToolState.PaintCell` 등 브러시 적용 경로는 편집 모델 전환 잔재(호출처 없음). 정리 시 `Brush` 플러밍과 함께 검토.

---

## 구현된 UI 흐름 (기연결)

### 퍼즐 타입 선택
- 콜백: `OnClickPuzzleType(string val)` — `0`=ThreeMatch, `1`=TapMatch, `2`=Link.
- 새 맵이 없으면 `PlayerPrefs` 마지막 선택 복원. 파일 로드 시 파일의 퍼즐 타입 우선.
- **Link 선택 시 보드가 헥사 배치로 그려진다.**

### 편집 모드 선택
- 콜백: `OnClickEditMode(string val)` — `0`=셀, `1`=블럭, `2`=타일. (인스펙터 패널 구성은 Phase 2)

---

## 뷰 사양 (Phase 1 적용값)

- 셀 프리팹 `MapCell`: 64×64, 앵커/피벗 좌하단. `Image`(배경) + `Button` + `Label`(TMP, `+` 표시, 44pt Bold) + `Selection`(노랑 반투명 하이라이트, 기본 비활성) + `BlockIcon`(블럭 스프라이트, inset 6px, 비율유지, 기본 비활성).
- **블럭 표시**: 셀에 `block_id`가 있으면 ID 텍스트 대신 **블럭 스프라이트**를 띄운다. 로드는 `AssetManager.Instance.LoadAsset<Sprite>("Block_{block_id}")`(Addressable, 동기). 로드 실패 시 ID 텍스트로 폴백. (블럭 ID 형식 `100-N`: 1빨강/2파랑/3노랑/4초록/5보라)
- 빈 칸 배경 불투명 타일색 + 밝은 `+` 라벨 / 생성 셀은 타입 틴트 + (블럭 있으면) 스프라이트.
- 셀 색: `Normal`=흰색, `Lock`=파랑, `Generator`=녹색, `Close`(빈칸)=어두운 타일.
- `BoardView._cellSpacing=6`(타일 간격), `CellRoot/BoardBackground`(어두운 보드 판).

## UI 공통
- `UIButton`은 클릭 시 짧은 DOTween 스케일 피드백. 콜백은 `_root`/`_callbackName`/`_callbackValue`.
- `UIButtonGroup`은 버튼 배열 + 공통 normal/selected 스프라이트 관리.
