# PuzleBattleGame 데이터 스키마 상세

## GameSpec 구조

[GameSpec 필드 표](../../../docs/DATA.md#gamespec-구조)는 런타임 모드, 규칙·블럭·스테이지와 피벗 설정의 관계를 설명한다. 해당 타입은 [GameSpec.cs](../../02_Scripts/PuzzleCore/Module/GameSpec.cs)와 [PuzzleDefine.cs](../../02_Scripts/PuzzleCore/Module/PuzzleDefine.cs)에 선언되어 있다.

## ReplayData JSON 구조

[ReplayData JSON 예제와 필드 표](../../../docs/DATA.md#replaydata-json-구조)에서 저장 경로, 스테이지 스냅샷, 기록 버전, 입력 순서와 완료 결과를 확인한다. [기록·재생 흐름](../02_Ingame/INGAME_REPLAY.md)은 이 필드를 쓰고 읽는 시점을 설명한다.
