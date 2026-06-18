# PuzleBattleGame 아키텍처 참고 문서

게임 전체 설계 방향과 개발 원칙을 정의한다.
코딩 규칙은 `../../../AGENTS.md`, 영역별 구현 상세는 각 영역 MD 참고.

---

## 1. 프로젝트 지향점

### 게임 정체성
- **퍼즐 대전 게임**. 리플레이를 통해 유저 간 대결을 **랭킹화**하여 구성한다.
- 대결은 동일 시드·동일 스테이지에서 각자 플레이한 리플레이를 비교하는 **비동기 대전** 방식. 실시간 대전은 고려하지 않는다.

### 콘텐츠 구조
- **메인 게임(대전 퍼즐)**: 플레이 진입 시 상대 유저의 리플레이 데이터를 다운로드/로드하고, 플레이어가 같은 조건의 퍼즐을 직접 플레이하여 점수 환산 결과로 랭킹을 구성한다.
- **사이드 콘텐츠(스테이지형 게임)**: ThreeMatch, Link, TapMatch 3가지 모드별 스테이지를 순차 공략하는 별도 콘텐츠다. 메인 대전 진입 흐름과 분리한다.

### 재화/경제 (추후)
- 게임 내 재화는 **별(★) 단일 통화**. 스테이지 클리어 시 쌓인 별이 재화가 된다.
- 재화는 스킨 등 **다양한 아이템 구매**에 사용 (캐시와 유사한 역할).

### 아키텍처에 주는 제약
- **리플레이 기반 랭킹** → 결정론 보장이 기능 요구사항 (§5).
- **비동기 대전** → 서버는 리플레이 저장소 + 랭킹 집계 역할. 실시간 동기화 레이어 불필요.
- **메인/사이드 콘텐츠 분리** → 메인 대전의 리플레이 다운로드/랭킹 흐름에 사이드 스테이지 로드 정책을 직접 섞지 않는다.
- **별 기반 단일 재화** → 사이드 스테이지 단위 보상 규칙이 데이터로 정의되어야 함 (`DATA.md`의 Stage/Objective 확장).
- **스테이지 진행 메타** → Model과 분리된 **유저 메타 데이터 레이어** 필요 (상세: `SERVER.md`).

---

## 2. 핵심 원칙

### MVC 엄격 분리
- **Model** (`PuzzleCore/Module/`): 순수 C# 논리. `UnityEngine` 종속성 금지.
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

---

## 3. 런타임 루프

### 프레임 모델
- **`Update`**: Controller가 유저 입력 감지 → `board.Input()` 큐에 기록 (시각 시간).
- **`FixedUpdate`**: 논리 프레임 전진. `board.FixedUpdate()`에서만 프레임 카운터 증가.
- Model의 모든 시간 의존 로직(타이머, 콤보 유지, 피버 지속)은 **논리 프레임 기반**이어야 한다. 실시간(`Time.deltaTime`) 사용 금지.

### 입력 게이팅
- `boardView.IsAnimating == true`이면 Controller가 입력을 차단.
- 애니메이션이 끝나기 전에 다음 프레임 입력을 받으면 뷰와 Model의 논리 프레임이 어긋남.

### 보드 상태머신
`Waiting → Matching → Falling → Filling → Waiting` 루프, 목표 달성/시간 종료 시 `Finish`.
상세 전이 조건: `INGAME.md` "Board 상태 머신".

---

## 4. Model-View 계약

### 큐 기반 단방향 통신
- View는 Model을 직접 참조하지 않는다. Model이 `BoardViewAction`을 큐에 쌓고 View가 `FetchActions()`로 소비.

### FetchActions() 소유권 이전
- 참조 스왑 패턴(`var res = _views; _views = new List<>(); return res;`)으로 복사 없이 반환.
- 반환 리스트는 호출자 소유 — 코루틴 yield 중에도 변경 없음 보장.

### 정렬 규약
- `frame` + `orderIndex` 기준 `List.Sort()` 정렬(**불안정 정렬**).
- 같은 (frame, orderIndex) 내부 순서는 미보장 → 타입별 처리 순서를 **소비 측에서 명시적으로 분리**해야 함.
- 상세: `INGAME.md` "ExecuteBatchMovement 처리 순서 규칙"

---

## 5. 결정론 보장

리플레이 기능의 근간. **어기면 동일 시드로도 다른 결과가 나와 리플레이가 깨진다.**

### Model(`PuzzleCore/Module/`)에서 금지된 비결정론 소스
| 소스 | 이유 | 대체 |
|------|------|------|
| `UnityEngine.Random`, `Random.Range` | 전역 상태, 시드 격리 불가 | `PuzzleRandom`(GameSpec.randomSeed 주입) |
| `System.Random` | 시드 미주입 시 매번 다름 | `PuzzleRandom` |
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

---

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

---

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

---

## 8. 게임 흐름

메인 대전 퍼즐과 사이드 스테이지형 콘텐츠의 상세 흐름은 `ARCHITECTURE_FLOW.md` 참고.
씬 전환 상세는 `SCENE.md` 참고.

---

## 9. 폴더 구조 (Assets 기준)

```
01_Scenes/    - SharedScene, TitleScene, LoadingScene, LobbyScene, GameScene
02_Scripts/
  PuzzleCore/ - Module(Model) / View / Controller
  Manager/    - 시스템 매니저 전체
  Lobby/      - 로비 로직
  Title/      - 타이틀 로직
  UI/         - UI 컴포넌트
03_Prefab/    - Puzzle/, UI/
04_Resources/ - 이미지, 사운드
05_Table/     - Rule/, Stage/, Replay/
```

주요 소스 파일 위치표: `MAP.md`
