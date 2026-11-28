# PuzleBattleGame 아키텍처 참고 문서

게임 전체 설계 방향과 개발 원칙을 정의한다.
코딩 규칙은 `../AGENTS.md`, 영역별 구현 상세는 각 영역 MD 참고.

## 1. 설계 범위

로비에서 보유 카드와 전장 배치를 관리한다. 퍼즐 컨트롤러는 씬 진입 전에 준비된 `GameSpec`과 선택적인 상대 기록을 받는다. 플레이 규칙은 [게임 사양서](SPEC.md), 기능별 구현 여부는 [구현 현황](IMPLEMENTATION.md)에서 확인한다.

| 영역 | 기술 경계 | 플레이 규칙 |
|---|---|---|
| 로비·카드 | 보유 상태, 배치, 타입별 능력치와 개별 카드의 보너스를 로비에서 관리한다. | [로비](LOBBY-DEFENSE.md) · [카드](LOBBY-CARDS.md) |
| 퍼즐 대전 | 로컬 대전에서는 호환 리플레이를 골라 같은 규칙·스테이지·시드로 두 보드를 실행한다. 결정론적 재현은 §5를 따른다. | [인게임](INGAME-BATTLE.md) |
| 서버 연동 기획 | 리플레이 저장·전달과 랭킹 집계는 서버 역할로 설계한다. 스테이지 진행·보상 내역은 보드 모델과 분리하고 스테이지별 보상 규칙은 데이터로 관리한다. | [배틀 리그](INGAME-BATTLE.md#배틀-리그) · [로비 프로필](LOBBY-DEFENSE.md#로비-화면과-탭) |

서버·유저 데이터의 구현 경계는 [서버 문서](SERVER.md)를 따른다. 번호로 불러오는 퍼즐 스테이지와 대전 상대 기록은 서로 다른 경로에서 읽는다.

## 2. 핵심 원칙

### MVC 엄격 분리
- **Model**의 보드 판정·점수·난수 계산은 순수 C#으로 실행한다. 같은 `PuzzleCore/Module/`에 있는 [ReplayStorage](../Assets/02_Scripts/PuzzleCore/Module/ReplayStorage.cs)는 저장 경로와 JSON 직렬화에 Unity API를 사용하는 파일 입출력 코드다.
- **View** (`PuzzleCore/View/`): Model의 `BoardViewAction` 큐를 수신, 시각 연출만.
- **Controller** (`PuzzleCore/Controller/`): 유저 입력 감지/큐 저장, 프레임 단위 Model 호출.

### 데이터 기반 생성
- 게임 규칙(Rule)·스테이지(Stage)는 JSON → `GameSpec`으로 병합 후 Model에 주입. 하드코딩 금지.
- 상세: `DATA.md`

### 결정론적 리플레이
- 동일 시드 + 동일 입력 → 동일 결과를 보장하는 게임 루프.
- 상세: 아래 §5, `INGAME.md`, `DATA.md`

### 인터페이스 기반 확장
- `IPuzzleBoard`로 퍼즐 모드 교체(ThreeMatch/Link/TapMatch).
- **블럭은 단일 `Block` 클래스 + 기믹 컴포지션**으로 확장한다(서브클래스 상속 폐기).
  - 조작 가능 여부(스왑/링크/터치)는 데이터(`inputType` 플래그)로 판정 → 보드가 게이팅.
  - 특수 행동은 `IGimmick`을 `IGimmickHost`(`Block`)에 부착해 부여. `GimmickFactory`가 `GimmickType` → 기믹 생성, `GimmickUtil`이 파괴·연쇄 공통 처리.
- 상세: `INGAME.md` "블럭 아키텍처 (기믹 컴포지션)"

## 3. 런타임 루프

### 프레임 모델
- **`Update`**: Controller가 유저 입력 감지 → `board.Input()` 큐에 기록 (시각 시간).
- **`FixedUpdate`**: 메인 컨트롤러가 본인 보드와 상대 리플레이를 같은 틱에 진행한다. 좌표/종료 입력을 수신 순서로 소비하고 타이머, 매칭, 낙하, 종료를 판정한 뒤 프레임 증가. 첫 판정 틱은 0번이며 보드 상태 변경은 이 경로에서만 실행한다.
- Model의 모든 시간 의존 로직(타이머, 콤보 유지, 피버 지속)은 **논리 프레임 기반**이어야 한다. 실시간(`Time.deltaTime`) 사용 금지.

### 입력 게이팅
- `PuzzleGameController.Update()`는 `boardView.IsAnimating` 동안 누르고 있는 포인터의 블럭 감지를 건너뛴다. 포인터를 떼면 연출 중에도 `InputEnd()`를 전달하고 마지막 좌표를 비운다.

### 보드 상태머신

`Waiting → Matching → Falling → Filling → Waiting`을 반복하고 모드별 종료 조건을 충족하면 `Finish`로 전이한다. 전이 구현은 [보드 상태 머신](INGAME.md#board-상태-머신-fixedupdate-내부-updatestate), 모드별 판정은 [인게임 사양](INGAME-BATTLE.md)을 따른다.

## 4. Model-View 계약

### 큐 기반 단방향 통신
- `PuzzleBoardView`는 `IPuzzleBoard`를 참조해 초기 보드를 그린다. 이후 보드가 생성·이동·파괴 결과를 `BoardViewAction`으로 쌓으면 뷰가 `FetchActions()`로 가져와 연출한다.

### FetchActions() 소유권 이전
- 참조 스왑 패턴(`var res = _views; _views = new List<>(); return res;`)으로 복사 없이 반환.
- 반환 리스트는 호출자 소유 — 코루틴 yield 중에도 변경 없음 보장.

### 정렬 규약
- `frame` + `orderIndex` 기준 `List.Sort()` 정렬(**불안정 정렬**).
- 같은 (frame, orderIndex) 내부 순서는 미보장 → 타입별 처리 순서를 **소비 측에서 명시적으로 분리**해야 함.
- 상세: `INGAME-VIEW-AND-BATTLE.md` "ExecuteBatchMovement 처리 순서 규칙"

## 5. 결정론 보장

리플레이 기능의 근간. **어기면 동일 시드로도 다른 결과가 나와 리플레이가 깨진다.**

### Model(`PuzzleCore/Module/`)에서 금지된 비결정론 소스
| 소스 | 이유 | 대체 |
|------|------|------|
| `UnityEngine.Random`, `Random.Range` | 전역 상태, 시드 격리 불가 | `PuzzleRandom`(GameSpec.randomSeed 주입) |
| 보드에서 `System.Random` 직접 생성 | 리플레이가 재현할 난수 흐름에서 벗어남 | `PuzzleRandom`이 내부 `System.Random`을 기록된 시드로 초기화 |
| `Time.deltaTime`, `Time.time`, `Time.unscaledTime` | 실시간 기반 → 프레임 레이트 의존 | 논리 프레임 카운터 |
| `DateTime.Now`, `DateTime.UtcNow` | 실행 시점 의존 | 논리 프레임 |
| `Dictionary<>` 순회 순서 의존 | .NET 버전에 따라 순서 상이 | `List<>` + 명시적 정렬 |
| `HashSet<>` 순회 순서 의존 | 동일 | `List<>` + 명시적 정렬 |
| `float` 직접 비교 (`==`) | 부동소수점 오차 | 정수 프레임 / `Mathf.Approximately` |

### 허용 영역 (리플레이에 무관)
- **View 레이어**: 애니메이션 Tween, 파티클, `Time.deltaTime` 기반 연출 — 자유 사용.
- **시드 생성 자체**: `StageInjection`에서 `new System.Random().Next()`로 초기 시드만 뽑음 → 이 값이 `GameSpec.randomSeed`로 저장되고, 게임 내부는 `PuzzleRandom`만 사용.
- **UI 연출 / 메타 정보**: `DateTime.Now`로 리플레이 파일명 생성, `Random.Range`로 팝업 선택 랜덤 Rule 로드 등.

### 결정론 검증 방법
- 동일 `ReplayData`로 재생 → Board의 최종 상태(점수, 남은 블럭)가 원본과 일치해야 함.
- 깨졌다면 Model 레이어에 허용되지 않은 비결정론 소스가 혼입된 것.

## 6. 데이터 경계

### GameSpec 단방향 주입
- `StageInjection.MakeGameSpec()` → `board.Initialize(gameSpec)` 한 번만.
- Model은 `GameSpec` 참조만 읽고, GameSpec을 역방향으로 수정하지 않는다.
- 리플레이 재생 시 동일 `GameSpec`(동일 시드) 주입 → 재현.

### struct vs class 전략
- **struct = 값 의미론**: 불변 좌표/수치 데이터 (`RuleData`, `ObjectiveData`, `InputRecord`, `InputEndRecord`, `GridPos`).
- **class = nullable 컨테이너**: JSON 파싱 실패 감지가 필요한 최상위 레벨 (`GameSpec`, `GameRuleContainer`, `StageData`, `CellData`, `BlockData`, `ReplayData`).
- struct는 `== null` 비교 불가(CS0019) — 파싱 실패 감지는 **class 컨테이너 null 체크**로 수행.
- 상세: `DATA.md` "데이터 타입 주의"

## 7. 성능 원칙

> 초안. 목표 프레임 레이트·타겟 플랫폼 확정 및 세부 조정은 추후 보강.

### 게임 루프 내 GC 할당 금지
- Model의 `FixedUpdate` 경로, View의 `ProcessActionQueue` 경로에서는 프레임당 힙 할당이 발생하지 않아야 한다.
- LINQ(`.Where`, `.Select`, `.Any`, `.Last`, `.ToList`) 금지 — Enumerator/람다 할당 발생.
- `foreach (Dictionary)` 금지 — Enumerator boxing. `List` 병렬 인덱스 사용.
- 매치/플러드필/뷰액션 같은 반복 작업은 **재사용 버퍼**(예: `_connectedBuffer`, `_batchActions`)로 수행.
- 코루틴은 `yield return null` 또는 캐시된 `WaitForSeconds` 외 새 `yield` 객체 생성 금지.

### 에셋/인스턴스는 풀로 관리
- 블럭·셀처럼 반복 생성되는 오브젝트는 **`PoolManager` 경유**. `Instantiate`/`Destroy` 직접 호출 금지.
- Addressables 로드는 반드시 `AssetManager` 경유 — 중복 로드 방지 + `MarkPersistent` 통제.

### 결정론 제약 내에서만 최적화
- 성능 개선이 §5 결정론 규칙과 충돌하면 **결정론이 우선**.
- 예: `HashSet` 순회가 빠르다고 Model 로직에 쓰지 않는다. 정렬된 `List`로 대체.
- 불안정 정렬 `List.Sort()`를 써서 얻는 속도 이득은 유지하되, **처리 순서 분리**(타입별 루프)로 결정성 보완.

### View는 비싸도 되지만 예측 가능해야 한다
- View 애니메이션/Tween은 GC 할당 허용 (비결정론 영역).
- 단, **애니메이션 지속 시간은 짧게** (현재 0.075s 기준) — 긴 애니메이션은 `boardView.IsAnimating` 게이팅으로 입력을 오래 막아 체감 반응성 저하.

### 측정 없이 최적화하지 않는다
- 새 최적화는 Unity Profiler로 **before/after GC Alloc + CPU 시간** 비교 후 반영.
- 최적화 목적의 PR/커밋은 측정 결과를 커밋 메시지에 포함.

### 정책 위반 시 체크리스트
- `using System.Linq` 추가 시 리뷰어 재확인.
- `new List<>()`, `new Dictionary<>()`가 `FixedUpdate`/`ProcessActionQueue` 경로에 추가될 때 재확인.
- 상세: `CONVENTIONS.md` 최적화 체크리스트.

## 8. 게임 흐름

`Main`이 씬을 전환하고, 로비는 `StageInjection`에 대전 조건을 전달한다. `PuzzleGameController`가 보드를 초기화하고 라운드 종료 시 리플레이 저장과 결과 표시를 처리한다.

[콘텐츠별 진입·저장 호출 경로](../Assets/06_Markdown/01_Architecture/ARCHITECTURE_FLOW.md) · [씬 전환 구현](SCENE.md)

## 9. 폴더 구조 (Assets 기준)

```
01_Scenes/    - SharedScene, TitleScene, LoadingScene, LobbyScene, GameScene, ToolScene
02_Scripts/
  PuzzleCore/ - Module(보드 로직·데이터·리플레이 저장) / View / Controller
  Battle/Pivot/ - 대전 피벗 규칙·세션·게이지 UI
  Manager/    - 시스템 매니저 전체
  Lobby/      - 로비 로직
  Title/      - 타이틀 로직
  UI/         - UI 컴포넌트
Resources/Prefab/ - Puzzle/, UI/, LobbyRoster/, Tool/ 등 프리팹
04_Resources/ - Ingame/Block/ 퍼즐 조각, Images/ 보드·게이지·UI 이미지, LobbyUnitCatalog.json 카드 타입 목록
05_Table/     - Rule/, Stage/, Replay/
```

기능별 구현 시작 파일은 [작업 지도](MAP.md#작업별-빠른-경로)와 각 영역 문서의 **콘텐츠와 C# 매핑** 표에서 확인한다.
