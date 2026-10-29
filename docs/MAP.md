# PuzleBattleGame 문서 네비게이션

작업 전 이 문서에서 대상 문서를 확인하고 이동하세요.

---

## 문서 목록

| 문서 | 내용 | 언제 읽을까? |
|------|------|-------------|
| [../AGENTS.md](../AGENTS.md) | Codex 작업 원칙, 코딩 규칙, 주의사항(Known Pitfalls), 작업별 참고 링크 | 항상 (기본 규칙) |
| [../CLAUDE.md](../CLAUDE.md) | Claude Code 호환용 기존 가이드 | Claude Code 사용 시 |
| [ARCHITECTURE.md](ARCHITECTURE.md) | MVC, 데이터 흐름, 리플레이, 게임 흐름, 폴더 구조 | 아키텍처 이해 시 |
| [CONVENTIONS.md](CONVENTIONS.md) | 코드 리뷰 체크리스트, 위반 예시, 수정 가이드 | 커밋 전 리뷰 시 필독 |
| [INGAME.md](INGAME.md) | 게임 루프, 보드 상태, 블럭, 매칭, 뷰 동기화, 애니메이션, 리플레이 | 인게임 퍼즐 작업 시 |
| [DATA.md](DATA.md) | JSON 구조, GameSpec, Rule/Stage/Replay 데이터, 추가 방법 | 데이터/설정 작업 시 |
| [UI.md](UI.md) | 도메인 시스템, 팝업/탭 생명주기, UIButton, 추가 방법 | UI 작업 시 |
| [SCENE.md](SCENE.md) | 씬 전환, SharedScene, 매니저, AssetManager, PoolManager | 인프라 작업 시 |
| [SERVER.md](SERVER.md) | 서버 통신, 공유 DLL, 네트워크 레이어, API 연동 | 서버 API 작업 시 |
| [CHANGELOG.md](CHANGELOG.md) | 전체 변경 이력 | 이전 작업 맥락 파악 시 |

---

## 디버깅/최적화 시 필독

| 상황 | 읽을 곳 |
|------|---------|
| 정렬 변경 후 버그 | [../AGENTS.md](../AGENTS.md) → "주의사항 - 정렬 안정성" |
| 뷰 블럭 미씽/소실 | [INGAME.md](INGAME.md) → "ExecuteBatchMovement 처리 순서 규칙" |
| LINQ 제거 최적화 | [../AGENTS.md](../AGENTS.md) → "주의사항 - 최적화 체크리스트" |
| 씬 전환 후 에셋 미씽 | [../AGENTS.md](../AGENTS.md) → "주의사항 - Pool/Addressable 생명주기" |
| struct null 비교 에러 | [../AGENTS.md](../AGENTS.md) → "주의사항 - 데이터 타입" |
| Model에서 Debug.Log 사용 | [../AGENTS.md](../AGENTS.md) → "주의사항 - Model 레이어 로깅" |
| 데이터 타입 확인 | [DATA.md](DATA.md) → "데이터 타입 주의" |

---

## 작업별 빠른 경로

기능 이름으로 찾은 뒤 **시작 파일 → 상세 문서의 콘텐츠와 C# 매핑 → 주요 메서드** 순서로 따라가세요. 전체 경로는 링크 대상에 포함되어 있습니다.

| 찾는 기능·수정할 내용 | 먼저 볼 C# 파일 | 관련 코드 전체 표 |
|---|---|---|
| 게임 시작·스테이지/60초 대전·랜덤 퍼즐 모드 선택 | [PopupReady.cs](../Assets/02_Scripts/UI/Popup/PopupReady.cs) | [UI.md](UI.md#콘텐츠와-c-매핑) |
| 퍼즐 입력·게임 루프·종료 처리 | [PuzzleGameController.cs](../Assets/02_Scripts/PuzzleCore/Controller/PuzzleGameController.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 3매치·스왑·자동 셔플 | [ThreeMatchPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/ThreeMatchPuzzleBoard.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 링크 연결 | [LinkPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/LinkPuzzleBoard.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 탭 매치·연결 블럭 탐색 | [TapMatchPuzzleBoard.cs](../Assets/02_Scripts/PuzzleCore/Module/Board/TapMatchPuzzleBoard.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 새 블럭·기믹 추가 | [PuzzleBlockFactory.cs](../Assets/02_Scripts/PuzzleCore/Module/Block/PuzzleBlockFactory.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 폭탄 범위·연쇄 파괴 | [BombGimmick.cs](../Assets/02_Scripts/PuzzleCore/Module/Gimmick/BombGimmick.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 점수·콤보·피버·목표 | [ObjectiveManager.cs](../Assets/02_Scripts/PuzzleCore/Module/ObjectiveManager.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 낙하 중 블럭 소실·뷰 액션 순서 | [PuzzleBoardView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleBoardView.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 블럭 애니메이션 | [PuzzleBlockView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleBlockView.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 보드 좌표·링크 선·배치 | [PuzzleBoardView.cs](../Assets/02_Scripts/PuzzleCore/View/PuzzleBoardView.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 리플레이 기록·저장 | [ReplayStorage.cs](../Assets/02_Scripts/PuzzleCore/Module/ReplayStorage.cs) | [DATA.md](DATA.md#콘텐츠와-c-매핑) |
| 리플레이 재생 | [ReplayController.cs](../Assets/02_Scripts/PuzzleCore/Controller/ReplayController.cs) | [INGAME.md](INGAME.md#콘텐츠와-c-매핑) |
| 새 스테이지·Rule JSON 로드 | [StageInjection.cs](../Assets/02_Scripts/StageInjection.cs) | [DATA.md](DATA.md#콘텐츠와-c-매핑) |
| JSON 필드·enum·입력 타입 추가 | [PuzzleDefine.cs](../Assets/02_Scripts/PuzzleCore/Module/PuzzleDefine.cs) | [DATA.md](DATA.md#콘텐츠와-c-매핑) |
| 팝업·탭·뒤로 가기 | [DomainManager.cs](../Assets/02_Scripts/Manager/DomainManager.cs) | [UI.md](UI.md#콘텐츠와-c-매핑) |
| UI 버튼 콜백 | [UIButton.cs](../Assets/02_Scripts/UI/UIButton.cs) | [UI.md](UI.md#콘텐츠와-c-매핑) |
| 새 씬·씬 전환·상주 초기화 | [Main.cs](../Assets/02_Scripts/Manager/Main.cs) | [SCENE.md](SCENE.md#콘텐츠와-c-매핑) |
| 에셋 로드·씬 전환 후 리소스 소실 | [AssetManager.cs](../Assets/02_Scripts/Manager/AssetManager.cs) | [SCENE.md](SCENE.md#콘텐츠와-c-매핑) |
| 풀 생성·반환 | [PoolManager.cs](../Assets/02_Scripts/Manager/PoolManager.cs) | [SCENE.md](SCENE.md#콘텐츠와-c-매핑) |
| 서버 API·네트워크 | [NetworkManager.cs](../Assets/02_Scripts/Manager/NetworkManager.cs) | [SERVER.md](SERVER.md#콘텐츠와-c-매핑) |
| 유저 정보·더미 폴백 | [IdentityLayer.cs](../Assets/02_Scripts/Manager/UserData/IdentityLayer.cs) | [SERVER.md](SERVER.md#콘텐츠와-c-매핑) |

---

## 확장 포인트 체크리스트

### 새 퍼즐 모드 추가
1. `IPuzzleBoard` 구현 클래스 작성 (`PuzzleCore/Module/Board/`)
2. `PuzzleType` enum 값 추가 (`PuzzleDefine.cs`)
3. `PuzzleGameController.Start()` PuzzleType별 분기 추가
4. Rule JSON 생성 + Addressable 등록 → 상세: [INGAME.md](INGAME.md), [DATA.md](DATA.md)

### 새 블럭/기믹 추가 (기믹 컴포지션)
- 일반 색 블럭: Rule JSON `blocks[]`에 BlockData 추가만으로 동작 (코드 변경 불필요)
- 새 행동(기믹)이 필요한 경우:
  1. `GimmickBase` 상속 기믹 클래스 작성 (`PuzzleCore/Module/Gimmick/`)
  2. `PuzzleDefine.cs`의 `GimmickType` enum 값 추가
  3. `GimmickFactory.Create()`에 `GimmickType → 기믹` 생성 분기 추가
  4. Rule JSON BlockData에 `gimmickIds`(문자열 목록) + 필요한 `inputType` 추가 → 상세: [INGAME.md](INGAME.md), [DATA.md](DATA.md)

### 새 매니저 추가
1. `02_Scripts/Manager/`에 MonoBehaviour 싱글톤 작성
2. SharedScene 하이어라키에 배치
3. 초기화 순서 의존성 있으면 `Main`에서 순서 보장 → 상세: [SCENE.md](SCENE.md)

### 새 서버 API 추가
1. 공유 DLL(`PuzleBattleShared`)에 DTO 추가 → `Plugins/`로 복사
2. `UserData/`에 Layer 클래스 작성 (Endpoint + Request)
3. `UserDataManager`에 Layer 등록 → 상세: [SERVER.md](SERVER.md)

---

## 소스 경로와 문서 관리 기준

- 실제 프로젝트 경로는 저장소 루트의 `Assets/02_Scripts/`에서 시작합니다. `IPuzzleBoard.cs`는 `PuzzleCore/Module/Board/`, `PopupReady.cs`는 `UI/Popup/` 아래에 있습니다.
- `Assets/02_Scripts/`의 C# 파일별 역할은 위에서 연결한 INGAME/DATA/UI/SCENE/SERVER의 코드 표에서 찾습니다. 파일명을 알고 있으면 해당 문서들에서 파일명으로 검색하세요.
- 하나의 파일에 여러 타입이 있을 수 있습니다. 예: `GameSpec.cs`에 `StageData`/`CellData`, `IGimmick.cs`에 `IGimmickHost`/`GimmickBase`가 함께 정의됩니다.
- 설계 방향과 예정 콘텐츠는 [ARCHITECTURE.md](ARCHITECTURE.md), 현재 구현 위치는 각 영역의 코드 표, 과거 변경은 [CHANGELOG.md](CHANGELOG.md)에서 확인합니다. 별 재화·랭킹 등 계획을 구현 완료로 해석하지 않습니다.
- 매 작업 완료 전 문서 영향을 점검합니다. 파일 추가·삭제·이동·이름 변경, 주요 타입/메서드 또는 책임·동작·데이터 구조 변경 시 담당 영역의 코드 표와 관련 본문을 같은 작업에서 갱신합니다. 작업 시작 파일이 달라지면 이 문서의 빠른 경로도 갱신합니다.
- 완료 기준과 보고 방식은 [AGENTS.md의 문서와 코드 동기화 지침](../AGENTS.md#문서와-코드-동기화-매-작업-필수)을 따릅니다. 문서 영향이 없으면 불필요한 수정 대신 그 사유를 보고합니다.
- 코드 링크는 문서 기준 상대 경로를 사용하고 줄 번호 대신 메서드/타입 이름을 기록합니다. 수정 후 링크 대상 파일과 해당 심볼의 존재를 확인합니다.
