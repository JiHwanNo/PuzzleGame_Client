# PuzleBattleGame 인게임 퍼즐 참고 문서

보드, 블럭, 매칭, 게임 루프, 뷰 동기화 작업 시 참고.
데이터/JSON 구조는 `DATA.md` 참고.

---

## 게임 루프

### Controller (PuzzleGameController)
- `Update()`: 마우스/터치 입력 → `Physics2D.OverlapPoint` → `PuzzleBlockCollider` → `board.Input(gridPos)`
- 포인터 릴리즈 시 `board.InputEnd()` 호출
- `FixedUpdate()`: `board.FixedUpdate()` (논리 프레임 전진)
- `boardView.IsAnimating`이 true이면 입력 차단

### Board 상태 머신 (IPuzzleBoard.Update)
```
Waiting: 유저 입력 수신 대기
  ↓ InputEnd() → 스왑 시도
Matching: FindMatches() → 3+ 연속 블럭 탐색 → 파괴 → AddView(Destroy)
  ↓
Falling: 빈 칸으로 블럭 낙하 + Generator 셀에서 새 블럭 생성 → AddView(Fall, CreateAndFall)
  ↓ 다시 Matching으로 (연쇄 반응)
Waiting: 매칭 없으면 복귀, HasPossibleMoves() 실패 시 자동 셔플
  ↓ 목표 달성 또는 시간 종료
Finish
```

---

## IPuzzleBoard 인터페이스

| 메서드 | 용도 |
|--------|------|
| `Initialize(GameSpec)` | 보드 초기화 (셀/블럭 생성) |
| `Input(GridPos)` | 유저 입력 큐에 추가 |
| `InputEnd()` | 큐 소비 → 스왑 시도 |
| `Update()` | 상태 머신 실행 (Matching/Falling/Filling) |
| `FixedUpdate()` | 논리 프레임 전진 + 타이머 갱신 |
| `Pause(bool)` | 일시정지 |
| `AddView(BoardViewAction)` | 뷰 액션 기록 |
| `FetchActions()` | 기록된 뷰 액션 반환 후 초기화 |
| `GetRecordedInputs()` | 리플레이용 입력 기록 반환 |
| `GetRecordedInputEnds()` | 리플레이용 입력 종료 기록 반환 |

**프로퍼티**: `State`, `Random`, `Objective`, `Cells`, `Width`, `Height`

**구현체**: ThreeMatchPuzzleBoard, LinkPuzzleBoard, TapMatchPuzzleBoard

---

## 블럭 아키텍처 (기믹 컴포지션)

서브클래스 상속(BaseBlock/NormalBlock/BombBlock)을 폐기하고, **단일 `Block` 클래스 + 기믹 부착(컴포지션)** 구조로 전환되었다.
블럭 자체는 데이터만 보유하고, 특수 행동(폭발 등)은 부착된 기믹(`IGimmick`)이 담당한다.

### 단일 블럭 클래스
```
Block : IGimmickHost — State: Idle, Selected, Moving, Matched, Falling, None
  ├─ _inputType : InputType   ← 데이터 inputType(문자열 목록)을 플래그로 파싱·캐싱
  └─ Gimmicks : List<IGimmick> ← 부착된 기믹 목록 (행동 담당)
```
- 조작 가능 여부(스왑/링크/터치)는 **데이터(`inputType`)로 판정**한다. 더 이상 서브클래스가 `return true`로 하드코딩하지 않는다.
- 생성자에서 `BlockData.inputType`(예: `["Swap","Touch"]`)을 `Enum.TryParse`로 `InputType` 플래그로 변환해 캐싱하고, `GetInputType()`으로 노출한다.

### 능력 게이팅 (보드 책임)
보드가 입력을 처리하기 전에 블럭의 `GetInputType()` 플래그를 검사해 행위를 허용/차단한다.
| 모드/행위 | 게이팅 위치 | 검사 |
|-----------|------------|------|
| 스왑 (ThreeMatch) | `ThreeMatchPuzzleBoard` | `GetInputType().HasFlag(InputType.Swap)` |
| 링크 (Link) | `LinkPuzzleBoard` | `GetInputType().HasFlag(InputType.Link)` |
| 터치 (TapMatch) | `TapMatchPuzzleBoard` | `GetInputType().HasFlag(InputType.Touch)` |

### 기믹 시스템 (`Module/Gimmick/`)
| 타입 | 역할 |
|------|------|
| `IGimmick` | 기믹 계약. `OnAttach(owner)`, `OnDestroyed(board, myPos)`, `OnTouched(board, myPos)`, `OnSwapped(board, myPos, targetPos)` 훅 정의 |
| `IGimmickHost` | 기믹을 부착받는 호스트 계약. `List<IGimmick> Gimmicks`, `AddGimmick(gimmick)` (현재 구현체는 `Block`) |
| `GimmickBase` | 모든 훅에 빈 기본 구현 제공(추상). 구체 기믹은 필요한 훅만 `override` |
| `BombGimmick` | 파괴 시 **원형(유클리드 거리) 반경** 내 블럭을 연쇄 파괴. 생성자 `radius`(최소 1) |
| `GimmickFactory` | `GimmickType`(enum) → 기믹 인스턴스 생성. `Bomb` → `BombGimmick(2)`(반경 2) |
| `GimmickUtil` | 파괴 공통 헬퍼(`internal static`). 목표 갱신 + `Destroy` 뷰 등록 + 연쇄 기믹 발화를 한 곳에 모음 |

### 파괴/연쇄 흐름
```
보드가 블럭 파괴 결정
  → destroyed.FireDestroyed(board, pos)        ← Block이 부착된 모든 기믹의 OnDestroyed 발화
    → BombGimmick.OnDestroyed(board, myPos)     ← 원형 반경 순회
      → GimmickUtil.DestroyBlock(board, target) ← 대상 파괴 (빈 칸/막힘·잠금 셀이면 종료)
        → cell.Block = null (먼저 비워 중복·무한 연쇄 차단)
        → board.AddView(Destroy)               ← 연출 프레임/order는 AddView가 스탬프
        → destroyed.FireDestroyed(...)          ← 연쇄 (폭탄이 폭탄을 터뜨림)
```
- 보드의 파괴 경로(ThreeMatch/Link/TapMatch 모두)는 블럭 제거 시 `destroyed.FireDestroyed(this, pos)`를 호출한다.
- **연출 책임 분리**: 기믹/`Block`은 `frame`/`orderIndex`(연출 데이터)를 다루지 않는다. 보드의 `AddView`가 frame을 스탬프하고 order를 부여한다.
- `IGimmick`은 `OnTouched`/`OnSwapped` 훅도 정의하지만, 현재 보드는 입력을 `inputType`으로 게이팅하며 **파괴 경로(`OnDestroyed`)만 발화**한다. 터치/스왑 훅은 향후 확장용으로 예약되어 있다.

### 새 블럭(기믹) 추가 절차
특정 행동이 필요 없는 일반 색 블럭은 **JSON에 BlockData만 추가**하면 된다(코드 변경 불필요). 새 행동(기믹)이 필요한 경우:
1. `Module/Gimmick/`에 `GimmickBase` 상속 기믹 클래스 작성, 필요한 훅(`OnDestroyed` 등)만 `override`.
2. `PuzzleDefine.cs`의 `GimmickType` enum에 값 추가.
3. `GimmickFactory.Create()`에 `GimmickType → 기믹 인스턴스` 생성 분기 추가(파라미터는 여기서 주입).
4. Rule JSON `blocks[]`의 BlockData에 `gimmickIds`(문자열 목록, 예: `["Bomb"]`)와 필요한 `inputType` 추가 → 상세: `DATA.md`.
- `inputType`(조작 방법)과 `gimmickIds`(행동)는 **서로 독립적인 데이터**다.

---

## 입력 처리 방식

### 3매치 (ThreeMatch)
1. **탭-탭**: 첫 블럭 선택 → 인접 블럭 선택 → 스왑
2. **드래그**: 홀드 후 드래그 → 첫-마지막 인접 블럭 스왑

### 링크 (Link)
- 드래그로 같은 종류 블럭 연결 → 릴리즈 시 경로상 블럭 파괴
- `LineRenderer`로 경로 시각화

### 탭 매치 (TapMatch)
- 블럭 터치 시 즉시 파괴 로직 실행

---

## 점수 및 콤보 (ObjectiveManager)

| 항목 | 값 |
|------|-----|
| 기본 점수 | 블럭당 10점 |
| 콤보 배율 | 1.0 + (combo - 1) × 0.2, 최대 3.0x |
| 피버 조건 | 7콤보 이상 |
| 피버 효과 | 7초간 (350프레임) 2.0x 추가 배율 |
| 콤보 유지 | 3초 (150프레임) 이내 재매칭 |

**목표 종류**: `Score` (점수 달성), `CollectBlock` (특정 블럭 수집), `ClearCell` (셀 클리어)

---

## 뷰 동기화 (PuzzleBoardView)

### 액션 처리 흐름
```
board.FetchActions()
  → List<BoardViewAction> (frame + orderIndex 순서, List.Sort 불안정 정렬)
  → 내부 _views 리스트를 참조 스왑으로 반환 (복사 비용 없음)
    → GroupActionsByFrameAndOrder() 수동 그룹화 (같은 frame+order는 동시 실행)
      → ProcessActionQueue 코루틴
        → ExecuteBatchMovement (Move, Fall, CreateAndFall)
          → _batchActions/_batchViews 병렬 리스트로 매핑 (Dictionary 할당 방지)
        → ExecuteSingleAction (Destroy, Create)
```

### ExecuteBatchMovement 처리 순서 규칙
- **반드시 Move/Fall을 먼저 처리한 뒤 CreateAndFall을 처리**해야 한다.
- Fall과 CreateAndFall은 같은 `orderIndex`(fallOrder)로 추가되므로 불안정 정렬 시 순서가 뒤바뀔 수 있음.
- CreateAndFall은 `targetPosition`에 기존 뷰가 있으면 `HandleImmediateDestroy`로 파괴하는데, Fall이 먼저 실행되어 해당 위치의 뷰를 제거하지 않으면 이동 예정 블럭이 파괴되어 미씽 발생.
- 따라서 루프를 분리하여 Move/Fall → CreateAndFall 순서를 보장한다.

### BoardViewAction 구조
| 필드 | 용도 |
|------|------|
| `frame` | 논리 프레임 번호 |
| `orderIndex` | 시각 순서 (같은 frame 내 정렬) |
| `type` | ViewType: Destroy, Create, Move, Fall, CreateAndFall, Land |
| `position` | 원본 좌표 |
| `targetPosition` | 이동 대상 좌표 |
| `blockData` | 블럭 정보 (생성 시) |

### 애니메이션 시간
| 종류 | 시간 | Ease |
|------|------|------|
| 클릭 | 스케일 1.1x, 0.038초, 2회 yoyo | — |
| 이동 (Move) | 0.075초 | OutBack |
| 낙하 (Fall) | 0.075초 | OutQuad |
| 파괴 (Destroy) | 스케일→0, 0.075초 | InBack |
| 생성 (Create) | 스케일 0→1, 0.075초 | OutBack |
| 액션 간 대기 | 0.019초 | — |

### 뷰 액션 처리 시 주의사항
- `FetchActions()`는 `List.Sort()` 불안정 정렬 사용 → 같은 (frame, orderIndex) 내 액션 순서 미보장.
- `FetchActions()`는 참조 스왑 방식으로 반환 — 반환된 리스트는 호출자 소유, 코루틴 yield 중 안전.
- `ProcessFallingAndFilling()`에서 Fall과 CreateAndFall은 **같은 fallOrder**로 추가됨 → 불안정 정렬 시 순서가 뒤바뀔 수 있음.
- 따라서 `ExecuteBatchMovement`나 `ProcessActionQueue` 등 뷰 액션을 소비하는 코드에서는 **타입별 처리 순서를 명시적으로 분리**해야 함.
- `ExecuteBatchMovement`는 `_batchActions`/`_batchViews` 병렬 리스트를 사용 — Dictionary 열거 GC 할당 방지.
- 새로운 ViewType을 추가할 때도 기존 타입과의 처리 순서 의존성을 반드시 확인할 것.
- `PuzzleBoardView.OnDestroy()`에서 `StopAllCoroutines()` 호출 — 파괴 중 코루틴 접근 크래시 방지.

### 좌표 변환 (GetLocalPos)
- 사각형: 보드 중앙 기준 `(X - width/2, Y - height/2) × cellSize`
- 육각형 (Even-Q Flat-Top): 짝수 열은 Y에 `cellSize × 0.5f` 오프셋
- 카메라 orthographicSize를 보드 높이에 맞게 자동 설정

---

## 핵심 열거형 (PuzzleDefine.cs)

| 열거형 | 값 |
|--------|-----|
| PuzzleType | ThreeMatch, Link, TapMatch |
| BoardShape | Quadrangle, Hexagon |
| CellType | Close, Normal, Lock, Generator |
| InputType | Swap(1), Link(2), Touch(4) — Flags |
| GimmickType | None(0), Bomb(1) — JSON엔 문자열, 내부 처리는 enum |
| BoardState | Waiting, Matching, Falling, Filling, Finish |
| ViewType | Destroy, Create, Move, Land, Fall, CreateAndFall |
| BlockState | Idle, Selected, Moving, Matched, Falling, None |

### GridPos
- `int X, Y` (public 필드, JSON 직렬화 가능) + 정적 방향 (Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight)
- `IsAdjacentSquare(a, b)`: 4방향 인접 판정
- `IsAdjacentHexagon(a, b)`: Even-Q Flat-Top 6방향 인접 판정

### InputRecord / InputEndRecord (PuzzleDefine.cs)
- `InputRecord`: `ulong frame` + `GridPos position` — 유저 클릭/터치 시점 기록
- `InputEndRecord`: `ulong frame` — 유저 포인터 릴리즈 시점 기록
- 모든 보드 구현체에서 `Input()` / `InputEnd()` 호출 시 자동 기록

---

## 리플레이 시스템

기록/재생 흐름과 ReplayController 배치 상세는 `INGAME_REPLAY.md` 참고.
