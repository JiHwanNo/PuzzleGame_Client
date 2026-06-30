# PuzleBattleGame 변경 이력 (Changelog)

최근 변경만 기록. 아키텍처/구조 정보는 각 참고 문서(`INGAME.md`, `DATA.md`, `SCENE.md`, `UI.md`) 참고.

---

## 2026-06-30 — 기믹을 "효과 동사(verb) + 속성 데이터"로 재설계 (클래스 명사 → 동사 라이브러리)

### 설계 전환
- 기믹은 더 이상 클래스(명사)가 아니라 **블럭 속성(HP·피격소스) + 효과 동사(verb)의 데이터 조합**으로 표현.
- 블럭이 무엇에 파괴되는지(`damagedBy`)와 무엇을 하는지(`effects`)를 **데이터로 분리**. 장애물(과자·벽돌)은 클래스 없이 데이터만으로 성립.

### 삭제
- `IGimmick`/`IGimmickHost`/`GimmickBase`/`GimmickFactory`/`GimmickUtil` + `BombGimmick`/`LineBombGimmick`/`RainbowBombGimmick`/`CookieGimmick` 전부 제거. `GimmickType` enum 제거.

### 신설 (`Module/Effect/`)
- `IBlockEffect`(동사 계약: `Trigger` + `Apply`) + `EffectContext`(struct).
- 동사: `DestroyRadiusEffect`(원형) / `DestroyLineEffect`(라인) / `DestroySameColorEffect`(무지개·스왑 소비).
- `EffectFactory`(`EffectData` → 동사) + `BlockDamage`(피격소스 게이팅 → HP 감소 → 0이면 파괴/연쇄, 구 `GimmickUtil` 대체).
- `PuzzleDefine.cs`: `DamageSource`(Flags) / `EffectTrigger` enum + `EffectData` class 신설, `LineDirection` 유지.

### 데이터/블럭
- `BlockData`: `gimmickIds` 제거 → `damagedBy`(List<string>) + `effects`(List<EffectData>) 추가. `life`=HP로 사용.
- `Block`: HP/`TakeDamage(source)` + `Effects`(동사 목록) 보유. `FireDestroyed`/`FireSwapped`는 트리거별 동사만 발화.
- `ThreeMatchPuzzleBoard`: 인접 매치 통지를 `BlockDamage.Damage(NeighborMatch)`로 전환(과자가 HP 데미지로 처리, 일반 블럭은 면역).
- `ThreeMatchRule.json`: Bomb 블럭을 `effects:[{OnDestroyed, DestroyRadius, 2}]`로 마이그레이션.

### 코드 리뷰 반영
- **매치 경로 HP 배선**: `ProcessMatching`이 `TakeDamage(Match)`를 거치도록 변경 → `DamageSource.Match`가 실코드화, 다중 HP 매치 블럭 지원. 파괴가 없으면 낙하 생략(무한 재매치 방지). life=1은 기존과 동일.
- **trigger 데이터화**: `EffectFactory`가 `data.trigger`를 파싱해 동사에 주입(미지정 시 동사별 기본값). "스왑 시 폭발" 같은 조합 가능.
- **한 스텝 = 칸당 최대 1대**: `_damageBuffer`로 추적, 폭탄 Splash+인접 매치 중복 데미지 차단.
- **무지개 자기 소비 분리**: `BlockDamage.Destroy`(게이팅 없는 무조건 제거) 추가 → Splash 면역 데이터여도 스왑 소비 후 고착되지 않음.
- **`PuzzleCell.IsPlayable`** 헬퍼 추가(중복 셀 판정 정리, `BlockDamage` 적용). `BlockDamage.Damage`는 파괴 여부(bool) 반환.

---

## 2026-06-26 — 기믹 4종 추가 (라인/무지개 폭탄, 과자 장애물, 폭탄전용 장애물 규약)

### 기믹
- `GimmickType`에 `LineBomb`(2)/`Rainbow`(3)/`Cookie`(4) 추가, `LineDirection` enum 신설(`PuzzleDefine.cs`).
- `IGimmick`/`GimmickBase`에 `OnNeighborMatched(board, myPos)` 훅 추가. `Block`에 `FireSwapped`/`FireNeighborMatched` 발화 메서드 추가.
- `LineBombGimmick`(행/열/십자 라인 파괴), `RainbowBombGimmick`(스왑 시 동색 전체 파괴), `CookieGimmick`(인접 매치 시 파괴) 작성 + `GimmickFactory` 분기.

### 보드 배선 (`ThreeMatchPuzzleBoard`)
- `ProcessSwapInput`: 물리 스왑 "전"에 `FireSwapped` 호출 → 소비 시(무지개) 매치 검사/복구 생략하고 `Falling` 진입.
- `ProcessMatching`: 매치 칸 파괴 후 `FireNeighborMatchedAround`로 직교 인접 블럭에 통지(과자) — 보드 (x,y) 스캔으로 결정론적.

### 규약
- **금이 간 벽돌(폭탄 전용 장애물)**: 별도 기믹 없이 데이터만으로 구성(조작 불가 `inputType` + 고유 `blockId`). 매칭 비대상이라 폭발 `DestroyBlock`에 의해서만 파괴 → 상세: `INGAME_GIMMICK.md`, `DATA.md`.

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
