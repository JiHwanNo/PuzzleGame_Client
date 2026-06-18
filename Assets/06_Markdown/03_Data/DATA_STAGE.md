# PuzleBattleGame 스테이지 저장/편집 인프라

모드별 스테이지 JSON의 로드 저장소와 시각 편집 도구 참고. JSON 포맷은 `DATA.md` 참고.

---

## 스테이지 저장소 (StageStorage)

모드별 스테이지 JSON을 로드하는 정적 저장소(`02_Scripts/StageMapTool/StageStorage.cs`). 다운로드 경로 우선, 없으면 Resources에서 로드.

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
작업 진행 상태·다음 작업 메모는 `STAGE_MAP_TOOL.md` 참고.

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
