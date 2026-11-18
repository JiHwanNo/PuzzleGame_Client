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
| 3매치 스왑·매칭·낙하·셔플 | [ThreeMatchPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/ThreeMatchPuzzleBoard.cs) | `ProcessSwapInput`, `FindMatches`, `ProcessFallingAndFilling`, `FillFromGenerator`, `CheckAndShuffleIfNoMoves`, `MatchTargetsExpanding` |
| 드래그 링크 성립 기준·연결·해제·낙하·매칭 대상 확장 | [LinkPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/LinkPuzzleBoard.cs) | `MinimumLinkLength`, `Input`, `InputEnd`(큐 등록), `FixedUpdate`, `UpdateState`, `ProcessFallingAndFilling`, `FillFromGenerator`, `HasEmptyCell`, `MatchTargetsExpanding` |
| 탭 매치 연결 블럭 탐색·파괴 | [TapMatchPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/TapMatchPuzzleBoard.cs) | `Input`, `InputEnd`(큐 등록), `FixedUpdate`, `UpdateState`, `GetConnectedBlocks`, `FillFromGenerator`, `CheckAndShuffleIfNoMoves`, `MatchTargetsExpanding` |
| 블럭 상태·입력 플래그·기믹 발화 | [Block.cs](../Assets/02_Scripts/PuzzleCore/Module/Block/Block.cs) | `Block`, `GetInputType`, `FireDestroyed` |
| 블럭 생성·기믹 부착 | [PuzzleBlockFactory.cs](../Assets/02_Scripts/PuzzleCore/Module/Block/PuzzleBlockFactory.cs) | `PuzzleBlockFactory` |
| 기믹 계약·호스트·기본 클래스 | [IGimmick.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/IGimmick.cs) | `IGimmick`, `IGimmickHost`, `GimmickBase` |
| 폭탄 범위·연쇄 파괴 | [BombGimmick.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/BombGimmick.cs) | `Radius`, `OnDestroyed` |
| 기믹 종류별 생성 | [GimmickFactory.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/GimmickFactory.cs) | `Create` |
| 공통 파괴·목표 갱신·뷰 액션 등록 | [GimmickUtil.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/GimmickUtil.cs) | `DestroyBlock` |
| 셀 상태와 블럭 보관 | [PuzzleCell.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzleCell.cs) | `PuzzleCell` |
| 바닥 패널 기본 타입 | [PuzzlePanel.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzlePanel.cs) | `PuzzlePanel`, `NormalPanel` |
| 점수·콤보·피버·시간 제한·목표 판정·누적 파괴/조작 성공 수 | [ObjectiveManager.cs](../Assets/02_Scripts/PuzzleCore/Module/ObjectiveManager.cs) | `OnMatchEvent`, `OnBlockDestroyed`, `UpdateTimer`, `ShouldFinish`, `IsAllObjectivesCleared`, `TotalDestroyedBlocks`, `TotalPlayerMatches`, `IsValidTimeLimit`, `GetTimeLimitFrames`, `RemainingSeconds` |
| 보드 배치·좌표 변환·뷰 액션 순서·링크 성립 표시·파괴 파편·폭탄 충격파 | [PuzzleBoardView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleBoardView.cs) | `DrawBoard`, `GetLocalPos`, `ExecuteBatchMovement`, `UpdateLineRenderer`, `CacheLinkPathIfChanged`, `AnimateLinkTip`, `PlayLinkRing`, `PlayMatchFragments`, `PlayBombWaves` |
| 블럭 스프라이트·클릭/이동/낙하/파괴 연출 | [PuzzleBlockView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleBlockView.cs) | `UpdateVisual`, `PlayMoveAnimation`, `PlayFallAnimation`, `PlayDestroyAnimation` |
| 셀 색상·상태 시각화 | [PuzzleCellView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleCellView.cs) | `Initialize`, `UpdateVisual` |
| 입력 충돌체와 블럭 뷰 연결 | [PuzzleBlockCollider.cs](../Assets/02_Scripts/PuzzleCore/PuzzleBlockCollider.cs) | `PuzzleBlockCollider` |
| 입력 충돌체와 셀 뷰 연결 | [PuzzleCellCollider.cs](../Assets/02_Scripts/PuzzleCore/PuzzleCellCollider.cs) | `PuzzleCellCollider` |
| 리플레이 보드 생성·프레임 입력 재생·축소 배치 | [ReplayController.cs](../Assets/02_Scripts/PuzzleCore/Controller/ReplayController.cs) | `Initialize`, `AdvanceFrame`, `HasValidResult`, `HasCompatibleBattleRules`, `UpdateStatusPosition`, `AlignToTopRight` |
| 시드 기반 난수 | [PuzzleRandom.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzleRandom.cs) | `Next`, `NextDouble`, `SetSeed` |
| 대전 피벗 충전·활성·콤보 소비 감속 | [PivotMode.cs](../Assets/02_Scripts/Battle/Pivot/PivotMode.cs) | `AdvanceTick`, `Stop`, `GaugeUnits`, `GaugePoints`, `GaugeRatio`, `IsActive`, `ComboCount`, `CurrentDrainPercent` |
| 피벗 충전량·소비량·콤보 간격 설정 | [PivotSettings.cs](../Assets/02_Scripts/Battle/Pivot/PivotSettings.cs) | `PivotSettings`, `IsValid`, `CopyValidated`, `HasSameRules` |
| 보드 통계·모드별 효과와 독립 피벗 모듈 연결 | [BattlePivotSession.cs](../Assets/02_Scripts/Battle/Pivot/BattlePivotSession.cs) | `BattlePivotSession`, `AdvanceAfterBoardTick`, `Dispose` |
| 세 모드 피벗의 이웃 한 겹 추가 파괴 | [LinkPivotEffect.cs](../Assets/02_Scripts/Battle/Pivot/LinkPivotEffect.cs) | `LinkPivotEffect`, `ExpandTargets`, `ExpandSquareTargets`, `ExpandNeighbors` |
| 플레이어 피벗 게이지 UI | [PivotGaugeView.cs](../Assets/02_Scripts/Battle/Pivot/PivotGaugeView.cs) | `PivotGaugeView`, `Refresh`, `Dispose` |

규칙/좌표/입력 기록 타입과 리플레이 파일 저장은 [DATA.md](DATA.md)의 코드 표를 함께 확인하세요.

## 게임 루프

### Controller (PuzzleGameController)
- `Update()`: 마우스/터치 입력 → `Physics2D.OverlapPoint` → `PuzzleBlockCollider` → `board.Input(gridPos)`
- 포인터 릴리즈 시 `board.InputEnd()` 호출
- `FixedUpdate()`: `board.FixedUpdate()` 후 `replayController.AdvanceFrame()` 호출. 양쪽 입력 판정·타이머·상태 전환을 동일한 공통 틱에서 진행
- `boardView.IsAnimating` 동안에는 포인터 아래 블럭을 새로 선택하지 않는다. 포인터 해제는 별도로 감지해 `InputEnd()`를 전달한다.

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

## 점수 및 콤보 (ObjectiveManager)

- 콤보는 **유저 조작으로 성공한 매칭 횟수**다. 3매치는 성공한 교환의 첫 매칭만, 링크는 3개 이상 연결 확정만, 탭 매치는 2개 이상 그룹 파괴 성공만 한 번씩 센다.
- 초기 보드 자동 매칭, 낙하 후 자동 연쇄, 폭탄과 피벗 추가 파괴는 별도 콤보를 올리거나 유지 타이머를 갱신하지 않는다. 점수·수집·파괴 개수는 정상 반영한다.
- `TotalPlayerMatches`는 피버 중에도 유저 성공 횟수를 기록한다. 피벗은 이 누적값과 `TotalDestroyedBlocks`를 분리해서 전달받는다.
- 새 점수는 배율을 1/5 단위 정수로 표현하고 `long` 중간값에서 곱셈·절삭한다. 점수·수집·파괴·유저 성공 누적값은 `int.MaxValue`에서 포화하며 음수 기본 점수는 거부한다. 기존 기록의 점수 계산은 `numericRuleVersion: 0`일 때만 실수 배율·절삭으로 복원한다.
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

[뷰 동기화와 리플레이·대전](INGAME-VIEW-AND-BATTLE.md)에서 상세 내용을 확인한다.
