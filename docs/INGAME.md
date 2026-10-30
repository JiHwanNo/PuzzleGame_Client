# PuzleBattleGame 인게임 퍼즐 참고 문서

보드, 블럭, 매칭, 게임 루프, 뷰 동기화 작업 시 참고.
데이터/JSON 구조는 `DATA.md` 참고.

## 콘텐츠와 C# 매핑

[전체 작업 지도](MAP.md) · [문서 목록](README.md)

파일 링크는 이 문서 기준 상대 경로이며, 마지막 열의 이름을 파일 안에서 검색하면 구현 위치를 찾을 수 있습니다.

| 찾는 콘텐츠 | 담당 C# 파일 | 주요 메서드·타입·멤버 |
|---|---|---|
| 입력, 게임 루프, 종료 후 저장, 점수/타이머 UI | [PuzzleGameController.cs](../Assets/02_Scripts/PuzzleCore/Controller/PuzzleGameController.cs) | `Start`, `Update`(입력 수집/UI), `FixedUpdate`(양쪽 공통 틱), `SaveReplay`, `ShowBattleResult`, `UpdateTimerUI` |
| 보드 공통 계약 | [IPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/IPuzzleBoard.cs) | `IPuzzleBoard`, `BoardViewAction` |
| 3매치 스왑·매칭·낙하·셔플 | [ThreeMatchPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/ThreeMatchPuzzleBoard.cs) | `ProcessSwapInput`, `FindMatches`, `ProcessFallingAndFilling`, `FillFromGenerator`, `CheckAndShuffleIfNoMoves` |
| 드래그 링크 연결·해제·낙하·매칭 대상 확장 | [LinkPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/LinkPuzzleBoard.cs) | `Input`, `InputEnd`(큐 등록), `FixedUpdate`, `UpdateState`, `ProcessFallingAndFilling`, `FillFromGenerator`, `HasEmptyCell`, `MatchTargetsExpanding` |
| 탭 매치 연결 블럭 탐색·파괴 | [TapMatchPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/TapMatchPuzzleBoard.cs) | `Input`, `InputEnd`(큐 등록), `FixedUpdate`, `UpdateState`, `GetConnectedBlocks`, `FillFromGenerator`, `CheckAndShuffleIfNoMoves` |
| 블럭 상태·입력 플래그·기믹 발화 | [Block.cs](../Assets/02_Scripts/PuzzleCore/Module/Block/Block.cs) | `Block`, `GetInputType`, `FireDestroyed` |
| 블럭 생성·기믹 부착 | [PuzzleBlockFactory.cs](../Assets/02_Scripts/PuzzleCore/Module/Block/PuzzleBlockFactory.cs) | `PuzzleBlockFactory` |
| 기믹 계약·호스트·기본 클래스 | [IGimmick.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/IGimmick.cs) | `IGimmick`, `IGimmickHost`, `GimmickBase` |
| 폭탄 범위·연쇄 파괴 | [BombGimmick.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/BombGimmick.cs) | `OnDestroyed` |
| 기믹 종류별 생성 | [GimmickFactory.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/GimmickFactory.cs) | `Create` |
| 공통 파괴·목표 갱신·뷰 액션 등록 | [GimmickUtil.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/GimmickUtil.cs) | `DestroyBlock` |
| 셀 상태와 블럭 보관 | [PuzzleCell.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzleCell.cs) | `PuzzleCell` |
| 바닥 패널 기본 타입 | [PuzzlePanel.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzlePanel.cs) | `PuzzlePanel`, `NormalPanel` |
| 점수·콤보·피버·시간 제한·목표 판정·누적 파괴/조작 성공 수 | [ObjectiveManager.cs](../Assets/02_Scripts/PuzzleCore/Module/ObjectiveManager.cs) | `OnMatchEvent`, `OnBlockDestroyed`, `UpdateTimer`, `ShouldFinish`, `IsAllObjectivesCleared`, `TotalDestroyedBlocks`, `TotalPlayerMatches` |
| 보드 배치·좌표 변환·뷰 액션 순서·링크 선 | [PuzzleBoardView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleBoardView.cs) | `DrawBoard`, `GetLocalPos`, `ExecuteBatchMovement`, `UpdateLineRenderer`, `CacheLinkPathIfChanged` |
| 블럭 스프라이트·클릭/이동/낙하/파괴 연출 | [PuzzleBlockView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleBlockView.cs) | `UpdateVisual`, `PlayMoveAnimation`, `PlayFallAnimation`, `PlayDestroyAnimation` |
| 셀 색상·상태 시각화 | [PuzzleCellView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleCellView.cs) | `Initialize`, `UpdateVisual` |
| 입력 충돌체와 블럭 뷰 연결 | [PuzzleBlockCollider.cs](../Assets/02_Scripts/PuzzleCore/PuzzleBlockCollider.cs) | `PuzzleBlockCollider` |
| 입력 충돌체와 셀 뷰 연결 | [PuzzleCellCollider.cs](../Assets/02_Scripts/PuzzleCore/PuzzleCellCollider.cs) | `PuzzleCellCollider` |
| 리플레이 보드 생성·프레임 입력 재생·축소 배치 | [ReplayController.cs](../Assets/02_Scripts/PuzzleCore/Controller/ReplayController.cs) | `Initialize`, `AdvanceFrame`, `HasValidResult`, `HasCompatibleBattleRules`, `UpdateStatusPosition`, `AlignToTopRight` |
| 시드 기반 난수 | [PuzzleRandom.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzleRandom.cs) | `Next`, `NextDouble`, `SetSeed` |
| 대전 피벗 충전·활성·콤보 소비 감속 | [PivotMode.cs](../Assets/02_Scripts/Battle/Pivot/PivotMode.cs) | `AdvanceTick`, `Stop`, `GaugeUnits`, `GaugePoints`, `IsActive`, `ComboCount`, `CurrentDrainPercent` |
| 피벗 충전량·소비량·콤보 간격 설정 | [PivotSettings.cs](../Assets/02_Scripts/Battle/Pivot/PivotSettings.cs) | `PivotSettings`, `IsValid`, `CopyValidated`, `HasSameRules` |
| 보드 통계·모드별 효과와 독립 피벗 모듈 연결 | [BattlePivotSession.cs](../Assets/02_Scripts/Battle/Pivot/BattlePivotSession.cs) | `BattlePivotSession`, `AdvanceAfterBoardTick`, `Dispose` |
| 링크 피벗의 육각형 이웃 한 겹 추가 파괴 | [LinkPivotEffect.cs](../Assets/02_Scripts/Battle/Pivot/LinkPivotEffect.cs) | `LinkPivotEffect`, `ExpandTargets` |
| 플레이어 피벗 게이지 UI | [PivotGaugeView.cs](../Assets/02_Scripts/Battle/Pivot/PivotGaugeView.cs) | `PivotGaugeView`, `Refresh`, `Dispose` |

규칙/좌표/입력 기록 타입과 리플레이 파일 저장은 [DATA.md](DATA.md)의 코드 표를 함께 확인하세요.

---

## 게임 루프

### Controller (PuzzleGameController)
- `Update()`: 마우스/터치 입력 → `Physics2D.OverlapPoint` → `PuzzleBlockCollider` → `board.Input(gridPos)`
- 포인터 릴리즈 시 `board.InputEnd()` 호출
- `FixedUpdate()`: `board.FixedUpdate()` 후 `replayController.AdvanceFrame()` 호출. 양쪽 입력 판정·타이머·상태 전환을 동일한 공통 틱에서 진행
- `boardView.IsAnimating`이 true이면 입력 차단

### Board 상태 머신 (FixedUpdate 내부 UpdateState)
```
Waiting: 유저 입력 수신 대기
  ↓ 고정 틱에서 입력 큐 소비 → 스왑 시도
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
| `InputEnd()` | 입력 종료를 큐에 등록. 반환값은 등록 성공 여부 |
| `FixedUpdate()` | 입력 큐 소비 + 타이머/상태 머신 판정 + 논리 프레임 전진 |
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

## 입력 안전성과 초기화

- `PuzzleGameController.GetGridPosFromCollider()`는 `PuzzleBlockView.BoardView`가 플레이어 보드인 경우만 입력을 받습니다. 리플레이의 `PuzzleBoardView.InputEnabled`는 꺼 두며, 뷰를 직접 클릭해도 모델에 사용자 입력을 전달하지 않습니다.
- 3매치는 `Close`/`Lock` 셀을 선택 및 스왑 대상에서 제외합니다. 초기 빈칸이 있으면 첫 고정 틱에서 낙하·생성을 한 번 처리한 뒤 매칭으로 이동합니다. 생성기가 없어 채울 수 없는 빈칸은 무한 재시도하지 않습니다.
- 3매치/탭 매치의 셔플은 100회 내 유효 배치를 찾지 못하면 원래 블럭 참조와 배치를 복원합니다. 성공했을 때만 새 배치의 뷰 액션을 전달합니다.
- 탭 매치의 셔플 성공 시 같은 논리 틱의 연출 순서는 **파괴 → 낙하/생성 낙하 → 셔플 생성**입니다. 순서 인덱스는 셔플 등록 후 초기화합니다.
- 이동 가능 여부 검사도 실제 입력 규칙을 따릅니다. 3매치는 교환 양쪽에 `Swap`, 탭 매치는 시작 블럭에 `Touch`가 있어야 합니다. 탭의 같은 색 연결 대상은 `Touch`가 없어도 실제 파괴 판정과 동일하게 인정합니다.
- 세 보드의 낙하 처리에서 누락된 셀 좌표는 낙하 경계로 취급합니다. 맵은 모든 셀을 작성하는 것이 원칙이며, 누락 데이터에서도 블럭 참조를 덮어쓰지 않도록 방어합니다.
- `PuzzleBlockView.UpdateVisual()`은 요청 세대를 비교하여 이전 이미지 완료 콜백을 무시합니다. `OnDisable()`에서도 세대를 증가시켜 풀 반환 전 요청을 무효화하며, 새 이미지 대기 중에는 이전 스프라이트를 표시하지 않습니다.

## 입력 처리 방식

### 3매치 (ThreeMatch)
- `ProcessSwapInput()`은 교환 판정 전에 선택 좌표와 선택 표시를 함께 해제합니다. `Swap` 권한 부족 등으로 거절되어도 이전 선택 표시가 남지 않습니다.
- `ProcessFallingAndFilling()` → `FillFromGenerator()`는 아래에서 위로 생성기를 찾으며, 각 생성기 자신의 목록으로 아래 구간부터 자신의 셀까지 빈칸을 채웁니다. `Close`/`Lock`/누락 셀과 이전 생성기가 공급 구간을 나눕니다. 생성기 없는 구간과 생성기 위쪽은 채우지 않습니다.
- 단일 최상단 생성기의 생성 순서(열 순서, 아래→위)는 유지합니다. 생성 연출은 해당 생성기 바로 위에서 시작합니다.

1. **탭-탭**: 첫 블럭 선택 → 인접 블럭 선택 → 스왑
2. **드래그**: 홀드 후 드래그 → 첫-마지막 인접 블럭 스왑

### 링크 (Link)
- 초기 빈칸이 있으면 첫 고정 틱에서 낙하·생성을 한 번 처리한 뒤 입력 대기로 이동합니다. 생성기 없는 빈칸은 반복 재시도하지 않습니다.
- `ProcessFallingAndFilling()` → `FillFromGenerator()`는 3매치와 동일하게 각 생성기 자신의 목록으로 아래 공급 구간을 채웁니다. `Close`/`Lock`/누락 셀과 이전 생성기가 구간을 나눕니다.
- `PuzzleBoardView.CacheLinkPathIfChanged()`는 전체 경로의 좌표 복사본을 비교합니다. 길이와 끝점이 같아도 중간 경로가 바뀌면 선과 마커를 갱신하며, 보드를 다시 그릴 때 캐시를 비웁니다.
- 드래그로 같은 종류 블럭 연결 → 릴리즈 시 경로상 블럭 파괴
- `LineRenderer`로 경로 시각화
- 매칭을 확정한 뒤 같은 고정 틱에 종료 입력이 다시 들어와도 확정된 링크 경로는 유지한다.

### 탭 매치 (TapMatch)
- `Initialize()`는 `Falling`으로 시작합니다. 첫 고정 틱에서 빈칸 보충과 `CheckAndShuffleIfNoMoves()`를 한 번 수행한 뒤 입력 대기로 이동합니다. 불가능한 셔플/빈 생성 목록을 매 틱 반복하지 않습니다. 첫 초기화 틱 이전 입력은 처리하지 않습니다.
- `ProcessInputEnd()`는 첫 입력 셀의 선택 상태를 먼저 해제합니다. `Touch` 권한 부족이나 셀 상태 변경으로 거절되어도 선택 표시가 남지 않습니다.
- `ProcessFallingAndFilling()` → `FillFromGenerator()`는 각 생성기의 목록으로 아래 공급 구간을 채웁니다. `Close`/`Lock`/누락 셀과 이전 생성기가 경계이며, 생성기 없는 구간은 보충하지 않습니다. 성공한 셔플은 보충 이후 순서로 화면에 반영합니다.
- 블럭 터치/릴리즈를 큐에 저장하고 고정 틱에서 파괴 판정.
- `Close`(막힌 구역)와 `Lock`(로직상 잠긴 셀)은 탭 선택과 연결 탐색에서 제외한다. 같은 색이어도 이 셀을 통과해 연결하지 않는다.

---

## 점수 및 콤보 (ObjectiveManager)

- 콤보는 **유저 조작으로 성공한 매칭 횟수**다. 3매치는 성공한 교환의 첫 매칭만, 링크는 3개 이상 연결 확정만, 탭 매치는 2개 이상 그룹 파괴 성공만 한 번씩 센다.
- 초기 보드 자동 매칭, 낙하 후 자동 연쇄, 폭탄과 피벗 추가 파괴는 별도 콤보를 올리거나 유지 타이머를 갱신하지 않는다. 점수·수집·파괴 개수는 정상 반영한다.
- `TotalPlayerMatches`는 피버 중에도 유저 성공 횟수를 기록한다. 피벗은 이 누적값과 `TotalDestroyedBlocks`를 분리해서 전달받는다.
- 이전 리플레이(`comboRuleVersion: 0`)의 3매치만 `GameSpec.UseLegacyAutomaticCombos`로 당시 자동 연쇄 포함 규칙을 재현한다.

| 항목 | 값 |
|------|-----|
| 기본 점수 | 블럭당 10점 |
| 콤보 배율 | 1.0 + (combo - 1) × 0.2, 최대 3.0x |
| 피버 조건 | 7콤보 이상 |
| 피버 효과 | 7초간 (350프레임) 2.0x 추가 배율 |
| 콤보 유지 | 3초 (150프레임) 이내 재매칭 |

**목표 종류**: `Score` (점수 달성), `CollectBlock` (특정 블럭 수집).
- 알 수 없는 목표 종류도 미달성으로 판정한다. 현재 플레이용 Rule에는 지원하는 목표만 사용한다.

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
- `InputRecord`: `ulong frame` + `ulong sequence` + `GridPos position` — 유저 클릭/터치 시점 기록
- `InputEndRecord`: `ulong frame` + `ulong sequence` — 유저 포인터 릴리즈 시점 기록
- 모든 보드 구현체에서 입력을 큐에 보관하고 고정 틱에서 처리할 때 기록. `sequence`는 좌표/종료 입력 간 공통 순서

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
    → 메인 컨트롤러의 공통 틱에서 AdvanceFrame 호출
      → 0번 프레임부터 Input/InputEnd를 frame + sequence 순서로 큐에 주입
      → board.FixedUpdate에서 입력과 상태 머신 판정
        → 프레임 카운터 증가
```

### ReplayController 배치
- 별도 모델/뷰를 사용하지만 논리 틱은 `PuzzleGameController.FixedUpdate()`가 함께 진행한다. `ReplayController`는 자체 `FixedUpdate()`를 실행하지 않는다.
- `viewScale`, `margin` 파라미터로 우측 상단에 축소 자동 배치
- `PuzzleBoardView.skipCameraAlign = true`로 카메라 간섭 방지

### 고정 틱 전환과 기존 리플레이
- 매칭·낙하·입력에 따른 블럭 상태 변경은 모두 모델의 `FixedUpdate()`에서 실행한다. `Update()`에서는 입력 수집과 UI/뷰 연출만 실행한다.
- 기존 리플레이에 `sequence`가 없으면 같은 프레임의 좌표 입력을 종료 입력보다 먼저 처리한다. 과거 가변 프레임 판정 결과와의 완전한 일치는 보장하지 않는다.

## 스테이지와 60초 대전
- `GameSpec.mode`의 기본값은 `Stage`. 목표 전체 달성 또는 Rule의 제한 시간 종료 시 끝난다.
- `Battle`은 목표 달성으로 끝나지 않으며, 50틱/초 × 60초 = 3,000틱을 정확히 진행한다. 양쪽 보드는 동일한 규칙·맵·시드를 사용한다.
- `ObjectiveManager`는 시간을 정수 틱으로 관리하고 표시할 때만 초로 변환한다. 콤보/피버도 기존처럼 논리 틱을 사용한다.
- `GameScene`의 작은 보드 아래에 남은 시간, 입력이 없는 대기 상태, 재생 완료 및 점수를 표시한다. 기존 기록은 `이전 기록`으로 구분한다.
- 대전 종료 시 현재 점수를 비교해 승리/패배/무승부를 4초간 표시한 뒤 로비로 돌아간다. 첫 기록 도전이면 본인 점수만 표시한다.
- 기록의 종료 틱·최종 점수가 재생 결과와 다르면 오류 상태를 표시하고 승패 비교에서 제외한다.
- 결과 UI와 리플레이 상태 UI는 `GameScene.unity`의 `BattleResult`, `ReplayStatus`이며 기존 한글 폰트 에셋을 사용한다.

## 대전 전용 피벗 모드

- 기존 피버와 피벗은 별도의 상태로 동작한다. 콤보 증가는 유저 조작 성공 기준이며 피벗 자체의 추가 점수 배율은 없다. 링크 피벗의 추가 파괴는 기존 블럭 점수 규칙으로 점수를 얻는다.
- `Battle/Pivot/PivotMode`와 `PivotSettings`는 Unity 및 `Puzzle.Core`를 참조하지 않는 순수 C# 모듈이다. 0.01점 단위 정수 게이지와 초당 50 논리 틱으로 동작하며 소수점은 표시할 때만 사용한다.
- `ObjectiveManager.TotalDestroyedBlocks`는 모드에 관계없이 실제 파괴 수를 기록한다. 일반 매칭과 폭탄 연쇄가 모두 포함되며, 단순 이동·생성·셔플은 포함하지 않는다.
- 연결 계층 `BattlePivotSession`은 대전 보드 초기화 직후 생성하고, 보드의 `FixedUpdate()` 직후 `AdvanceAfterBoardTick()`을 한 번 호출한다. 누적 파괴 수와 유저 매칭 성공 횟수의 증가분을 각각 전달하며 액션 큐는 소비하지 않는다. 링크 보드에는 별도 효과의 매칭 대상 확장 함수를 구독한다.
- `GameMode.Battle`에서만 충전/발동하며 스테이지에서는 세션을 만들거나 피벗 설정을 검증하지 않는다. 플레이어와 상대 리플레이는 세션을 따로 보유한다. 게임 또는 재생 종료 시 `Stop()`으로 게이지와 활성 상태를 정리한다.

| 설정 | 기본값 | 의미 |
|---|---|---|
| `Capacity` | 1,000 | 최대 게이지 점수 |
| `ChargePerBlockUnits` | 10 | 0.01점 단위 충전량. 블럭당 0.1점, 10개당 1점 |
| `ChargeComboStep` | 10 | 충전 보너스가 증가하는 콤보 간격 |
| `ChargeBonusPerStepUnits` | 1 | 단계마다 블럭당 0.01점 추가. 10~19콤보는 0.11점, 20~29콤보는 0.12점 |
| `DrainPerSecond` | 10 | 기본 초당 소비량. 만충 상태에서 100초 유지 |
| `DrainReductionPercentPerCombo` | 25 | 빠른 연속 유저 매칭 성공마다 줄이는 기본 소비 속도의 백분율 |
| `MinimumDrainPercent` | 25 | 최소 소비 속도, 기본값에서 초당 2.5점 |
| `ComboWindowTicks` | 50 | 유저 매칭 성공을 빠른 콤보로 인정하는 최대 간격, 기본 1초 |
| `LinkNeighborClear` | 링크 JSON만 true | 링크 피벗 중 육각형 이웃 한 겹 추가 파괴. 다른 모드와 기존 기록은 false |

- 충전 중에는 게이지가 감소하지 않는다. 최대치 도달 시 발동하며 초과 충전은 버린다. 발동 틱에는 소비하지 않고 다음 틱부터 소비한다.
- 피벗 콤보는 기존 피버 콤보와 독립적이다. 충전 중부터 직전 유저 매칭 성공과 50틱 이내인 성공을 연속 콤보로 세며, 현재 성공을 포함해 10콤보가 되는 순간부터 충전 보너스를 적용한다. 자동 매칭·폭탄·추가 파괴는 콤보를 늘리거나 시간을 갱신하지 않지만, 충전 중에는 당시 콤보 배율로 파괴량만큼 충전한다.
- 활성 중 블럭 파괴는 게이지를 다시 채우지 않는다. 빠른 연속 유저 매칭 성공마다 소비량이 초당 10 → 7.5 → 5 → 2.5점으로 감소한다. 자동 파괴는 감속을 추가하지 않는다. 발동 전 콤보 횟수로 여러 감속 단계를 즉시 적용하지 않는다.
- 50틱 동안 유저 매칭 성공이 없고 다음 틱으로 넘어가면 피벗 콤보는 0으로, 소비 속도는 기본값으로 복귀한다. 게이지 소진 후에는 다시 충전할 수 있으며, 소진 틱의 파괴를 재충전에 중복 사용하지 않는다. 정수 단위 미만 소비량은 다음 틱으로 이월한다.
- 수치는 `Assets/05_Table/Rule/`의 세 Rule JSON 최상위 `pivotSettings`에서 모드별로 조정한다. `AssetManager`로 읽은 `GameRuleContainer` → `StageInjection` → `GameSpec.pivotSettings` → 컨트롤러로 전달한다. Inspector 설정은 사용하지 않는다.
- 대전 준비 시 설정이 없거나 범위를 벗어나면 로그를 남기고 씬 진입 전에 중단한다. 스테이지는 이 설정의 누락·범위 오류 때문에 중단하지 않는다. 구체적인 JSON 예시는 [DATA.md](DATA.md#피벗-게임-데이터)를 참고한다.
- 실행에는 검증된 복사본을 사용하고 `ReplayData.pivotSettings`에 저장하여 재생에서도 당시 설정을 사용한다. 필드가 없는 기존 기록은 기본값을 사용한다. 잘못된 피벗 설정을 포함한 대전 기록은 로드/상대 선택 단계에서 제외한다. 기존 기록의 점수 규칙은 콤보 버전으로 복원한다.
- 플레이어 UI는 기존 타이머 아래에 `PivotGaugeView`가 생성한다. 기존 한글 폰트를 재사용하며 충전은 파란색, 활성은 주황색으로 표시한다. 현재 점수와 최대치 1,000을 함께 표시한다. 종료 시 숨기고 컨트롤러 제거 시 정리한다. 상대 피벗 상태와 점수는 기존 리플레이 상태 텍스트에 표시한다. 양쪽 모두 소수점 한 자리 아래를 버려 미충전 상태가 만충으로 반올림되지 않게 한다.

### 링크 피벗: 육각형 이웃 한 겹

- `LinkMatchRule.json`의 `pivotSettings.LinkNeighborClear`가 true이고, 매칭을 처리하는 틱 시작 시 피벗이 활성 상태일 때만 적용한다. 게이지를 채워 처음 발동시킨 매칭에는 소급 적용하지 않는다.
- 유효한 원래 연결 경로의 각 칸에 대해 `GridPos.IsAdjacentHexagon`과 동일한 6방향 이웃을 색상·입력 플래그와 무관하게 추가한다. 보드 밖·누락·빈 셀·Close/Lock 셀은 제외한다. 일반/생성기 셀의 블럭만 대상이다.
- 추가 대상에서는 다시 이웃을 찾지 않는다. 원래 경로와 이웃 중복은 제거하고 경로 순서 → 고정 좌표 오프셋 순서로 수집한다. 해시 집합은 중복 검사에만 사용한다.
- `LinkPivotEffect`는 대상 수집만 담당한다. 링크 보드가 원래 경로와 추가 대상을 같은 파괴 연출 순서로 처리하며 점수·수집·폭탄 기믹을 기존 경로로 적용한다. 포함된 폭탄의 고유 연쇄 범위는 한 겹 밖으로 이어질 수 있다.
- 링크가 짧아 매칭에 실패했거나, 피벗 비활성/스테이지/사각형 보드이면 추가 효과가 없다. 3매치·탭 매치 효과는 아직 추가하지 않았다.
- `Dispose()`는 매칭 확장 구독을 해제하고 피벗을 정지한다. 게임 종료, 상대 재초기화, 컨트롤러 제거 시 호출한다.
- 새 기록은 `LinkNeighborClear`와 `comboRuleVersion: 1`을 저장한다. 해당 효과 필드가 없는 기존 기록은 이웃 파괴 없이 재생하고, 콤보 버전이 없는 기록은 당시 자동 매칭/파괴 기준 콤보로 재생한다.
- 대전 상대는 현재 콤보 버전과 Rule JSON의 피벗 설정 전체가 같은 완주 기록만 선택한다. 다른 규칙의 기존 기록은 재생할 수 있지만 승패 비교에서는 제외한다. 호환 기록이 없으면 첫 기록 도전으로 진행한다.
