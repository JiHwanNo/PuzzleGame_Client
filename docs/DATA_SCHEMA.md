# PuzleBattleGame 데이터 스키마 상세

`DATA.md`의 상세 스키마 분리 문서.

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

## ReplayData JSON 구조

파일 위치:
- **에디터**: `Assets/05_Table/Replay/replay_{timestamp}.json`
- **빌드**: `Application.persistentDataPath/Replay/replay_{timestamp}.json`

게임 종료 시 `ReplayStorage.Save()`에 의해 자동 생성됨.

```json
{
    "ruleAddress": "LinkMatchRule",
    "stageAddress": "Stage",
    "stageJson": "{\"stage_id\":1,\"stage_width\":8,\"stage_height\":8,\"cells\":[]}",
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
| `stageJson` | string | 기록 시점의 스테이지 JSON 스냅샷 |
| `randomSeed` | int | 게임에 사용된 난수 시드 |
| `inputs` | List | 유저 입력 기록 |
| `inputEnds` | List | 유저 입력 종료 기록 |
| `recordedAt` | string | 기록 일시 |
