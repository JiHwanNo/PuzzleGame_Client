# 씬 진입과 게임 데이터 전달

씬 전환과 데이터 주입의 호출 경로를 설명한다. 플레이 규칙은 [사양서](../../../docs/SPEC.md), 기능별 연결 여부는 [구현 현황](../../../docs/IMPLEMENTATION.md)에서 확인한다.

## 로비에서 퍼즐 라운드로

| 단계 | 호출 경로와 데이터 |
|---|---|
| 앱 시작 | `TitleScene`에서 시작한다. `Main.AutoCreate()`가 공용 `SharedScene`을 추가 로드하고, `TitleMain.Start()`의 연출이 끝나면 `Main.MoveScene`이 `LoadingScene`을 거쳐 `LobbyScene`으로 이동한다. |
| 로비 플레이 버튼 | `LobbyMain.OnClickPlay()`가 `PrepareBattle(null)`을 호출한다. 로컬 완주 기록을 탐색해 호환되는 상대의 규칙·스테이지·시드를 적용하고, 상대가 없으면 기본 규칙으로 보드를 준비한다. `StageInjection.SetReplayData`에 상대 기록 또는 `null`을 전달하고, 라이프 차감에 성공하면 게임 씬으로 이동한다. |
| 준비 팝업 | 룰렛 연출 후 `PopupReady.SpinAndStart()`가 `PrepareSelectedGame()`을 호출한다. 이때 선택된 모드로 규칙 주소를 정한다. 대전은 `LobbyMain.PrepareBattle(ruleAddress)`, 스테이지는 `StageInjection.MakeGameSpec(ruleAddress, "Stage")`를 호출한다. |
| 게임 씬 | `PuzzleGameController`가 주입된 `GameSpec`으로 보드를 초기화하고 뷰를 그린다. 상대 기록이 있으면 `ReplayController`가 같은 논리 틱에 상대 보드를 진행한다. |
| 종료 | `Finish`에 도달하면 `PuzzleGameController`가 로컬 리플레이 저장을 시도하고 결과를 표시한다. `OnClickRetry`는 같은 대전 조건으로 게임 씬을 다시 열고, `OnClickLobby`는 로비로 돌아간다. |

씬 전환과 비동기 대전 결과 집계는 각각 [씬 관리](../../../docs/SCENE.md)와 [배틀 리그 사양](../../../docs/INGAME-BATTLE.md#배틀-리그)을 따른다.

## 번호 기반 퍼즐 스테이지 로드

모드·번호로 스테이지를 지정하는 호출자를 위해 `StageInjection.MakeGameSpec(ruleAddress, puzzleType, stageId)`가 별도로 제공된다. `StageStorage.TryLoadStageJson`은 `persistentDataPath`에 저장된 파일을 먼저 읽고, 없으면 같은 키의 번들 `Resources` 데이터를 읽는다. 서버에서 파일을 받는 단계는 이 로더의 호출 경로에 없다.

사이드 스테이지 선택 화면과 전용 씬은 기획 후보다. 로비 방어 스테이지는 별도의 전장 데이터로 진행한다. [퍼즐 스테이지 데이터](../../../docs/RULES-AND-STAGES.md) · [사이드 퍼즐 해금과 재화에 관한 초기안](../../../docs/LOBBY-DEFENSE.md#사이드-퍼즐-해금과-재화에-관한-초기안)
