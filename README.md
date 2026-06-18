# PuzleBattleGame

Unity 6000.0.38f1 (URP) 기반의 확장 가능한 퍼즐 배틀 게임 프레임워크.
3매치, 링크, 탭 매치 등 다양한 퍼즐 규칙을 JSON 데이터로 유연하게 적용할 수 있도록 설계.

## 주요 특징

- **데이터 기반 설계**: 게임 규칙과 스테이지를 JSON으로 정의, `GameSpec`으로 병합하여 Model에 주입
- **MVC 분리**: 순수 C# Model / Unity View / Controller 입력 루프로 엄격한 관심사 분리
- **인터페이스 확장**: `IPuzzleBoard` + Capability 기반 블럭으로 새로운 퍼즐 모드 유연하게 추가
- **결정론적 리플레이**: 시드 기반 난수, 프레임 단위 입력 큐로 재현 가능한 게임 플레이
- **Addressables 에셋 관리**: AssetManager를 통한 비동기/동기 로드 및 캐싱

## 기술 스택

| 항목 | 기술 |
|------|------|
| 엔진 | Unity 6000.0.38f1 |
| 렌더 파이프라인 | Universal Render Pipeline (URP) |
| UI | TextMesh Pro (TMP) |
| 에셋 관리 | Addressables |
| 데이터 | JsonUtility + Serializable Classes |

## 씬 구성

`SharedScene(상주)` → `TitleScene` → `LoadingScene` → `LobbyScene` → `GameScene`

## 문서

상세 문서는 [`Assets/06_Markdown/`](Assets/06_Markdown/README.md) 폴더 참고. 콘텐츠별 폴더로 묶여 있으며, 작업 영역별 200자 요약 + 링크로 정리되어 있습니다.

## 향후 계획

- [ ] 적(Enemy) AI 및 배틀 연동
- [ ] 추가 특수 블럭 확장 (줄 제거 등)
