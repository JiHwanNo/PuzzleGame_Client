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

**StageInjection**: 싱글톤. `MakeGameSpec(ruleAddress, stageAddress)`로 JSON 로드 후 `GetGameSpec()`으로 반환. 파싱 실패 시 `false` 반환 + `_gameSpec = null`.

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
    ├─ inputType       (int) — Flags: 1:Swap, 2:Link, 4:Touch (조합 가능, 예: 5=Swap+Touch)
    ├─ destroyType     (int) — 파괴 방식
    └─ life            (int) — 내구도
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
      "inputType": 5,
      "destroyType": 2,
      "life": 1
    },
    {
      "blockId": "100-2",
      "inputType": 1,
      "destroyType": 2,
      "life": 1
    }
  ]
}
```

### puzzleType 값
| 값 | 모드 | 보드 클래스 |
|----|------|------------|
| 1 | ThreeMatch | ThreeMatchPuzzleBoard |
| 2 | Link | LinkPuzzleBoard |
| 3 | TapMatch | TapMatchPuzzleBoard |

### inputType 값 (비트 플래그)
| 값 | 의미 | 생성되는 블럭 |
|----|------|-------------|
| 1 | Swap | NormalBlock |
| 2 | Link | NormalBlock |
| 4 | Touch | NormalBlock |
| 5 | Swap+Touch | BombBlock |

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
3. `inputType` 플래그 조합 설정
4. 필요 시 `INGAME.md` 참고하여 새 Block 클래스 + Factory 분기 추가
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
