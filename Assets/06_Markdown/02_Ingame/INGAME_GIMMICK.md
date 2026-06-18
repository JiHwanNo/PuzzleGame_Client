# PuzleBattleGame 기믹 시스템 상세

`INGAME.md`의 블럭 기믹 컴포지션 상세 문서. 블럭 단일화 구조 개요는 `INGAME.md` "블럭 아키텍처" 참고.

---

## 기믹 시스템 (`Module/Gimmick/`)
| 타입 | 역할 |
|------|------|
| `IGimmick` | 기믹 계약. `OnAttach(owner)`, `OnDestroyed(board, myPos)`, `OnTouched(board, myPos)`, `OnSwapped(board, myPos, targetPos)` 훅 정의 |
| `IGimmickHost` | 기믹을 부착받는 호스트 계약. `List<IGimmick> Gimmicks`, `AddGimmick(gimmick)` (현재 구현체는 `Block`) |
| `GimmickBase` | 모든 훅에 빈 기본 구현 제공(추상). 구체 기믹은 필요한 훅만 `override` |
| `BombGimmick` | 파괴 시 **원형(유클리드 거리) 반경** 내 블럭을 연쇄 파괴. 생성자 `radius`(최소 1) |
| `GimmickFactory` | `GimmickType`(enum) → 기믹 인스턴스 생성. `Bomb` → `BombGimmick(2)`(반경 2) |
| `GimmickUtil` | 파괴 공통 헬퍼(`internal static`). 목표 갱신 + `Destroy` 뷰 등록 + 연쇄 기믹 발화를 한 곳에 모음 |

## 파괴/연쇄 흐름
```
보드가 블럭 파괴 결정
  → destroyed.FireDestroyed(board, pos)        ← Block이 부착된 모든 기믹의 OnDestroyed 발화
    → BombGimmick.OnDestroyed(board, myPos)     ← 원형 반경 순회
      → GimmickUtil.DestroyBlock(board, target) ← 대상 파괴 (빈 칸/막힘·잠금 셀이면 종료)
        → cell.Block = null (먼저 비워 중복·무한 연쇄 차단)
        → board.AddView(Destroy)               ← 연출 프레임/order는 AddView가 스탬프
        → destroyed.FireDestroyed(...)          ← 연쇄 (폭탄이 폭탄을 터뜨림)
```
- 보드의 파괴 경로(ThreeMatch/Link/TapMatch 모두)는 블럭 제거 시 `destroyed.FireDestroyed(this, pos)`를 호출한다.
- **연출 책임 분리**: 기믹/`Block`은 `frame`/`orderIndex`(연출 데이터)를 다루지 않는다. 보드의 `AddView`가 frame을 스탬프하고 order를 부여한다.
- `IGimmick`은 `OnTouched`/`OnSwapped` 훅도 정의하지만, 현재 보드는 입력을 `inputType`으로 게이팅하며 **파괴 경로(`OnDestroyed`)만 발화**한다. 터치/스왑 훅은 향후 확장용으로 예약되어 있다.

## 새 블럭(기믹) 추가 절차
특정 행동이 필요 없는 일반 색 블럭은 **JSON에 BlockData만 추가**하면 된다(코드 변경 불필요). 새 행동(기믹)이 필요한 경우:
1. `Module/Gimmick/`에 `GimmickBase` 상속 기믹 클래스 작성, 필요한 훅(`OnDestroyed` 등)만 `override`.
2. `PuzzleDefine.cs`의 `GimmickType` enum에 값 추가.
3. `GimmickFactory.Create()`에 `GimmickType → 기믹 인스턴스` 생성 분기 추가(파라미터는 여기서 주입).
4. Rule JSON `blocks[]`의 BlockData에 `gimmickIds`(문자열 목록, 예: `["Bomb"]`)와 필요한 `inputType` 추가 → 상세: `DATA.md`.
- `inputType`(조작 방법)과 `gimmickIds`(행동)는 **서로 독립적인 데이터**다.
