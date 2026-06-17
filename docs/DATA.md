# PuzleBattleGame 데이터/설정 참고 문서

JSON 규칙, 스테이지, GameSpec, 블럭 데이터 관련 작업 시 참고.

---

## 데이터 흐름

```
Rule JSON + Stage JSON
  → AssetManager.LoadAsset<TextAsset>(address)
    → JsonUtility.FromJson<T>()
      → StageInjection.MakeGameSpec()
        → GameSpec { StageData, RuleData, List<BlockData> }
          → PuzzleGameController.Start() 에서 소비
            → board.Initialize(gameSpec)
```

**StageInjection**: 싱글톤. JSON 로드 후 `GetGameSpec()`으로 반환. 파싱 실패 시 `false` 반환 + `_gameSpec = null`. `MakeGameSpec`은 두 가지 오버로드를 제공한다.
- `MakeGameSpec(ruleAddress, stageAddress)` — 레거시/메인·리플레이 경로. Rule·Stage 둘 다 **Addressable** 에셋(`AssetManager.LoadAsset<TextAsset>`)으로 로드. `PopupReady`(시작/리플레이)가 사용.
- `MakeGameSpec(ruleAddress, puzzleType, stageId)` — 사이드 스테이지 모드 경로. Rule은 Addressable, Stage는 **`StageStorage`**(Resources/다운로드 경로)로 로드. 아래 "스테이지 저장소" 참고.

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
│       ├─ type         (int) — 0:Score, 1:CollectBlock, 2:ClearCell
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

파일 위치:
- **레거시/메인**: `Assets/05_Table/Stage/Stage.json` (Addressable 등록 에셋)
- **모드별 스테이지**: `Assets/Resources/Stage/{모드}/Stage_{번호:000}.json` (예: `Resources/Stage/ThreeMatch/Stage_001.json`). `StageStorage`로 로드. 아래 "스테이지 저장소" 참고.

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

1. 모드별 스테이지: `Assets/Resources/Stage/{모드}/Stage_{번호:000}.json` 생성 (스테이지 맵 툴 권장). 레거시 메인 경로는 `Assets/05_Table/Stage/Stage.json` 수정
2. `cells` 배열에 보드 크기만큼 셀 데이터 작성
3. 최상단 행은 `cell_type: 3` (Generator) + `generator_block_ids` 설정
4. `block_id`로 초기 블럭 배치 (null이면 빈 셀)
5. `stage_id`는 파일 번호와 일치해야 한다 (맵 툴 검증 규칙)

---

## 스테이지 저장소 (StageStorage)

모드별 스테이지 JSON을 로드하는 정적 저장소(`02_Scripts/StageStorage.cs`). 다운로드 경로 우선, 없으면 Resources에서 로드.

| 항목 | 값 |
|------|-----|
| Resources 루트 | `Stage` |
| 모드 폴더 | ThreeMatch → `ThreeMatch`, Link → `Link`, TapMatch → `TapMatch` |
| 리소스 키 | `Stage/{모드}/Stage_{번호:000}` (예: `Stage/ThreeMatch/Stage_001`) |
| 다운로드 경로 | `Application.persistentDataPath/Stage/{모드}/Stage_{번호:000}.json` |
| 스테이지 번호 범위 | `MinStageId(1)` ~ `MaxStageId(100)` |

- `TryLoadStageJson(puzzleType, stageId, out json)`: 다운로드 파일이 있으면 먼저 읽고, 없으면 `Resources.Load<TextAsset>`로 로드.
- `GetResourceKey` / `TryGetModeFolder` / `GetStageFileName` / `GetDownloadedPath`: 키·경로 생성 헬퍼.
- 지원하지 않는 퍼즐 타입이나 범위를 벗어난 번호는 `Debug.LogError` 후 실패 반환.

---

## 스테이지 맵 툴 (StageMapTool)

스테이지 JSON을 시각적으로 편집/저장하는 에디터 도구. `ToolScene`(SceneEnum 미등록, 에디터 전용)에서 동작. 소스: `02_Scripts/StageMapTool/`.

### Core (`StageMapTool/Core/`)
| 클래스 | 역할 |
|--------|------|
| `StageMapToolState` | 현재 편집 상태(퍼즐 타입, `StageId`, `RuleAddress`, `StageData`, `Brush`) 보유. `PaintCell(x,y)`로 선택 셀에 브러시 적용, `GetCell(x,y)` 조회 |
| `StageMapCellBrush` | 셀에 칠할 값(`cellType`, `blockId`, `panelId`, `generatorBlockIds`). `CopyFrom`으로 값 복사 |
| `StageMapJsonRepository` | StageData JSON 로드/저장. `LoadOrCreate`(없으면 기본 8×8 생성, 최상단 행 Generator), `SaveToResources`(`Application.dataPath/Resources/Stage/{모드}/`에 저장 후 AssetDatabase.Refresh), `SaveToDownloaded`, `CreateDefaultStage`. 저장 시 셀을 (y, x) 순으로 정렬해 diff 안정화 |
| `StageMapValidator` | 저장 전 정합성 검증 → `StageMapValidationResult` |
| `StageMapValidationResult` | `errors`/`warnings` 목록, `IsValid()`(오류 0개) |

### Runtime (`StageMapTool/Runtime/`)
| 클래스 | 역할 |
|--------|------|
| `StageMapToolController` | UI 입력을 상태 모듈에 연결하는 MonoBehaviour. `UIButtonGroup`으로 퍼즐 타입/편집 모드 선택 |

- **퍼즐 타입 버튼**: `OnClickPuzzleType(val)` — 인덱스 `0:ThreeMatch, 1:TapMatch, 2:Link`. Rule 주소 `ThreeMatchRule/TapMatchRule/LinkMatchRule`. 맵 데이터가 없으면 `PlayerPrefs`의 마지막 선택값 복원.
- **편집 모드 버튼**: `OnClickEditMode(val)` — `0:Cell, 1:Block, 2:Tile`. 선택 모드의 하위 패널만 활성화.

### 검증 규칙 (StageMapValidator)
- `stage_id`가 파일 번호와 일치해야 함, `stage_width/height > 0`.
- 셀 좌표 범위/중복 검사, 셀 개수 = `width × height`.
- `Close` 셀은 `block_id`·생성 목록이 비어 있어야 함.
- `block_id`/`generator_block_ids` 항목은 현재 Rule의 `blocks[]`에 존재해야 함.
- `Generator` 셀은 `generator_block_ids`가 비면 안 됨.

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
| `ruleAddress` | string | 규칙 JSON의 Addressable 에셋 주소 |
| `stageAddress` | string | 스테이지 JSON의 Addressable 에셋 주소 |
| `randomSeed` | int | 게임에 사용된 난수 시드 |
| `inputs` | List | 유저 입력 기록 (프레임 + 그리드 좌표) |
| `inputEnds` | List | 유저 입력 종료 기록 (프레임) |
| `recordedAt` | string | 기록 일시 (ISO 8601) |
