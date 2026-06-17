# 문서 인덱스

PuzleBattleGame(Unity 6000.0.38f1, URP) 문서 모음. 작업 영역에 맞는 문서를 골라 읽으세요.
세부 네비게이션(확장 포인트·소스 위치)은 [MAP.md](MAP.md), 코딩 규칙 전체는 [../AGENTS.md](../AGENTS.md) 참고.

---

## 문서 목록

| 문서 | 200자 요약 |
|------|-----------|
| [MAP.md](MAP.md) | **문서 네비게이션.** 작업별 빠른 경로, 디버깅 시 읽을 곳, 확장 포인트 체크리스트(새 모드/블럭/매니저/API 추가 절차), 주요 소스 파일 위치 표. 작업 시작 전 진입점. |
| [ARCHITECTURE.md](ARCHITECTURE.md) | **아키텍처 전반.** MVC 엄격 분리, 데이터 기반 생성, 결정론적 리플레이, 인터페이스 기반 확장 원칙. 데이터 흐름·게임 흐름·폴더 구조. 결정론 규칙(난수/시간). |
| [CONVENTIONS.md](CONVENTIONS.md) | **커밋 전 코드 리뷰 필독.** 리뷰 체크리스트, 위반 예시와 수정 가이드. 명명/스타일/주석 규칙 검증 기준. PR 올리기 전 반드시 확인. |
| [INGAME.md](INGAME.md) | **인게임 퍼즐.** 게임 루프, 보드 상태, 블럭, 매칭, 뷰 동기화, 애니메이션, 리플레이. ExecuteBatchMovement 처리 순서 규칙. 보드 좌표/레이아웃. |
| [DATA.md](DATA.md) | **데이터/설정.** JSON 구조, GameSpec, Rule/Stage/Replay 데이터, 데이터 추가 방법. struct vs class 데이터 타입 주의. 스테이지 저장소(StageStorage)·맵 툴(StageMapTool). |
| [UI.md](UI.md) | **UI 시스템.** 도메인 시스템, 팝업/탭 생명주기, UIButton, 팝업·탭 추가 방법. |
| [SCENE.md](SCENE.md) | **씬/인프라.** 씬 전환, SharedScene, 매니저 구조, AssetManager, PoolManager. 새 씬·매니저 추가. |
| [SERVER.md](SERVER.md) | **서버 통신.** 서버 API 연동, 공유 DLL(PuzleBattleShared), 네트워크 레이어, 공유 DTO 추가 방법. |
| [CHANGELOG.md](CHANGELOG.md) | **변경 이력.** 전체 작업 변경 기록. 이전 작업 맥락 파악 시 참고. |

---

## 루트 문서 (자동 로드)

| 문서 | 요약 |
|------|------|
| [../AGENTS.md](../AGENTS.md) | Codex 작업 원칙 + 코딩 규칙 + 주의사항(Known Pitfalls). 기본 규칙. |
| [../CLAUDE.md](../CLAUDE.md) | Claude Code 호환 가이드. AGENTS.md와 동일 규칙. |
| [../README.md](../README.md) | 프로젝트 개요, 주요 특징, 기술 스택, 씬 구성. |
