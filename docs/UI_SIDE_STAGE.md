# 사이드 스테이지 UI

사이드 스테이지형 콘텐츠의 팝업 진입 흐름을 정리한다.

---

## 팝업 흐름

사이드 스테이지형 콘텐츠는 메인 대전 팝업과 분리한다.

```text
Lobby 버튼
  → DomainManager.Instance.OpenPopup("Popup{Mode}Stage")
    → 스테이지 전체 목록 팝업 표시
      → 선택한 stageId로 StageInjection.MakeGameSpec(ruleAddress, puzzleType, stageId)
        → GameScene 또는 사이드 콘텐츠 전용 씬 이동
```

---

## 권장 팝업 이름

- `PopupThreeMatchStage`
- `PopupLinkStage`
- `PopupTapMatchStage`

각 팝업은 스테이지 목록, 잠금/클리어/별 상태, 선택 스테이지 시작 처리를 담당한다.
