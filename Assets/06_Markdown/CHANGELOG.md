# PuzleBattleGame 변경 이력 (Changelog)

최근 변경만 기록. 아키텍처/구조 정보는 각 참고 문서(`INGAME.md`, `DATA.md`, `SCENE.md`, `UI.md`) 참고.

---

## 2026-06-18 — Resources 폴더 구조 타입별 정리 (Prefab/Texture/Animation)

### 폴더 구조
- `Assets/Resources/`를 에셋 타입별로 정리: `Prefab/`, `Texture/`, `Animation/`(+기존 `Font/`, `Stage/`).
- 모든 프리팹을 `Resources/Prefab/`로 통일하고 `Assets/03_Prefab/` 폴더 제거(`CellMaker`, `Button` 포함, GUID 보존 이동).
- **非Addressable 텍스처**(UI `button_Image`/`panel_Image` 스프라이트, `Tool/DarkPlus`)를 `04_Resources/` → `Resources/Texture/`로 이동. 모두 GUID 참조라 코드 변경 없음.
- Addressable 텍스처(Block_100-*, hexagonCell, squareCell)는 `04_Resources/Ingame/`에 유지.

> 규칙: Addressable 미등록 + 빌드 포함 텍스처는 `Resources/Texture/`, Addressable 소스는 `04_Resources/`.

---

## 2026-06-18 — 프리팹 로드 방식 Addressables → Resources 전환

### AssetManager
- 프리팹 전용 Resources API 추가: `LoadResource<T>`, `LoadGameObjectFromResources`, `LoadGameObjectFromResourcesAsync`(`Resources.LoadAsync` 기반), `_resourcePacket` 캐시.
- 데이터 에셋(스프라이트·TextAsset)은 기존 Addressables 경로 유지. 기존 `LoadGameObject(Async)`(Addressable)는 범용 유틸로 보존(현재 미사용).

### 에셋 이동
- `CellPrefab`/`BlockPrefab`/`PopupReady` 프리팹을 `Assets/Resources/Prefab/` 하위로 이동(GUID 보존), 해당 Addressable 그룹 엔트리 제거(중복 등록 방지).

### 호출부
- `PuzzleBoardView`: 셀/블럭 프리팹을 `LoadResource<GameObject>`로 로드(`Prefab/Puzzle/...`).
- `PopupController`: 팝업을 `LoadGameObjectFromResourcesAsync`로 로드(`Prefab/UI/Popup/{팝업이름}`).

---

## 2026-04-30 — UI 버튼 및 맵툴 재작업 기반

### UI
- **UIButton**: 클릭 시 DOTween 스케일 피드백 추가.
- **UIButton**: 버튼 이미지 스프라이트 교체용 `SetSprite(Sprite)` 추가.
- **UIButtonGroup**: 인스펙터 캐싱된 버튼 배열에 normal/selected 스프라이트 적용.

### StageMapTool
- **StageMapToolController**: 퍼즐 타입 선택을 `StageMapToolState`와 연결.
- 마지막 퍼즐 타입은 새 맵 데이터가 없을 때만 `PlayerPrefs`에서 복원.
- **StageMapToolController**: 셀/블럭/타일 편집 모드 선택과 하위 패널 전환 추가.
- 기존 Runtime 뷰/입력/패널 스크립트와 `ToolScene` 제거, `Core` 모듈은 유지.
- 다음 작업 메모는 `STAGE_MAP_TOOL.md`에 정리.

---

## 2026-04-13 — 버그 수정 및 안전성 강화

### Null 안전성
- **LinkPuzzleBoard.Input()**: 링크 경로 마지막 셀 null 체크 + `Log()` 에러 출력
- **StageInjection.MakeGameSpec()**: JSON 파싱 결과 null 검증 (실패 시 `false` 반환)
- **PuzzleGameController.SaveReplay()**: `GetGameSpec()` null 체크
- **ThreeMatchPuzzleBoard.ProcessSwapInput()**: 스왑 대상 블럭 null 시 `Log()` 출력
- **AssetManager.LoadAsset\<T\>()**: 빈 주소 시 `LogError` 출력
- **ReplayStorage.Load()**: 파싱 결과/입력 기록/주소 누락 검증

### 최적화
- **FetchActions()**: 리스트 복사 → 참조 스왑으로 GC 제거 (3개 보드 전체)
- **AssetManager.ReleaseAll()**: LINQ → `_releaseBuffer` 재사용 루프
- **HasEmptyCell()**: `.Any()` → 수동 foreach
- **LinkPuzzleBoard**: `.Last()` → `[Count - 1]` 인덱스 접근
- **ExecuteBatchMovement**: Dictionary → `_batchActions`/`_batchViews` 병렬 리스트
- `using System.Linq` 제거 (LinkPuzzleBoard, ThreeMatchPuzzleBoard, AssetManager)

### 생명주기
- **PuzzleBoardView.OnDestroy()**: `StopAllCoroutines()` 추가

---

## 이전 이력 (요약)

- **인프라**: SharedScene 영구 상주, 씬 전환 파이프라인, 매니저 싱글톤 시스템
- **에셋**: AssetManager (Addressables 래핑, 캐싱, MarkPersistent/ReleaseAll), PoolManager
- **도메인 UI**: DomainManager 팝업/탭 스택, PopupBase/TabBase 생명주기
- **퍼즐 Model**: IPuzzleBoard 3종 (ThreeMatch/Link/TapMatch), 블럭 팩토리, 콤보/피버, 자동 셔플
- **퍼즐 View**: BatchMovement 처리 순서 보장, 애니메이션 (0.075s), LineRenderer 링크 경로
- **리플레이**: InputRecord/InputEndRecord 프레임 기록, ReplayController 축소 배치 재생
- **최적화**: LINQ 전면 제거, FloodFill/FindMatches 버퍼 재사용, ContactFilter2D 캐싱, LineRenderer 변경 감지
- **버그 수정**: ExecuteBatchMovement 블럭 미씽, GetPointerPosition null 크래시, ReplayStorage 모바일 경로, Main.cs 이벤트 릭, StageInjection 반환값 등
