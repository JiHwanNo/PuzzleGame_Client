# PuzleBattleGame 변경 이력 (Changelog)

최근 변경만 기록. 아키텍처/구조 정보는 각 참고 문서(`INGAME.md`, `DATA.md`, `SCENE.md`, `UI.md`) 참고.

---

## 2026-11-27 — 로비 전장에서 캐릭터 자동 전투 표시

배치 확정 뒤에는 격자가 숲 공터로 바뀌고 아군이 배치한 흙 발판에서 대기한다. 정찰·병사·호위는 걸어가는 캐릭터로 표시한다. 지나가는 적을 향한 사수의 공격 자세, 투사체, 착탄, 체력 감소와 처치가 모델의 교전 기록에 맞춰 이어진다. [전장 사양](LOBBY-DEFENSE.md#배치-캐릭터와-등불유령) · [자동 공격 화면](art/lobby-field/2026-11-27-combat.jpg)

Unity 6000.3.20f1에서 배치 확정, 적의 보행, 발사 자세와 방향, 화살·마법탄, 체력 감소, 처치·골드 지급, 저장본 복귀를 확인했다. 실제 로비 씬을 1배속으로 녹화하고 720×1280, 24fps의 441프레임을 디코딩했다.

## 2026-11-27 — 로비 전장 축소와 방어 카드 배치 모드

- 전장을 100열에서 20열로 줄였다. 10행 200칸과 입·출구를 한눈에 볼 수 있다. 빈 전장의 최단 경로는 19걸음이다.
- 플레이 오른쪽 카드 아이콘이 배치 모드를 연다. 카드와 셀을 고른 뒤 확정하거나 취소하면 기본 화면으로 돌아간다. 인벤토리에서 고른 카드로 바로 배치할 수도 있다.
- 푸른 숲 배경에 잔디 셀과 흙 셀을 구분했다. 전장 아래 공터에 캐릭터를 크게 표시하고, 배치된 셀을 누르면 해당 캐릭터를 볼 수 있다.
- 기존 저장본에서 새 전장 밖과 출구에 놓인 카드는 인벤토리로 돌리고, 나머지 배치와 스테이지·골드·진행 중인 적을 이어받는다. [세부 사양](LOBBY-DEFENSE.md#3-보드와-배치-규칙)

Unity 6000.3.20f1 플레이 모드에서 마지막 셀 선택, 취소와 확정, 인벤토리 진입, 일반 화면의 캐릭터 선택, 전투 후 무료 이동, 흙·잔디 전환과 스테이지 결과 버튼을 검증했다. 100열 저장본의 카드·적 좌표 변환과 재로딩 시 중복 변환 방지도 확인했다. [기본 로비 화면](art/lobby-field/2026-11-27-field.jpg) · [배치 미리보기 화면](art/lobby-field/2026-11-27-placement.jpg)

검증 로그에는 이전 실행과 같은 공용 씬의 누락 스크립트 경고와 Unity Editor 검색 인덱스 예외가 기록됐다. 로비 조작 검증은 끝까지 통과했다.

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
