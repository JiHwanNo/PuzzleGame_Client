# PuzleBattleGame 게임 흐름 상세

`ARCHITECTURE.md`의 게임 흐름 분리 문서.

---

## 메인 대전 퍼즐

```
[앱 시작]
  Main (RuntimeInitializeOnLoadMethod) → SharedScene 자동 로드
    → TitleScene (CI 연출)
      → LoadingScene 경유
        → LobbyScene
          → PopupReady 팝업 열기
            → [플레이] 상대 유저 리플레이 데이터 다운로드/로드
              → 리플레이의 Rule/Stage/Seed로 플레이어 GameSpec 준비
              → SetReplayData()
              → LoadingScene 경유
                → GameScene
                  → Board 생성 + Initialize + View 그리기 → 게임 루프
                  → ReplayController 초기화 → 상대 리플레이 자동 재생
                    → 게임 종료(Finish) → 점수 환산/랭킹 반영 → LobbyScene 이동
```

---

## 사이드 스테이지형 콘텐츠

```
[사이드 콘텐츠 진입]
  LobbyScene 사이드 콘텐츠 버튼
    → 모드별 스테이지 전체 팝업
      → 스테이지 번호 선택
        → StageStorage로 Resources 또는 다운로드 StageData 로드
        → StageInjection.MakeGameSpec(ruleAddress, puzzleType, stageId)
          → GameScene 또는 전용 스테이지 씬에서 플레이
```

씬 전환 상세는 `SCENE.md` 참고.
