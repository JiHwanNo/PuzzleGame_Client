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

## 블럭 아키텍처

### 상속 구조
```
BaseBlock (추상) — State: Idle, Selected, Moving, Matched, Falling, None
├─ NormalBlock (ISwappableBlock, ILinkableBlock)
└─ BombBlock (ITouchableBlock + ISwappableBlock)
```

### 능력 인터페이스
| 인터페이스 | 메서드 | 용도 |
|-----------|--------|------|
| ITouchableBlock | `OnTouched(board, myPos)` | 터치 시 실행 |
| ISwappableBlock | `OnSwapped(board, myPos, targetPos)` | 스왑 시 실행 |
| ILinkableBlock | `CanLink(board, myPos, previousPos)` | 링크 연결 판정 |

### 팩토리 (PuzzleBlockFactory.Create)
- Touch + Swap → BombBlock
- Swap only → NormalBlock
- 기타 → NormalBlock

### 새 블럭 추가 시
1. `BaseBlock` 상속 클래스 작성 (`Module/Block/`)
2. 필요한 능력 인터페이스 구현
3. `PuzzleBlockFactory.Create()`에 분기 추가
4. `DATA.md` 참고하여 BlockData JSON 추가

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

### 기록 흐름
```
게임 시작 → StageInjection이 randomSeed 생성 → Board.Initialize(spec)에 시드 주입
게임 중   → Board가 InputRecord / InputEndRecord 자동 기록
게임 종료 → PuzzleGameController가 Finish 감지
  → board.GetRecordedInputs() + GetRecordedInputEnds()
    → ReplayData 조립 → ReplayStorage.Save() → JSON 파일 저장
```

### 재생 흐름 (ReplayController)
```
ReplayData 로드 → GameSpec 복원 (동일 Rule/Stage + 동일 시드)
  → IPuzzleBoard 생성 + Initialize
    → FixedUpdate에서 프레임 카운터 증가
      → 해당 프레임의 Input/InputEnd 기록을 보드에 주입
        → 동일한 게임 결과 재현
```

### ReplayController 배치
- 메인 보드와 독립적으로 동작 (별도 IPuzzleBoard + PuzzleBoardView)
- `viewScale`, `margin` 파라미터로 우측 상단에 축소 자동 배치
- `PuzzleBoardView.skipCameraAlign = true`로 카메라 간섭 방지
