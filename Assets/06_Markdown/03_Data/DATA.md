# PuzleBattleGame 데이터/설정 참고 문서

게임 규칙은 [게임 사양서](../../../docs/SPEC.md)에서, 데이터 정의와 저장 위치는 [데이터 구현 문서](../../../docs/DATA.md)에서 찾는다. 아래는 Unity 프로젝트에서 사용하는 로드 경로다.

## 데이터 흐름

- 로비는 [유닛 명단](../../04_Resources/LobbyUnitCatalog.json)을 주소 `LobbyUnitCatalog`로 읽고 각 행을 [유닛 정의](../../02_Scripts/Lobby/LobbyUnit.cs)에 연결한다. 종류별 기본값과 저장된 카드 보너스의 관계는 [카드 사양서](../../../docs/LOBBY-CARDS.md)에 있다.
- [StageInjection](../../02_Scripts/StageMapTool/StageInjection.cs)의 `MakeGameSpec(ruleAddress, stageAddress, mode)`는 규칙과 스테이지 주소를 받아 `GameSpec`을 구성한다. `mode`의 기본값은 Stage이며 대전은 Battle을 지정한다.
- `MakeGameSpec(ruleAddress, puzzleType, stageId)`는 모드와 번호로 `StageStorage`에 스테이지를 요청한다. `MakeGameSpecFromData`는 맵 툴에서 정규화한 편집 데이터를 메모리로 주입한다.
- 성공한 `GameSpec`은 `GetGameSpec()`로 인게임 컨트롤러에 전달한다. 로드 또는 검증 실패 시 반환값과 오류 로그를 확인한다.

### 데이터 타입 주의 (struct vs class)

`RuleData`, `ObjectiveData`, `InputRecord`, `InputEndRecord`는 값 타입이다. JSON 파싱 실패 검사는 이를 담는 클래스에서 수행한다. [타입별 표](../../../docs/DATA.md#데이터-타입-주의-struct-vs-class)

## GameSpec 구조

[GameSpec 필드 구조](../../../docs/DATA.md#gamespec-구조)

## Rule JSON 구조

규칙 파일 위치, 예제와 피벗 설정의 단위는 [Rule JSON 구조](../../../docs/RULES-AND-STAGES.md#rule-json-구조)를 참고한다.

### puzzleType 값

[퍼즐 타입과 보드 클래스](../../../docs/RULES-AND-STAGES.md#puzzletype-값)

### inputType 값 (문자열 목록 → InputType 플래그)

[조작 문자열과 플래그](../../../docs/RULES-AND-STAGES.md#inputtype-값-문자열-목록--inputtype-플래그)

### gimmickIds 값

일반 블럭은 기믹 목록을 비워 두며, 행동 이름은 등록된 기믹 타입과 연결한다. [기믹 연결 방식](../02_Ingame/INGAME_GIMMICK.md)

## Stage JSON 구조

대전의 기본 스테이지 주소는 `Stage`다. 맵 툴과 모드별 스테이지는 `Assets/Resources/Stage/{모드}/Stage_{번호:000}.json` 경로를 사용한다. [Stage JSON 예제](../../../docs/RULES-AND-STAGES.md#stage-json-구조)

### cell_type 값

[셀 타입과 데이터 값](../../../docs/RULES-AND-STAGES.md#cell_type-값)

## 새 규칙 추가 방법

기존 모드에 규칙을 추가하는 절차와 새 보드 구현을 추가하는 절차는 [규칙 추가 방법](../../../docs/RULES-AND-STAGES.md#새-규칙-추가-방법)에 있다.

## 새 스테이지 추가 방법

Addressables용 스테이지는 [스테이지 추가 방법](../../../docs/RULES-AND-STAGES.md#새-스테이지-추가-방법)을 따른다. 맵 툴이 저장하는 모드별 파일은 `stage_id`와 파일 번호를 일치시킨다.

## 스테이지 저장소 / 맵 툴

[StageStorage와 편집기 검증 규칙](DATA_STAGE.md) · [맵 툴 작업 메모](STAGE_MAP_TOOL.md)

## 새 블럭 추가 방법

[블럭 데이터·기믹·스프라이트 등록 절차](../../../docs/RULES-AND-STAGES.md#새-블럭-추가-방법)

## ReplayData JSON 구조

[저장 필드와 호환 버전](../../../docs/DATA.md#replaydata-json-구조) · [기록과 재생 경로](../02_Ingame/INGAME_REPLAY.md)
