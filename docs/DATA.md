# PuzleBattleGame 데이터/설정 참고 문서

JSON 규칙, 스테이지, GameSpec, 블럭 데이터 관련 작업 시 참고.

## 콘텐츠와 C# 매핑

[전체 작업 지도](MAP.md) · [문서 목록](README.md)

파일 링크는 이 문서 기준 상대 경로이며, 마지막 열의 이름을 파일 안에서 검색하면 구현 위치를 찾을 수 있습니다.

| 찾는 콘텐츠 | 담당 C# 파일 | 주요 메서드·타입·멤버 |
|---|---|---|
| Rule/Stage 로드·사양 병합·시드 생성 | [StageInjection.cs](../Assets/02_Scripts/StageInjection.cs) | `MakeGameSpec`, `GetGameSpec`, `SetReplayData` |
| 게임 사양·스테이지·셀 데이터 정의 | [GameSpec.cs](../Assets/02_Scripts/PuzzleCore/Module/GameSpec.cs) | `GameSpec`, `GameSpec.mode`, `GameSpec.pivotSettings`, `UseLegacyAutomaticCombos`, `BattleFrameCount`, `StageData`, `CellData`, `GetBlock` |
| 규칙·블럭·목표 데이터, enum, 좌표, 입력 기록 | [PuzzleDefine.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzleDefine.cs) | `GameRuleContainer`, `RuleData`, `BlockData`, `ObjectiveData`, `GridPos`, `InputRecord`, `InputEndRecord` |
| 리플레이 저장 형식 | [ReplayData.cs](../Assets/02_Scripts/PuzzleCore/Module/ReplayData.cs) | `ReplayData`, `IsCompleteBattleRecord`, `HasCompatibleBattleRules`, `pivotSettings`, `comboRuleVersion` |
| Rule JSON 및 리플레이에 보관하는 피벗 규칙 | [PivotSettings.cs](../Assets/02_Scripts/Battle/Pivot/PivotSettings.cs) | `PivotSettings`, `IsValid`, `CopyValidated`, `HasSameRules` |
| 리플레이 JSON 저장·로드·저장 경로 | [ReplayStorage.cs](../Assets/02_Scripts/PuzzleCore/Module/ReplayStorage.cs) | `Save`, `Load`, `GetReplayDirectoryPath` |

---

## 데이터 흐름

`ReplayData.pivotSettings`에는 플레이 시작 시 검증한 피벗 설정 복사본을 저장한다. 재생은 기록된 설정을 사용하고, 해당 필드가 없는 기존 기록은 기본값으로 피벗 게이지를 계산한다. 입력 저장 형식의 `formatVersion`은 유지하고, 콤보 판정 차이는 `comboRuleVersion`으로 구분한다. 설정 항목은 [피벗 모드](INGAME.md#대전-전용-피벗-모드)를 참고한다.

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

## Rule JSON 구조

파일 위치: `Assets/05_Table/Rule/`
- `ThreeMatchRule.json` — 3매치 규칙
- `TapMatchRule.json` — 탭 매치 규칙
- `LinkMatchRule.json` — 링크 매치 규칙

```json
{
  "rule": {
    "ruleId": "ThreeMatch_Quadrangle",
    "puzzleType": 1,
    "boardShape": 1,
    "timeLimit": 0,
    "objectives": [
      { "type": 0, "count": 1000 },
      { "type": 1, "targetId": "100-1", "count": 20 }
    ]
  },
  "blocks": [
    {
      "blockId": "100-1",
      "inputType": [ "Swap", "Touch" ],
      "destroyType": 2,
      "life": 1
    },
    {
      "blockId": "200-1",
      "inputType": [ "Swap", "Touch" ],
      "destroyType": 51,
      "life": 1,
      "gimmickIds": [ "Bomb" ]
    }
  ]
}
```

> JSON에는 `_comment*` 형태의 설명 필드를 자유롭게 넣을 수 있다. `JsonUtility`는 매칭되지 않는 키를 무시하므로 파싱에 영향 없음.

### puzzleType 값
| 값 | 모드 | 보드 클래스 |
|----|------|------------|
| 1 | ThreeMatch | ThreeMatchPuzzleBoard |
| 2 | Link | LinkPuzzleBoard |
| 3 | TapMatch | TapMatchPuzzleBoard |

### 피벗 게임 데이터

다음 세 파일의 최상위 `pivotSettings`를 수정한다. 이미 등록된 Rule Addressable을 `AssetManager`로 읽으므로 별도 리소스 등록은 필요 없다.

- [ThreeMatchRule.json](../Assets/05_Table/Rule/ThreeMatchRule.json): 3매치
- [LinkMatchRule.json](../Assets/05_Table/Rule/LinkMatchRule.json): 링크
- [TapMatchRule.json](../Assets/05_Table/Rule/TapMatchRule.json): 탭 매치

```json
"pivotSettings": {
  "Capacity": 1000,
  "ChargePerBlockUnits": 10,
  "ChargeComboStep": 10,
  "ChargeBonusPerStepUnits": 1,
  "DrainPerSecond": 10,
  "DrainReductionPercentPerCombo": 25,
  "MinimumDrainPercent": 25,
  "ComboWindowTicks": 50,
  "LinkNeighborClear": true
}
```

`Capacity`는 최대 점수, `DrainPerSecond`는 초당 소비 점수다. `Units`로 끝나는 충전량은 0.01점 단위이므로 `ChargePerBlockUnits: 10`은 블럭당 0.1점(10개당 1점)이다. 콤보가 `ChargeComboStep`만큼 증가할 때마다 블럭당 `ChargeBonusPerStepUnits`를 더한다. 기본값에서 10콤보는 블럭당 0.11점, 20콤보는 0.12점이다. 기본 충전량을 바꿀 때 배율을 유지하려면 추가 충전량도 함께 조정한다.

`DrainReductionPercentPerCombo`는 빠른 콤보마다 감소하는 기본 소비 속도의 퍼센트포인트, `MinimumDrainPercent`는 최소 소비 비율이다. `ComboWindowTicks`는 초당 50틱 기준이므로 50은 1초다. 모든 값은 양수이고 소비 비율은 1~100이다. 자세한 판정 규칙은 [피벗 모드](INGAME.md#대전-전용-피벗-모드)를 참고한다.

`LinkNeighborClear`는 링크 JSON에서만 true이며, 피벗 중 육각형 링크 경로의 이웃 한 겹을 추가 파괴한다. 3매치·탭 매치는 false다. 이 필드가 없는 기존 리플레이 설정은 false로 읽어 당시 점수와 파괴 범위를 유지한다. 콤보는 유저 조작의 매칭 성공 기준이며 자동 연쇄는 횟수·시간 갱신에서 제외한다.

대전 시작 시 검증한 복사본을 사용하며, 게임 도중 원본 설정을 바꿔도 현재 게임에는 적용하지 않는다. 새 대전은 수정한 JSON을 사용하고 상대 리플레이는 기록된 `pivotSettings`를 우선 사용한다. 기존 기록에 필드가 없을 때만 이전 기본값을 사용한다.

### inputType 값 (문자열 목록 → InputType 플래그)
JSON에는 **가독성을 위해 문자열 목록**으로 두고, 게임 로직에서는 `Block` 생성자가 `Enum.TryParse`로 `InputType` 플래그(int)로 변환한다. 보드는 이 플래그로 조작 가능 여부를 게이팅한다.

| 문자열 | InputType 플래그 | 의미 |
|--------|:---:|------|
| `"Swap"` | 1 | 위치 바꾸기 (ThreeMatch) |
| `"Link"` | 2 | 연결하기 (Link) |
| `"Touch"` | 4 | 터치(클릭)하기 (TapMatch) |

조합 예: `["Swap","Touch"]` → `Swap|Touch`(5). `inputType`은 **조작 방법**일 뿐이며, 폭탄 같은 **행동은 `gimmickIds`로 별도 지정**한다.

### gimmickIds 값 (문자열 목록 → GimmickType → 기믹 부착)
블럭에 부착할 기믹을 문자열 목록으로 지정한다. `PuzzleBlockFactory`가 `Enum.TryParse`로 `GimmickType`(enum) 변환 후 `GimmickFactory`로 기믹을 생성해 `Block`에 부착한다.

| 문자열 | GimmickType | 동작 |
|--------|:---:|------|
| `"Bomb"` | 1 | 원형 폭탄. 파괴 시 주변 반경 2칸(유클리드 거리) 블럭 연쇄 파괴 |

> **문자열 ↔ enum 변환 정리**: `JsonUtility`는 `inputType`/`gimmickIds`를 `List<string>`으로 역직렬화만 한다. 문자열 → enum 변환은 `Block`(inputType) / `PuzzleBlockFactory`(gimmickIds)에서 `Enum.TryParse`로 수행한다. JsonUtility 자체는 enum 변환을 하지 않으므로, 새 값 추가 시 enum 정의(`PuzzleDefine.cs`)와 팩토리 분기를 함께 갱신해야 한다.

---

## Stage JSON 구조

파일 위치: `Assets/05_Table/Stage/Stage.json`

```json
{
  "stage_id": 1,
  "stage_width": 8,
  "stage_height": 8,
  "cells": [
    {
      "x": 0, "y": 0,
      "block_id": "100-1",
      "panel_id": 0,
      "cell_type": 1,
      "generator_block_ids": []
    },
    {
      "x": 0, "y": 7,
      "block_id": null,
      "panel_id": 0,
      "cell_type": 3,
      "generator_block_ids": ["100-1", "100-2", "100-3"]
    }
  ]
}
```

### cell_type 값
| 값 | 종류 | 설명 |
|----|------|------|
| 0 | Close | 비활성 셀 (블럭 배치 불가) |
| 1 | Normal | 일반 셀 |
| 2 | Lock | 잠금 셀 |
| 3 | Generator | 블럭 생성기 (보통 최상단 행) |

---

## 새 규칙 추가 방법

1. `Assets/05_Table/Rule/`에 새 JSON 파일 생성 (기존 형식 참고)
2. `puzzleType`에 새 값 할당
3. `PuzzleDefine.cs`의 `PuzzleType` 열거형에 값 추가
4. `IPuzzleBoard` 구현 클래스 작성 (`INGAME.md` 참고)
5. `PuzzleGameController.Start()`에서 PuzzleType별 분기 추가
6. Addressable에 JSON 등록

## 새 스테이지 추가 방법

1. `Assets/05_Table/Stage/Stage.json` 수정 또는 새 JSON 생성
2. `cells` 배열에 보드 크기만큼 셀 데이터 작성
3. 최상단 행은 `cell_type: 3` (Generator) + `generator_block_ids` 설정
4. `block_id`로 초기 블럭 배치 (null이면 빈 셀)

## 새 블럭 추가 방법

1. Rule JSON의 `blocks` 배열에 새 BlockData 추가
2. `blockId` 고유 값 지정
3. `inputType` 조작 방법 문자열 목록 설정 (예: `["Swap","Touch"]`)
4. 특수 행동이 필요하면 `gimmickIds`에 기믹 문자열 추가 (예: `["Bomb"]`).
   - **새 기믹 타입**이면 `PuzzleDefine.cs`의 `GimmickType` enum 값 + `GimmickFactory` 분기 + (`GimmickBase` 상속) 기믹 클래스를 함께 추가 → 상세: `INGAME.md` "새 블럭(기믹) 추가 절차"
   - 일반 색 블럭은 코드 변경 없이 JSON 추가만으로 동작
5. 블럭 스프라이트를 `Assets/04_Resources/Block/`에 추가 후 Addressable 등록

---

## ReplayData JSON 구조

파일 위치:
- **에디터**: `Assets/05_Table/Replay/replay_{timestamp}.json`
- **빌드**: `Application.persistentDataPath/Replay/replay_{timestamp}.json`

게임 종료 시 `ReplayStorage.Save()`에 의해 자동 생성됨.

```json
{
    "formatVersion": 1,
    "mode": 1,
    "timeLimit": 60,
    "frameCount": 3000,
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
| `timeLimit` | float | 기록 당시 제한 시간(초) |
| `frameCount` | ulong | 실제 완료한 총 틱 수. 대전은 3,000 |
| `finalScore` | int | 재생 결과와 비교할 최종 점수 |
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
- `Battle`은 제한 시간 60초, 완료 틱 수 3,000인 현재 형식의 기록만 상대로 선택한다. `Stage` 기록이나 이전 형식은 대전 기록으로 변환하지 않는다.
- `ReplayStorage.Load()`에서 형식 버전, 모드, 시간, 완료 기록을 검사한다. 향후 알 수 없는 버전은 거부한다.
- 대전 기록의 `pivotSettings`가 존재하면 유효 범위도 검사한다. 잘못된 스냅샷은 상대 선택 전에 제외하며, 필드가 없는 기존 기록은 허용한다.
- 새 플레이는 `comboRuleVersion: 1`로 저장한다. 버전 0의 3매치 재생은 자동 매칭도 기존 점수 콤보에 포함하며, 피벗 콤보도 기존의 파괴 발생 기준을 사용한다. 이를 위한 `GameSpec.UseLegacyAutomaticCombos`는 재생 시에만 설정하는 비직렬화 필드다. 알 수 없는 콤보 버전은 로드에서 거부한다.
- `IsCompleteBattleRecord()`는 재생 가능한 완주 기록인지 검사하고, `HasCompatibleBattleRules()`는 현재 점수 조건으로 대전할 수 있는지 별도로 검사한다. `PopupReady.OnClickBattle()`은 현재 콤보 버전과 해당 Rule JSON의 피벗 설정이 모두 같은 상대만 선택한다. 결과 표시 때도 같은 조건을 검사하므로, 다른 규칙의 기록을 직접 재생해도 승패를 비교하지 않는다.
- 동일 주소의 Rule/Stage가 바뀌면 기존 결과와 달라질 수 있다. 종료 틱/점수 검증 실패 시 승패를 비교하지 않는다. 현재 형식은 Rule/Stage 전체 스냅샷을 저장하지 않는다.
- 문서 JSON 예제의 입력과 점수는 필드 설명용이며 실제 대전 상대는 게임에서 완주하여 저장한 기록을 사용한다.
