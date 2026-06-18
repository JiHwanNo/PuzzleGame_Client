# PuzleBattleGame 리플레이 상세

`INGAME.md`의 리플레이 시스템 분리 문서.

---

## 기록 흐름

```
게임 시작 → StageInjection이 randomSeed 생성 → Board.Initialize(spec)에 시드 주입
게임 중   → Board가 InputRecord / InputEndRecord 자동 기록
게임 종료 → PuzzleGameController가 Finish 감지
  → board.GetRecordedInputs() + GetRecordedInputEnds()
    → ReplayData 조립 → ReplayStorage.Save() → JSON 파일 저장
```

---

## 재생 흐름

```
ReplayData 로드 → GameSpec 복원 (동일 Rule/Stage + 동일 시드)
  → IPuzzleBoard 생성 + Initialize
    → FixedUpdate에서 프레임 카운터 증가
      → 해당 프레임의 Input/InputEnd 기록을 보드에 주입
        → 동일한 게임 결과 재현
```

---

## ReplayController 배치

- 메인 보드와 독립적으로 동작 (별도 IPuzzleBoard + PuzzleBoardView).
- `viewScale`, `margin` 파라미터로 우측 상단에 축소 자동 배치.
- `PuzzleBoardView.skipCameraAlign = true`로 카메라 간섭 방지.
