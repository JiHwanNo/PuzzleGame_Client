# 사이드 스테이지 UI

모드별 사이드 퍼즐 목록을 추가할 때 사용할 팝업 구성안이다. 스테이지 해금과 별 지급의 채택 여부는 [로비 사양서](../../../docs/LOBBY-DEFENSE.md#사이드-퍼즐-해금과-재화에-관한-초기안)에서 확인한다.

---

## 팝업 흐름

사이드 스테이지 목록은 별도 팝업으로 여는 안이다. 현재 로비 플레이 버튼의 진입 경로는 [인게임 사양서](../../../docs/INGAME-BATTLE.md#한-판의-진행)에 정리했다.

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
