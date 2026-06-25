# 문서 인덱스

PuzleBattleGame(Unity 6000.0.38f1, URP) 문서 모음. 작업 영역에 맞는 문서를 골라 읽으세요.
세부 네비게이션(확장 포인트·소스 위치)은 [MAP.md](MAP.md), 코딩 규칙 전체는 [../../AGENTS.md](../../AGENTS.md) 참고.

> **폴더 구조.** 문서는 콘텐츠별 폴더로 묶여 있다(`00_Guide`/`01_Architecture`/`02_Ingame`/`03_Data`/`04_UI`/`05_Scene`/`06_Server`/`07_Agents`).
> 본문에서 백틱으로 표기한 문서명(예: `INGAME.md`)은 **위치가 아닌 식별자**다. 실제 위치는 이 인덱스·[MAP.md](MAP.md)의 링크를 따른다.

---

## 문서 목록

| 문서 | 200자 요약 |
|------|-----------|
| [MAP.md](MAP.md) | **문서 네비게이션.** 작업별 빠른 경로, 디버깅 시 읽을 곳, 확장 포인트 체크리스트(새 모드/블럭/매니저/API 추가 절차), 주요 소스 파일 위치 표. 작업 시작 전 진입점. |
| [01_Architecture/ARCHITECTURE.md](01_Architecture/ARCHITECTURE.md) | **아키텍처 전반.** MVC 엄격 분리, 데이터 기반 생성, 결정론적 리플레이, 인터페이스 기반 확장 원칙. 데이터 흐름·게임 흐름·폴더 구조. 결정론 규칙(난수/시간). |
| [00_Guide/CONVENTIONS.md](00_Guide/CONVENTIONS.md) | **커밋 전 코드 리뷰 필독.** 리뷰 체크리스트, 위반 예시와 수정 가이드. 명명/스타일/주석 규칙 검증 기준. PR 올리기 전 반드시 확인. |
| [00_Guide/PREFAB_STRUCTURE.md](00_Guide/PREFAB_STRUCTURE.md) | **프리팹 구조 규칙.** Root=컨트롤러/child=시각·입력 요소, 클릭은 UIButton 경유(Button.onClick→OnClickEvent→root SendMessage), 선택 표시는 테두리 방식. 프리팹 생성/리팩터링 시. |
| [02_Ingame/INGAME.md](02_Ingame/INGAME.md) | **인게임 퍼즐.** 게임 루프, 보드 상태, 블럭, 매칭, 뷰 동기화, 애니메이션, 리플레이. ExecuteBatchMovement 처리 순서 규칙. 보드 좌표/레이아웃. |
| [03_Data/DATA.md](03_Data/DATA.md) | **데이터/설정.** JSON 구조, GameSpec, Rule/Stage/Replay 데이터, 데이터 추가 방법. struct vs class 데이터 타입 주의. 스테이지 저장소(StageStorage)·맵 툴(StageMapTool). |
| [04_UI/UI.md](04_UI/UI.md) | **UI 시스템.** 도메인 시스템, 팝업/탭 생명주기, UIButton, 팝업·탭 추가 방법. |
| [05_Scene/SCENE.md](05_Scene/SCENE.md) | **씬/인프라.** 씬 전환, SharedScene, 매니저 구조, AssetManager, PoolManager. 새 씬·매니저 추가. |
| [06_Server/SERVER.md](06_Server/SERVER.md) | **서버 통신.** 서버 API 연동, 공유 DLL(PuzleBattleShared), 네트워크 레이어, 공유 DTO 추가 방법. |
| [CHANGELOG.md](CHANGELOG.md) | **변경 이력.** 전체 작업 변경 기록. 이전 작업 맥락 파악 시 참고. |

---

## 상세 단편 문서

주제 문서에서 분리한 상세 문서. 해당 주제 작업 시 상위 문서에서 링크로 진입.

| 문서 | 상위 | 요약 |
|------|------|------|
| [01_Architecture/ARCHITECTURE_FLOW.md](01_Architecture/ARCHITECTURE_FLOW.md) | ARCHITECTURE | 메인 대전/사이드 스테이지 게임 흐름 상세. |
| [02_Ingame/INGAME_GIMMICK.md](02_Ingame/INGAME_GIMMICK.md) | INGAME | 기믹 인터페이스/파괴 연쇄/새 기믹 추가 절차. |
| [02_Ingame/INGAME_REPLAY.md](02_Ingame/INGAME_REPLAY.md) | INGAME | 리플레이 기록/재생 흐름 상세. |
| [03_Data/DATA_SCHEMA.md](03_Data/DATA_SCHEMA.md) | DATA | GameSpec 전체 필드 스키마 트리 + ReplayData JSON. |
| [03_Data/DATA_STAGE.md](03_Data/DATA_STAGE.md) | DATA | 스테이지 저장소(StageStorage)·맵 툴(StageMapTool) 레퍼런스. |
| [03_Data/STAGE_MAP_TOOL_ARCH.md](03_Data/STAGE_MAP_TOOL_ARCH.md) | DATA_STAGE | 맵 툴(ToolScene) 아키텍처·데이터 흐름·구현 순서. |
| [06_Server/SERVER_DTO.md](06_Server/SERVER_DTO.md) | SERVER | 공유 DLL DTO 필드 정의. |
| [04_UI/UI_SIDE_STAGE.md](04_UI/UI_SIDE_STAGE.md) | UI | 사이드 스테이지 팝업 진입 흐름. |
| [03_Data/STAGE_MAP_TOOL.md](03_Data/STAGE_MAP_TOOL.md) | DATA_STAGE/CHANGELOG | 스테이지 맵 툴 상태·작업 메모. |

---

## 루트 문서 (자동 로드)

| 문서 | 요약 |
|------|------|
| [../../AGENTS.md](../../AGENTS.md) | Codex 작업 원칙 + 코딩 규칙 + 주의사항(Known Pitfalls). 기본 규칙. |
| [../../CLAUDE.md](../../CLAUDE.md) | Claude Code 호환 가이드. AGENTS.md와 동일 규칙. |
| [../../README.md](../../README.md) | 프로젝트 개요, 주요 특징, 기술 스택, 씬 구성. |

---

## 팀 에이전트 문서 (`07_Agents/`)

Codex 팀 에이전트 운용 규칙과 역할별 가이드. 진입점은 [07_Agents/AGENT_TEAM.md](07_Agents/AGENT_TEAM.md).
역할 문서: `AGENT_ARCHITECT` · `AGENT_CODING` · `AGENT_REVIEW` · `AGENT_CONVENTION` · `AGENT_IMAGE_PROMPT`.
