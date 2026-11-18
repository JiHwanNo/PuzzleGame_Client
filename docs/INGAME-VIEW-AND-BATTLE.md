# 뷰 동기화와 리플레이·대전

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
| `blockData` | 생성 또는 파괴한 블럭. 파괴 시 부착된 폭탄의 반경을 읽는다. |

### 애니메이션 시간
| 종류 | 시간 | Ease |
|------|------|------|
| 클릭 | 스케일 1.1x, 0.038초, 2회 yoyo | — |
| 이동 (Move) | 0.075초 | OutBack |
| 낙하 (Fall) | 0.075초 | OutQuad |
| 파괴 (Destroy) | 스케일→0, 0.075초 | InBack |
| 생성 (Create) | 스케일 0→1, 0.075초 | OutBack |
| 액션 간 대기 | 0.019초 | — |

### 매치 파편과 링크 성립 표시

- 파괴 액션을 재생할 때 실제 블럭 스프라이트로 작은 파편 3개를 표시한다. 각 보드에서 최대 24개를 재사용하며 파편이 사라질 때까지 액션 큐를 지연시키지 않는다.
- 링크가 최소 길이 3개에 도달하면 세 번째 블럭에 원이 한 번 퍼지고 연결선이 굵어진다. 2개로 줄이면 원을 숨기고 기본 굵기로 돌아간다. 모델과 뷰는 같은 성립 기준을 사용한다.

### 폭탄 충격파

- 폭탄의 파괴 액션이 재생되면 그 좌표에서 주황색 원이 0.24초 동안 기믹의 셀 단위 반경까지 퍼진다. 연쇄 폭탄마다 자기 위치에서 원을 재생하며 다음 파괴와 낙하는 기존 순서로 진행한다.
- 충격파는 보드별로 선 렌더러를 재사용한다. 원본 PNG 없이 플레이어·리플레이 보드의 로컬 좌표에 그린다.

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

## 수치 경계와 시간 변환

- `ObjectiveManager.IsValidTimeLimit`는 NaN·Infinity·음수와 정수 틱 범위 초과를 거부한다. 0은 무제한이다. 새 규칙의 최대 입력 float는 42,949,672초이며 2,147,483,600틱으로 정확히 변환된다.
- `GetTimeLimitFrames`는 double로 초×50을 계산한다. 정수 틱과의 차이가 float 입력의 상대 표현 오차 `2^-24` 이내면 해당 정수로 보정하고, 나머지 부분 틱은 올림한다. 따라서 0.3초는 15틱, 0.301초는 16틱이다. 타이머 표시의 올림은 `RemainingSeconds`가 정수 나눗셈으로 계산한다.
- `numericRuleVersion: 0` 기록은 `UseLegacyNumericRules`로 이전 float 곱셈·올림을 복원하므로 0.3초 기록의 16틱 종료도 유지한다. 새 기록은 버전 1이며 다른 버전은 대전 승패 비교에서 제외한다.
- 맵은 양수 크기와 범위 안의 셀 좌표만 허용한다. 로드와 보드 초기화에서 `StageData.HasValidCoordinates`로 검사한다. 사각형·육각형 인접 판정은 좌표를 long으로 확장한 뒤 뺄셈·절댓값을 계산한다.

## 스테이지와 90초 대전
- `GameSpec.mode`의 기본값은 `Stage`. 목표 전체 달성 또는 Rule의 제한 시간 종료 시 끝난다.
- `Battle`은 목표 달성으로 끝나지 않으며, 50틱/초 × 90초 = 4,500틱을 정확히 진행한다. 양쪽 보드는 동일한 규칙·맵·시드를 사용한다.
- `ObjectiveManager`는 시간을 정수 틱으로 관리하고 표시할 때만 초로 변환한다. 콤보/피버도 기존처럼 논리 틱을 사용한다.
- `GameScene`의 작은 보드 아래에 남은 시간, 입력이 없는 대기 상태, 재생 완료 및 점수를 표시한다. 기존 기록은 `이전 기록`으로 구분한다.
- 대전 종료 시 현재 점수를 비교해 승리/패배/무승부를 4초간 표시한 뒤 로비로 돌아간다. 첫 기록 도전이면 본인 점수만 표시한다.
- 기록의 종료 틱·최종 점수가 재생 결과와 다르면 오류 상태를 표시하고 승패 비교에서 제외한다.
- 결과 UI와 리플레이 상태 UI는 `GameScene.unity`의 `BattleResult`, `ReplayStatus`이며 기존 한글 폰트 에셋을 사용한다.

## 대전 전용 피벗 모드

- 기존 피버와 피벗은 별도의 상태로 동작한다. 콤보 증가는 유저 조작 성공 기준이며 피벗 자체의 추가 점수 배율은 없다. 피벗의 추가 파괴는 기존 블럭 점수 규칙으로 점수를 얻는다.
- `Battle/Pivot/PivotMode`와 `PivotSettings`는 Unity 및 `Puzzle.Core`를 참조하지 않는 순수 C# 모듈이다. 0.01점 단위 정수 게이지와 초당 50 논리 틱으로 동작하며 소수점은 표시할 때만 사용한다.
- `ObjectiveManager.TotalDestroyedBlocks`는 모드에 관계없이 실제 파괴 수를 기록한다. 일반 매칭과 폭탄 연쇄가 모두 포함되며, 단순 이동·생성·셔플은 포함하지 않는다.
- 연결 계층 `BattlePivotSession`은 대전 보드 초기화 직후 생성하고, 보드의 `FixedUpdate()` 직후 `AdvanceAfterBoardTick()`을 한 번 호출한다. 누적 파괴 수와 유저 매칭 성공 횟수의 증가분을 각각 전달하며 액션 큐는 소비하지 않는다. 세 보드에 별도 효과의 매칭 대상 확장 함수를 구독한다.
- `GameMode.Battle`에서만 충전/발동하며 스테이지에서는 세션을 만들거나 피벗 설정을 검증하지 않는다. 플레이어와 상대 리플레이는 세션을 따로 보유한다. 게임 또는 재생 종료 시 `Stop()`으로 게이지와 활성 상태를 정리한다.

| 설정 | 현재 Rule JSON 값 | 의미 |
|---|---|---|
| `Capacity` | 1,000 | 최대 게이지 점수 |
| `ChargePerBlockUnits` | 400 | 블럭당 4점 충전 |
| `ChargeComboStep` | 10 | 충전 보너스가 증가하는 콤보 간격 |
| `ChargeBonusPerStepUnits` | 40 | 10콤보마다 블럭당 0.4점 추가 |
| `DrainPerSecond` | 167 | 기본 유지 약 6초 |
| `DrainReductionPercentPerCombo` | 10 | 빠른 연속 유저 매칭 성공마다 줄이는 기본 소비 속도의 백분율 |
| `MinimumDrainPercent` | 75 | 최소 초당 125.25점 소비, 최대 유지 약 8초 |
| `ComboWindowTicks` | 50 | 유저 매칭 성공을 빠른 콤보로 인정하는 최대 간격, 기본 1초 |
| `LinkNeighborClear` | 링크 JSON만 true | 링크 피벗 중 육각형 이웃 한 겹 추가 파괴. 다른 모드와 기존 기록은 false |
| `SquareNeighborClear` | 3매치·탭 JSON만 true | 원래 매칭의 상하좌우 한 겹 추가 파괴. 기존 기록은 false |

- 충전 중에는 게이지가 감소하지 않는다. 최대치 도달 시 발동하며 초과 충전은 버린다. 발동 틱에는 소비하지 않고 다음 틱부터 소비한다.
- 피벗 콤보는 기존 피버 콤보와 독립적이다. 충전 중부터 직전 유저 매칭 성공과 50틱 이내인 성공을 연속 콤보로 세며, 현재 성공을 포함해 10콤보가 되는 순간부터 충전 보너스를 적용한다. 자동 매칭·폭탄·추가 파괴는 콤보를 늘리거나 시간을 갱신하지 않지만, 충전 중에는 당시 콤보 배율로 파괴량만큼 충전한다.
- 활성 중 블럭 파괴는 게이지를 다시 채우지 않는다. 빠른 연속 유저 매칭 성공마다 소비량이 초당 167 → 150.3 → 133.6 → 125.25점으로 감소한다. 자동 파괴는 감속을 추가하지 않는다. 발동 전 콤보 횟수로 여러 감속 단계를 즉시 적용하지 않는다.
- 50틱 동안 유저 매칭 성공이 없고 다음 틱으로 넘어가면 피벗 콤보는 0으로, 소비 속도는 기본값으로 복귀한다. 게이지 소진 후에는 다시 충전할 수 있으며, 소진 틱의 파괴를 재충전에 중복 사용하지 않는다. 정수 단위 미만 소비량은 다음 틱으로 이월한다.
- 수치는 `Assets/05_Table/Rule/`의 세 Rule JSON 최상위 `pivotSettings`에서 모드별로 조정한다. `AssetManager`로 읽은 `GameRuleContainer` → `StageInjection` → `GameSpec.pivotSettings` → 컨트롤러로 전달한다. Inspector 설정은 사용하지 않는다.
- 대전 준비 시 설정이 없거나 범위를 벗어나면 로그를 남기고 씬 진입 전에 중단한다. 스테이지는 이 설정의 누락·범위 오류 때문에 중단하지 않는다. 구체적인 JSON 예시는 [RULES-AND-STAGES.md](RULES-AND-STAGES.md#피벗-게임-데이터)를 참고한다.
- 실행에는 검증된 복사본을 사용하고 `ReplayData.pivotSettings`에 저장하여 재생에서도 당시 설정을 사용한다. 필드가 없는 기존 기록은 기본값을 사용한다. 잘못된 피벗 설정을 포함한 대전 기록은 로드/상대 선택 단계에서 제외한다. 기존 기록의 점수 규칙은 콤보 버전으로 복원한다.
- 플레이어 UI는 기존 타이머 아래에 `PivotGaugeView`가 생성한다. 기존 한글 폰트를 재사용하며 충전은 파란색, 활성은 주황색으로 표시한다. 현재 점수와 최대치 1,000을 함께 표시한다. 종료 시 숨기고 컨트롤러 제거 시 정리한다. 상대 피벗 상태와 점수는 기존 리플레이 상태 텍스트에 표시한다. 양쪽 모두 정수 몫·나머지로 소수점 한 자리 문자열을 만든다. `GaugeRatio`는 double 중간값으로 계산하고 미충전 값이 float에서 1로 반올림되면 1보다 작은 최댓값으로 제한한다.

### 링크 피벗: 육각형 이웃 한 겹

- `LinkMatchRule.json`의 `pivotSettings.LinkNeighborClear`가 true이고, 매칭을 처리하는 틱 시작 시 피벗이 활성 상태일 때만 적용한다. 게이지를 채워 처음 발동시킨 매칭에는 소급 적용하지 않는다.
- 유효한 원래 연결 경로의 각 칸에 대해 `GridPos.IsAdjacentHexagon`과 동일한 6방향 이웃을 색상·입력 플래그와 무관하게 추가한다. 보드 밖·누락·빈 셀·Close/Lock 셀은 제외한다. 일반/생성기 셀의 블럭만 대상이다.
- 추가 대상에서는 다시 이웃을 찾지 않는다. 원래 경로와 이웃 중복은 제거하고 경로 순서 → 고정 좌표 오프셋 순서로 수집한다. 해시 집합은 중복 검사에만 사용한다.
- `LinkPivotEffect`는 대상 수집만 담당한다. 링크 보드가 원래 경로와 추가 대상을 같은 파괴 연출 순서로 처리하며 점수·수집·폭탄 기믹을 기존 경로로 적용한다. 포함된 폭탄의 고유 연쇄 범위는 한 겹 밖으로 이어질 수 있다.
- 링크가 짧아 매칭에 실패했거나, 피벗 비활성/스테이지/사각형 보드이면 추가 효과가 없다. 3매치·탭 매치는 아래의 사각형 이웃 효과를 적용한다.
- `Dispose()`는 매칭 확장 구독을 해제하고 피벗을 정지한다. 게임 종료, 상대 재초기화, 컨트롤러 제거 시 호출한다.
- 새 기록은 `LinkNeighborClear`와 `comboRuleVersion: 1`을 저장한다. 해당 효과 필드가 없는 기존 기록은 이웃 파괴 없이 재생하고, 콤보 버전이 없는 기록은 당시 자동 매칭/파괴 기준 콤보로 재생한다.
- 대전 상대는 현재 콤보·수치 계산 버전과 Rule JSON의 피벗 설정 전체가 같은 완주 기록만 선택한다. 다른 규칙의 기존 기록은 재생할 수 있지만 승패 비교에서는 제외한다. 호환 기록이 없으면 첫 기록 도전으로 진행한다.

### 90초 대전 충전 밸런스와 사각형 피벗

- 기준 입력은 매초 유저 매칭 1회, 매칭당 10개 파괴다. 실제 상태 머신 검증에서 첫 발동은 24초, 다음 발동은 55초와 86초다. 활성 중 충전하지 않으므로 충전 약 24초와 유지 약 7초를 합쳐 반복 간격 약 31초를 맞춘다. 플레이 속도·자동 연쇄·콤보에 따라 발동 시점은 달라지며 시간만으로 자동 발동하지 않는다.
- `SquareNeighborClear`는 3매치·탭 매치 JSON에서 true, 링크 및 기존 기록에서는 false다. `ThreeMatchPuzzleBoard.MatchTargetsExpanding`과 `TapMatchPuzzleBoard.MatchTargetsExpanding`에 `LinkPivotEffect.ExpandSquareTargets`를 연결한다.
- 3매치는 확정 매칭(자동 연쇄 포함), 탭 매치는 유효한 2개 이상 연결 덩어리의 상하좌우 한 겹을 색상과 무관하게 추가 파괴한다. 대각선, 빈칸, 누락 셀, Close/Lock은 제외한다. 추가 대상에서 다시 확장하지 않으며 폭탄 고유 연쇄만 별도로 이어진다.
- 새 효과가 구독된 보드는 원래 매칭을 Y→X 순으로 수집하고 고정 오프셋 순으로 이웃을 추가한다. 효과가 없는 기존 기록은 기존 파괴 순서를 유지한다. 추가 파괴는 점수·수집에 반영하지만 유저 매칭 횟수를 늘리지 않는다.
- `PivotSettings` 클래스 초기값은 구형 리플레이 복원을 위해 유지한다. 새 플레이 밸런스는 Rule JSON이 기준이다. 두 효과 플래그 모두 스냅샷 복사와 호환성 비교에 포함한다.
- 기존 60초 기록은 `UseRecordedBattleDuration`으로 원래 3,000틱 동안 재생한다. 새 플레이는 항상 90초이며 60초 기록은 상대 선택과 승패 비교에서 제외한다.
