# PuzleBattleGame 변경 이력 (Changelog)

최근 변경만 기록. 아키텍처/구조 정보는 각 참고 문서(`INGAME.md`, `DATA.md`, `SCENE.md`, `UI.md`) 참고.

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
