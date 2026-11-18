# PuzleBattleGame 데이터/설정 참고 문서

JSON 규칙, 스테이지, GameSpec, 블럭 데이터 관련 작업 시 참고.

## 콘텐츠와 C# 매핑

[전체 작업 지도](MAP.md) · [문서 목록](README.md)

파일 링크는 이 문서 기준 상대 경로이며, 마지막 열의 이름을 파일 안에서 검색하면 구현 위치를 찾을 수 있습니다.

| 찾는 콘텐츠 | 담당 C# 파일 | 주요 메서드·타입·멤버 |
|---|---|---|
| Rule/Stage 로드·사양 병합·시드 생성 | [StageInjection.cs](../Assets/02_Scripts/StageInjection.cs) | `MakeGameSpec`, `GetGameSpec`, `SetReplayData` |
| 게임 사양·스테이지·셀 데이터 정의 | [GameSpec.cs](../Assets/02_Scripts/PuzzleCore/Module/GameSpec.cs) | `GameSpec`, `GameSpec.mode`, `GameSpec.pivotSettings`, `UseLegacyAutomaticCombos`, `UseRecordedBattleDuration`, `UseLegacyNumericRules`, `BattleFrameCount`, `StageData`, `HasValidCoordinates`, `CellData`, `GetBlock` |
| 규칙·블럭·목표 데이터, enum, 좌표, 입력 기록 | [PuzzleDefine.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzleDefine.cs) | `GameRuleContainer`, `RuleData`, `BlockData`, `ObjectiveData`, `GridPos`, `InputRecord`, `InputEndRecord` |
| 리플레이 저장 형식 | [ReplayData.cs](../Assets/02_Scripts/PuzzleCore/Module/ReplayData.cs) | `ReplayData`, `IsCompleteBattleRecord`, `HasCompatibleBattleRules`, `pivotSettings`, `comboRuleVersion`, `numericRuleVersion` |
| Rule JSON 및 리플레이에 보관하는 피벗 규칙 | [PivotSettings.cs](../Assets/02_Scripts/Battle/Pivot/PivotSettings.cs) | `PivotSettings`, `IsValid`, `CopyValidated`, `HasSameRules` |
| 리플레이 JSON 저장·로드·저장 경로 | [ReplayStorage.cs](../Assets/02_Scripts/PuzzleCore/Module/ReplayStorage.cs) | `Save`, `Load`, `GetReplayDirectoryPath` |

---

## 데이터 흐름

`ReplayData.pivotSettings`에는 플레이 시작 시 검증한 피벗 설정 복사본을 저장한다. 재생은 기록된 설정을 사용하고, 해당 필드가 없는 기존 기록은 기본값으로 피벗 게이지를 계산한다. 입력 저장 형식의 `formatVersion`은 유지하고, 콤보 판정 차이는 `comboRuleVersion`, 시간·점수 계산 차이는 `numericRuleVersion`으로 구분한다. 설정 항목은 [피벗 모드](INGAME-VIEW-AND-BATTLE.md#대전-전용-피벗-모드)를 참고한다.

```
Rule JSON + Stage JSON
  → AssetManager.LoadAsset<TextAsset>(address)
    → JsonUtility.FromJson<T>()
      → StageInjection.MakeGameSpec()
        → GameSpec { StageData, RuleData, List<BlockData>, pivotSettings }
          → PuzzleGameController.Start() 에서 소비
            → board.Initialize(gameSpec)
```

**StageInjection**: 싱글톤. `MakeGameSpec(ruleAddress, stageAddress, mode)`로 JSON 로드 후 `GetGameSpec()`으로 반환한다. `mode` 생략 시 Stage이며, 대전·대전 리플레이 진입은 Battle을 전달한다. 파싱 실패 시 `false` 반환 + `_gameSpec = null`. 대전은 `pivotSettings`가 없거나 유효하지 않아도 같은 방식으로 진입을 중단한다. 스테이지는 대전 전용 설정의 누락·범위 오류를 검사하지 않는다.

### 데이터 타입 주의 (struct vs class)
| 타입 | 종류 | `== null` 가능 | 비고 |
|------|------|:---:|------|
| `GameRuleContainer` | class | O | JSON 파싱 실패 시 null 반환 |
| `RuleData` | **struct** | X | 파싱 실패 시 기본값(zero) — 컨테이너(`GameRuleContainer`) null 체크로 대체 |
| `ObjectiveData` | **struct** | X | |
| `GameSpec` | class | O | |
| `StageData` | class | O | JSON 파싱 실패 시 null 반환 |
| `CellData` | class | O | |
| `BlockData` | class | O | |
| `InputRecord` | **struct** | X | |
| `InputEndRecord` | **struct** | X | |
| `ReplayData` | class | O | |

---

## GameSpec 구조

```
GameSpec
├─ mode (GameMode: 0=Stage, 1=Battle)
├─ pivotSettings (PivotSettings) — Rule JSON에서 전달한 대전 전용 피벗 규칙
├─ StageData
│   ├─ stage_id        (int) — 스테이지 번호
│   ├─ stage_width     (int) — 보드 가로 크기
│   ├─ stage_height    (int) — 보드 세로 크기
│   └─ List<CellData>
│       ├─ x, y                  (int) — 셀 좌표
│       ├─ block_id              (string) — 초기 블럭 ID (null이면 비어있음)
│       ├─ panel_id              (int) — 바닥 패널 종류
│       ├─ cell_type             (int) — CellType 열거형 값
│       └─ generator_block_ids   (List<string>) — Generator 셀의 생성 블럭 목록
│
├─ RuleData
│   ├─ ruleId          (string) — 규칙 식별자
│   ├─ puzzleType      (int) — 1:ThreeMatch, 2:Link, 3:TapMatch
│   ├─ boardShape      (int) — 1:Quadrangle, 2:Hexagon
│   ├─ timeLimit       (float) — 제한 시간 (초), 0이면 무제한
│   └─ List<ObjectiveData>
│       ├─ type         (int) — 0:Score, 1:CollectBlock
│       ├─ targetId     (string) — 대상 blockId (CollectBlock일 때)
│       └─ count        (int) — 목표 값
│
├─ randomSeed        (int) — 결정론적 리플레이를 위한 난수 시드 (StageInjection에서 자동 생성)
│
└─ List<BlockData>
    ├─ blockId         (string) — 블럭 식별자 (예: "100-1")
    ├─ inputType       (List<string>) — 조작 방법 문자열 목록 (예: ["Swap","Touch"]) → InputType 플래그로 변환
    ├─ destroyType     (int) — 파괴 방식
    ├─ life            (int) — 내구도
    └─ gimmickIds      (List<string>) — 부착할 기믹 id 목록 (예: ["Bomb"]). 없거나 비어있으면 기믹 없음
```

---

[규칙과 스테이지 데이터](RULES-AND-STAGES.md)에서 상세 내용을 확인한다.

## ReplayData JSON 구조

파일 위치:
- **에디터**: `Assets/05_Table/Replay/replay_{timestamp}.json`. 플레이 기록과 Unity가 생성한 짝 파일(`.json.meta`)은 프로젝트에 누적되며 Git에서 제외한다.
- **빌드**: `Application.persistentDataPath/Replay/replay_{timestamp}.json`

게임 종료 시 `ReplayStorage.Save()`에 의해 자동 생성됨.

```json
{
    "formatVersion": 1,
    "numericRuleVersion": 1,
    "mode": 1,
    "timeLimit": 90,
    "frameCount": 4500,
    "finalScore": 30,
    "ruleAddress": "LinkMatchRule",
    "stageAddress": "Stage",
    "randomSeed": 2095364872,
    "inputs": [
        { "frame": 67, "position": { "X": 3, "Y": 6 } },
        { "frame": 90, "position": { "X": 3, "Y": 5 } }
    ],
    "inputEnds": [
        { "frame": 115 },
        { "frame": 240 }
    ],
    "recordedAt": "2026-04-01T20:37:34+09:00"
}
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `formatVersion` | int | 현재 1. 없으면 이전 형식 0으로 읽음 |
| `comboRuleVersion` | int | 현재 1은 유저 조작 성공만 콤보로 계산. 없는 기존 기록의 0은 당시 자동 매칭/파괴 포함 규칙을 복원 |
| `mode` | GameMode | Stage(0), Battle(1) |
| `timeLimit` | float | 기록 당시 제한 시간(초). 유한·음이 아닌 값이며 정수 틱 범위 이내 |
| `numericRuleVersion` | int | 현재 1: 시간 표현 오차 보정·정수 점수. 누락된 기존 기록의 0: 이전 float 계산 복원 |
| `frameCount` | ulong | 실제 완료한 총 틱 수. 대전은 4,500 |
| `finalScore` | int | 재생 결과와 비교할 최종 점수. 새 규칙은 int.MaxValue에서 포화 |
| `pivotSettings` | PivotSettings | 대전 시작 당시 피벗 설정 복사본. 없는 기존 기록은 기본값 사용 |
| `ruleAddress` | string | 규칙 JSON의 Addressable 에셋 주소 |
| `stageAddress` | string | 스테이지 JSON의 Addressable 에셋 주소 |
| `randomSeed` | int | 게임에 사용된 난수 시드 |
| `inputs` | List | 유저 입력 기록 (프레임 + 그리드 좌표) |
| `inputEnds` | List | 유저 입력 종료 기록 (프레임) |
| `recordedAt` | string | 기록 일시 (ISO 8601) |

### 리플레이 입력 순서
- 새 `InputRecord`와 `InputEndRecord`는 공통 증가 순서인 `ulong sequence`를 저장한다. 같은 고정 틱 안에서 좌표 입력과 손 떼기 이벤트가 섞여도 원래 순서로 복원한다.
- 기록 프레임은 입력 판정을 실행한 고정 틱이며 0부터 시작한다. 이전 형식의 기록은 읽을 수 있지만 이전 가변 프레임 게임 결과의 완전한 재현은 보장하지 않는다.

### 목표 종류와 셀 속성
- 목표는 `Score`(0), `CollectBlock`(1)을 지원한다. 그 외 목표 값은 달성으로 판정하지 않는다.
- 맵의 비활성 영역은 `CellType.Close`(0), 로직상 잠긴 셀은 `CellType.Lock`(2)이다. 셀 속성과 목표 종류는 별개의 데이터다.

### 대전 기록 호환성
- `Battle`은 제한 시간 90초, 완료 틱 수 4,500인 현재 형식의 기록만 상대로 선택한다. `Stage` 기록이나 이전 형식은 대전 기록으로 변환하지 않는다.
- `ReplayStorage.Load()`에서 형식 버전, 모드, 시간, 완료 기록을 검사한다. 향후 알 수 없는 버전은 거부한다.
- 대전 기록의 `pivotSettings`가 존재하면 유효 범위도 검사한다. 잘못된 스냅샷은 상대 선택 전에 제외하며, 필드가 없는 기존 기록은 허용한다.
- 새 플레이는 `comboRuleVersion: 1`로 저장한다. 버전 0의 3매치 재생은 자동 매칭도 기존 점수 콤보에 포함하며, 피벗 콤보도 기존의 파괴 발생 기준을 사용한다. 이를 위한 `GameSpec.UseLegacyAutomaticCombos`는 재생 시에만 설정하는 비직렬화 필드다. 알 수 없는 콤보 버전은 로드에서 거부한다.
- `IsCompleteBattleRecord()`는 재생 가능한 완주 기록인지 검사하고, `HasCompatibleBattleRules()`는 현재 점수 조건으로 대전할 수 있는지 별도로 검사한다. `LobbyMain.PrepareBattle()`은 슬롯에서 선택한 Rule의 기록 중 현재 콤보·수치 계산 버전과 해당 Rule JSON의 피벗 설정이 모두 같은 상대만 선택한다. 결과 표시 때도 같은 조건을 검사하므로, 다른 규칙의 기록을 직접 재생해도 승패를 비교하지 않는다.
- 동일 주소의 Rule/Stage가 바뀌면 기존 결과와 달라질 수 있다. 종료 틱/점수 검증 실패 시 승패를 비교하지 않는다. 현재 형식은 Rule/Stage 전체 스냅샷을 저장하지 않는다.
- 문서 JSON 예제의 입력과 점수는 필드 설명용이며 실제 대전 상대는 게임에서 완주하여 저장한 기록을 사용한다.

- 90초 재밸런싱: 매초 10개 파괴·매칭 1회 기준 24/55/86초에 피벗 발동. `SquareNeighborClear`는 3매치·탭 매치에서 true이며 이전 기록의 누락 필드는 false다. 클래스 초기값은 구형 기록용이고 현재 밸런스는 위 JSON을 사용한다.
- `IsCompleteBattleRecord()`는 60초/3,000틱과 90초/4,500틱을 모두 허용하여 구형 기록을 읽을 수 있다. `HasCompatibleBattleRules()`는 90초 기록만 현재 상대로 허용한다. `UseRecordedBattleDuration`은 상대 재생에서만 기록 당시 제한 시간을 복원한다.
