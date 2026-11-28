# PuzleBattleGame 리플레이 구현

대전의 상대 선정·승패는 [인게임 사양서](../../../docs/INGAME-BATTLE.md#현재-승패와-기록), 저장 JSON과 구형 기록 호환성은 [데이터 문서](../../../docs/DATA.md#대전-기록-호환성)에 있다.

## 기록 흐름

1. `StageInjection`이 준비한 `GameSpec`의 시드로 보드를 초기화한다.
2. 보드는 고정 틱에서 처리한 위치 입력과 손 떼기 입력에 프레임·공통 순번을 기록한다.
3. [PuzzleGameController](../../02_Scripts/PuzzleCore/Controller/PuzzleGameController.cs)는 종료 시 입력 목록과 시드, 스테이지 스냅샷, 대전 피벗 설정, 규칙 버전, 종료 틱·점수를 모아 `ReplayStorage.Save()`에 전달한다.

## 재생 흐름

1. [ReplayController.Initialize](../../02_Scripts/PuzzleCore/Controller/ReplayController.cs)는 Rule 주소를 읽고, 기록의 `stageJson`을 우선 사용한다. 이 필드가 없으면 원래 스테이지 주소를 읽는다. 시드와 기록 당시 계산 규칙으로 별도 보드를 초기화한다.
2. `PuzzleGameController.FixedUpdate()`에서 호출하는 `AdvanceFrame()`은 해당 틱의 입력을 순서대로 주입하고 보드와 피벗을 전진시킨 뒤 프레임 수를 늘린다.
3. 현재 형식은 보드의 종료 상태와 실제 재생 틱·점수를 기록과 대조한다. 그 결과는 `HasValidResult`에서 확인한다.

## ReplayController 배치

메인 보드와 별도의 `IPuzzleBoard`와 `PuzzleBoardView`를 사용한다. `viewScale`과 `margin`으로 우측 상단에 배치하고 `skipCameraAlign`으로 메인 카메라 정렬을 유지한다.
