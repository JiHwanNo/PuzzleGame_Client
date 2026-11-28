# PuzleBattleGame

3매치·링크·탭 매치를 지원하는 Unity URP 퍼즐 게임입니다. 로비에서 스테이지를 시작하거나 로컬 리플레이를 상대로 90초 점수 대전을 플레이할 수 있습니다. 호환되는 대전 기록이 없으면 첫 기록에 도전합니다.

프로젝트 문서는 두 갈래로 나뉩니다. [1. 사양서](docs/SPEC.md)에서 로비·인게임·카드의 규칙을, [2. 구현 현황](docs/IMPLEMENTATION.md)에서 기능별 반영 상태와 코드를 확인할 수 있습니다.

## 실행 흐름

1. 프로젝트에 지정된 Unity 에디터로 열고 `TitleScene`에서 Play를 누릅니다. 에디터 버전은 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt), URP·Addressables·Input System 버전은 [manifest.json](Packages/manifest.json)에 있습니다.
2. 타이틀이 표시될 때 `Main`이 공통 매니저와 카메라가 있는 `SharedScene`을 추가로 로드합니다. 타이틀 연출이 끝나면 로딩 씬을 거쳐 로비로 이동합니다.
3. 로비 메인에서 플레이를 눌러 퍼즐 대전에 들어갑니다. 종료 후 결과 화면에서 다시 도전하거나 로비로 돌아갑니다. 세부 규칙은 [한 판의 진행](docs/INGAME-BATTLE.md#한-판의-진행)을 참고하세요.

## 코드 읽기

| 궁금한 점 | 시작할 곳 |
|---|---|
| 어떤 보드와 블럭이 생성되는가? | [Rule JSON](Assets/05_Table/Rule)과 [Stage JSON](Assets/05_Table/Stage)을 [StageInjection](Assets/02_Scripts/StageMapTool/StageInjection.cs)이 읽어 `GameSpec`으로 조립합니다. |
| 한 번의 입력이 화면에 어떻게 반영되는가? | [PuzzleGameController](Assets/02_Scripts/PuzzleCore/Controller/PuzzleGameController.cs)가 입력을 수집하고 고정 틱마다 보드를 진행합니다. [PuzzleBoardView](Assets/02_Scripts/PuzzleCore/View/PuzzleBoardView.cs)가 보드의 액션 큐를 읽어 이동·생성·파괴를 그립니다. |
| 대전 상대와 점수는 어떻게 결정되는가? | [LobbyMain](Assets/02_Scripts/Lobby/LobbyMain.cs)이 호환되는 기록을 고르고 [ReplayController](Assets/02_Scripts/PuzzleCore/Controller/ReplayController.cs)가 기록된 시드와 입력으로 상대 보드를 재생합니다. |

기능별 파일과 주요 메서드는 [작업별 코드 지도](docs/MAP.md#작업별-빠른-경로), 구현 범위와 설계 방향은 [아키텍처](docs/ARCHITECTURE.md)에서 확인할 수 있습니다.
