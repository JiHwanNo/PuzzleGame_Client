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
- `MakeGameSpec(ruleAddress, puzzleType, stageId)` — 사이드 스테이지 모드 경로. Rule은 Addressable, Stage는 **`StageStorage`**(Resources/다운로드 경로)로 로드. `DATA_STAGE.md` 참고.

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

GameSpec 전체 필드 스키마 트리는 [`DATA_SCHEMA.md`](DATA_SCHEMA.md) 참고.

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
      "effects": [ { "trigger": "OnDestroyed", "action": "DestroyRadius", "param": "2" } ]
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

조합 예: `["Swap","Touch"]` → `Swap|Touch`(5). `inputType`은 **조작 방법**일 뿐이며, 폭탄 같은 **행동은 `effects`로, 피격 규칙은 `damagedBy`로 별도 지정**한다.

### life / damagedBy 값 (HP와 피격 규칙)
블럭은 "속성 데이터 + 효과 동사"의 조합이다. HP와 어떤 데미지에 깎이는지를 데이터로 정의한다.
- **`life`** (int) — HP(내구도). 피격마다 1 감소, 0이면 파괴. 미지정/0이면 1로 취급.
- **`damagedBy`** (List<string>) — HP를 깎을 수 있는 데미지 소스. 미지정/비어있으면 일반 블럭 기본값 `Match|Splash`.

| 문자열 | DamageSource | 의미 |
|--------|:---:|------|
| `"Match"` | 1 | 자신이 매치에 포함되어 터짐 (일반팡) |
| `"NeighborMatch"` | 2 | 직교 인접 칸에서 매치 발생 (과자 등 인접 파괴형) |
| `"Splash"` | 4 | 폭발 여파 — 폭탄/라인/무지개 등 효과 동사에 의한 파괴 (폭탄팡) |

### effects 값 (효과 동사 목록 → IBlockEffect 부착)
블럭에 부착할 **효과 동사**를 목록으로 지정한다. `PuzzleBlockFactory`가 `EffectFactory`로 동사를 생성해 `Block`에 부착한다.
각 항목은 `{ trigger, action, param }` 형태다. **`trigger`는 발화 시점을 데이터로 지정**하며(`OnDestroyed`/`OnSwapped`), 생략하면 아래 동사별 기본값을 쓴다.

| action | 기본 trigger | param | 동작 |
|--------|---------|-------|------|
| `"DestroyRadius"` | `OnDestroyed` | 반경(정수, 예: `"2"`) | 원형 폭발. 발화 시 유클리드 반경 내 블럭에 Splash 데미지 |
| `"DestroyLine"` | `OnDestroyed` | 방향(`Horizontal`/`Vertical`/`Cross`) | 라인 폭발. 발화 시 행/열(십자) 라인에 Splash 데미지 |
| `"DestroySameColor"` | `OnSwapped` | (없음) | 무지개. 발화 시 대상 색 블럭을 모두 파괴하고 자신을 소비. `OnSwapped`면 `inputType`에 `Swap` 필요 |

> `trigger`를 명시하면 기본값을 덮어쓴다. 예: `{trigger:"OnSwapped", action:"DestroyRadius", param:"2"}` → "스왑하면 반경 2 폭발".

> **장애물은 클래스 없이 데이터로 구성한다.** 효과 동사가 없어도 `damagedBy`만으로 성립한다.
> - **과자(인접 파괴)**: `damagedBy:["NeighborMatch","Splash"]`, `effects` 없음.
> - **금이 간 벽돌(폭탄 전용)**: `damagedBy:["Splash"]`, `inputType` 없음(조작 불가), `effects` 없음.
> - **다중 HP**: `life:2` + 위 `damagedBy` → 두 번 피격되어야 파괴.

> **문자열 ↔ enum 변환 정리**: `JsonUtility`는 `inputType`/`damagedBy`/`effects`를 그대로 역직렬화만 한다. 문자열 → enum/동사 변환은 `Block`(inputType·damagedBy) / `EffectFactory`(effects)에서 수행한다. **기존 동사 조합으로 표현되는 새 블럭은 데이터만 추가**하면 되고, 진짜 새로운 동작만 동사 클래스 + `EffectFactory` 분기를 추가한다.

---

## Stage JSON 구조

파일 위치:
- **레거시/메인**: `Assets/05_Table/Stage/Stage.json` (Addressable 등록 에셋)
- **모드별 스테이지**: `Assets/Resources/Stage/{모드}/Stage_{번호:000}.json` (예: `Resources/Stage/ThreeMatch/Stage_001.json`). `StageStorage`로 로드. `DATA_STAGE.md` 참고.

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

## 스테이지 저장소 / 맵 툴

모드별 스테이지 로드 저장소(`StageStorage`)와 시각 편집 도구(`StageMapTool`)는 [`DATA_STAGE.md`](DATA_STAGE.md) 참고.

---

## 새 블럭 추가 방법

1. Rule JSON의 `blocks` 배열에 새 BlockData 추가
2. `blockId` 고유 값 지정
3. `inputType` 조작 방법 문자열 목록 설정 (예: `["Swap","Touch"]`)
4. HP/피격 규칙이 필요하면 `life`(HP) + `damagedBy`(피격 소스) 지정. 장애물(과자·벽돌)은 이 둘만으로 성립.
5. 특수 행동이 필요하면 `effects`에 효과 동사 추가 (예: `[{"trigger":"OnDestroyed","action":"DestroyRadius","param":"2"}]`).
   - 기존 동사 조합으로 표현되면 **코드 변경 없이 데이터만** 추가.
   - **진짜 새로운 동작**이면 `Module/Effect/`에 `IBlockEffect` 동사 클래스 + `EffectFactory` 분기를 함께 추가 → 상세: `INGAME_GIMMICK.md` "새 동작 추가 절차"
   - 일반 색 블럭은 코드 변경 없이 JSON 추가만으로 동작
5. 블럭 스프라이트를 `Assets/04_Resources/Block/`에 추가 후 Addressable 등록

---

## ReplayData JSON 구조

ReplayData JSON 포맷과 필드 표는 [`DATA_SCHEMA.md`](DATA_SCHEMA.md) 참고. 기록/재생 흐름은 `INGAME_REPLAY.md` 참고.
