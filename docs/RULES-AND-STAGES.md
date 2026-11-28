# 규칙과 스테이지 데이터

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

피벗 동작과 밸런스는 [인게임 사양서](INGAME-BATTLE.md)에 정리한다. 세 규칙은 최상위 `pivotSettings`를 `AssetManager`로 읽고 대전 진입 시 검증한 복사본을 사용한다. 설정값은 [3매치 Rule](../Assets/05_Table/Rule/ThreeMatchRule.json), [링크 Rule](../Assets/05_Table/Rule/LinkMatchRule.json), [탭 매치 Rule](../Assets/05_Table/Rule/TapMatchRule.json)에서 수정한다.

| 필드 | 저장 단위와 처리 |
|---|---|
| `Capacity`, `DrainCapacity`, `DrainPerSecond` | 충전 완료 점수, 활성 게이지 총점, 초당 소비 점수. `DrainCapacity`가 0이거나 누락되면 `Capacity`를 활성 총점으로 사용한다. |
| `ChargePerBlockUnits`, `ChargeComboStep`, `ChargeBonusPerStepUnits` | `Units`는 점수의 1/100 단위다. 콤보가 한 단계(`ChargeComboStep`) 오를 때마다 블럭당 충전량에 단계 보너스를 더한다. |
| `DrainReductionPercentPerCombo`, `MinimumDrainPercent` | 빠른 콤보당 기본 소비 속도 감소분(퍼센트포인트)과 최소 소비 비율(퍼센트). |
| `RefillPercentPerRapidCombo`, `ComboWindowTicks` | 빠른 매칭당 활성 게이지 회복 비율(0~100), 콤보 허용 간격(논리 틱). 회복 비율이 없는 기록은 0을 사용한다. |
| `LinkNeighborClear`, `SquareNeighborClear` | 육각 링크와 사각 보드 각각의 이웃 추가 파괴 스위치. 기록에서 누락된 필드는 false로 읽는다. |

재생 시에는 `ReplayData.pivotSettings`를 사용하며 스냅샷 전체가 없으면 구형 기본값을 적용한다. [리플레이 저장 형식](DATA.md#replaydata-json-구조)

### 시간과 좌표 데이터 검증

- `StageInjection`은 Rule 제한 시간을 `ObjectiveManager.IsValidTimeLimit`로 검증한다. NaN·Infinity·음수·int 틱 범위 초과는 로그를 남기고 진입을 중단한다. 0은 무제한이며 대전 시간은 `GameSpec.BattleDurationSeconds`를 사용한다.
- Stage는 양수 `stage_width`/`stage_height`, null이 아닌 `cells`, 각 셀의 `0 <= x < stage_width`, `0 <= y < stage_height`를 요구한다. `HasValidCoordinates`는 일반 로드·리플레이·보드 직접 초기화에서 공통 사용한다.
- 새 기록은 `numericRuleVersion: 1`을 저장한다. 버전 0은 유효한 입력 범위 내에서 이전 시간·점수 계산을 재현한다. 알 수 없는 버전은 로드와 재생에서 거부한다. 새 버전의 계산 정책은 [수치 경계와 시간 변환](INGAME-VIEW-AND-BATTLE.md#수치-경계와-시간-변환)을 참고한다.

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

1. [기존 Rule JSON](../Assets/05_Table/Rule)에서 보드의 `puzzleType`을 확인하고 블럭·목표·시간 설정을 작성한다.
2. JSON을 Addressables에 등록한다. 준비 팝업에서 추첨할 규칙이면 준비 팝업의 규칙 주소·모드 이름·릴 이미지에 같은 순서로 등록한다.
3. 새로운 보드 동작이 필요하면 `PuzzleType`과 `IPuzzleBoard` 구현을 추가하고 플레이·리플레이 양쪽의 생성 분기를 연결한다. [새 퍼즐 모드 추가](MAP.md#새-퍼즐-모드-추가)

## 새 스테이지 추가 방법

1. `Assets/05_Table/Stage/Stage.json` 수정 또는 새 JSON 생성
2. `cells` 배열에 보드 크기만큼 셀 데이터 작성
3. 최상단 행은 `cell_type: 3` (Generator) + `generator_block_ids` 설정
4. `block_id`로 초기 블럭 배치 (null이면 빈 셀)

## 새 블럭 추가 방법

그림을 만들 때는 [보드 블럭 스타일과 폭탄 프롬프트](ART-STYLE.md#보드-블럭)를 참고한다.

1. Rule JSON의 `blocks` 배열에 새 BlockData 추가
2. `blockId` 고유 값 지정
3. `inputType` 조작 방법 문자열 목록 설정 (예: `["Swap","Touch"]`)
4. 특수 행동이 필요하면 `gimmickIds`에 기믹 문자열 추가 (예: `["Bomb"]`).
   - **새 기믹 타입**이면 `PuzzleDefine.cs`의 `GimmickType` enum 값 + `GimmickFactory` 분기 + (`GimmickBase` 상속) 기믹 클래스를 함께 추가 → 상세: `INGAME.md` "새 블럭(기믹) 추가 절차"
   - 일반 색 블럭은 코드 변경 없이 JSON 추가만으로 동작
5. 블럭 이미지를 단일 Sprite로 임포트하고 `BuildIn_Resource` 그룹에 `Block_{blockId}` 주소로 등록한다. 예를 들어 [원형 폭탄 스프라이트](../Assets/04_Resources/Ingame/Block/Block_200-1.png)의 주소는 `Block_200-1`이다. [PuzzleBlockView](../Assets/02_Scripts/PuzzleCore/View/PuzzleBlockView.cs)가 이 주소로 이미지를 로드한다.
