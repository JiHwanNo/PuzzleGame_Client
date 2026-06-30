# PuzleBattleGame 효과(기믹) 시스템 상세

`INGAME.md`의 블럭 효과 컴포지션 상세 문서. 블럭 단일화 구조 개요는 `INGAME.md` "블럭 아키텍처" 참고.

> **핵심 개념**: 기믹은 더 이상 "명사(클래스)"가 아니라, **블럭 속성 데이터(HP·피격소스) + 효과 동사(verb)의 조합**이다.
> "라인 폭탄"이라는 블럭이 따로 있는 게 아니라, `DestroyLine`이라는 **동사**를 데이터로 부착한 블럭일 뿐이다.

---

## 효과 시스템 (`Module/Effect/`)
| 타입 | 역할 |
|------|------|
| `IBlockEffect` | 효과 동사 계약. `EffectTrigger Trigger`(데이터 trigger로 주입) + `Apply(board, myPos, context)`(동작 수행, 소비 시 true) |
| `DestroyRadiusEffect` | 원형(유클리드 거리) 반경 내 블럭에 폭발 데미지. 생성자 `(trigger, radius)`. 기본 trigger `OnDestroyed` |
| `DestroyLineEffect` | 자신을 지나는 행/열(또는 십자) 라인에 폭발 데미지. 생성자 `(trigger, LineDirection)`. 기본 trigger `OnDestroyed` |
| `DestroySameColorEffect` | 대상과 같은 색(blockId) 블럭을 보드 전체에서 파괴하고 **자신을 소비**(`BlockDamage.Destroy`). 기본 trigger `OnSwapped` |
| `EffectFactory` | `EffectData`(trigger/action/param) → 효과 동사 인스턴스 생성. action으로 동사 선택, **trigger/param 해석해 주입**(미지정 시 동사별 기본 trigger) |
| `BlockDamage` | 데미지 공통 헬퍼(`internal static`). `Damage`: **이번 스텝 중복 차단 → 피격소스 게이팅(damagedBy) → HP 감소 → 0이면 파괴**. `Destroy`: 게이팅 없는 "활동 소비" 무조건 제거(무지개 자기 제거용) |

> **발화 시점은 데이터가 결정**한다. 각 동사는 `Trigger`를 데이터 `trigger`로 주입받으므로(미지정 시 위 기본값), `{trigger:"OnSwapped", action:"DestroyRadius"}`처럼 "스왑 시 폭발" 같은 조합도 데이터로 가능하다.
> **한 스텝 = 칸당 최대 1대**: 한 번의 매치 해소/스왑에서 같은 칸은 데미지를 1회만 받는다(`_damageBuffer`로 추적). 폭탄 Splash와 인접 매치가 같은 칸에 겹쳐도 HP는 1만 깎인다.

## 블럭 = 속성 + 효과 동사
블럭은 다음 데이터로 정의된다 (상세 스키마: `DATA.md`):
- **`life`** — HP(내구도). 피격마다 1 감소, 0이면 파괴. (미지정/0이면 1)
- **`damagedBy`** — HP를 깎는 데미지 소스 목록. 미지정 시 일반 블럭 기본값 `Match|Splash`.
  - `Match` 자기 매치(일반팡) · `NeighborMatch` 직교 인접 매치 · `Splash` 폭발 여파(폭탄팡)
- **`effects`** — 부착할 효과 동사 목록(trigger + action + param).

### 데이터만으로 만들어지는 블럭 (효과 동사 없음)
효과가 없는 장애물은 **클래스 없이 순수 데이터**다:
| 블럭 | 데이터 | 설명 |
|------|--------|------|
| 과자 장애물 | `damagedBy:["NeighborMatch","Splash"]`, `effects:[]` | 인접 매치/폭발에만 파괴. 매치 비대상이라 일반팡으론 안 사라짐 |
| 금이 간 벽돌(폭탄 전용) | `damagedBy:["Splash"]`, `inputType:[]`, `effects:[]` | 폭발에만 파괴. 조작/매치 불가 |
| 다중 HP 장애물 | `life:2`, `damagedBy:["NeighborMatch","Splash"]` | 두 번 피격되어야 파괴 |

## 파괴/연쇄 흐름 (폭탄류)
```
보드가 블럭 파괴 결정
  → destroyed.FireDestroyed(board, pos)         ← Block이 OnDestroyed 효과 동사만 골라 발화
    → DestroyRadiusEffect.Apply(board, myPos)   ← 원형 반경 순회
      → BlockDamage.Damage(board, target, Splash)
        → target.TakeDamage(Splash)             ← damagedBy 게이팅 + HP--. 면역/HP잔존이면 종료
        → cell.Block = null (먼저 비워 중복·무한 연쇄 차단)
        → board.AddView(Destroy)                ← 연출 프레임/order는 AddView가 스탬프
        → destroyed.FireDestroyed(...)          ← 연쇄 (폭탄이 폭탄을 터뜨림)
```
- 보드의 직접 매치 파괴 경로(ThreeMatch/Link/TapMatch 모두)는 블럭 제거 시 `destroyed.FireDestroyed(this, pos)`를 호출한다.
- **연출 책임 분리**: 효과/`Block`은 `frame`/`orderIndex`(연출 데이터)를 다루지 않는다. 보드의 `AddView`가 frame을 스탬프하고 order를 부여한다.
- **데미지 소스 게이팅**: `BlockDamage.Damage`는 블럭의 `damagedBy`에 소스가 없으면 무시한다. 폭발이 모든 블럭을 무조건 부수지 않으며, "폭탄 전용 장애물"도 별도 코드 없이 데이터로 성립한다.

### 스왑 소비 흐름 (무지개 폭탄)
```
ProcessSwapInput(first, second)  ← inputType.Swap 게이팅 통과 후
  → cellA.Block.FireSwapped(this, first, second)   ← 물리 스왑 "전"에 OnSwapped 효과 발화
    → DestroySameColorEffect.Apply(...)            ← 대상 색 전체 파괴 + 자신 파괴 → true 반환
  → (소비됨) State = Falling                        ← 매치 검사/복구를 건너뛰고 낙하·보충으로 진입
```
- `FireSwapped`가 true(소비)면 일반 매치 검사/원상복구를 수행하지 않고 곧바로 낙하 단계로 넘어간다. 빈 칸을 만들었으나 즉시 매치가 없을 수 있으므로 `Matching`이 아니라 `Falling`으로 진입한다.

### 매치 파괴 흐름 (일반팡, HP 경로)
```
ProcessMatching()
  → _damageBuffer.Clear()                          ← 이번 스텝 피격 집합 초기화
  → 각 매치 칸: block.TakeDamage(Match)            ← "한 매치 = 한 대". HP 0이면 파괴+FireDestroyed, 남으면 자리 유지(금만 감)
  → DamageNeighborsOfMatches(matches)              ← 인접 장애물 데미지(아래)
  → return (파괴가 하나라도 있었나)                 ← 모두 HP만 깎였으면 false → 낙하 생략(무한 재매치 방지)
```
- 일반 블럭(life=1, 기본 damagedBy=Match|Splash)은 `1→0` 즉시 파괴 → 기존 동작과 동일.
- `life:2` 매치 블럭을 데이터로 주면 두 번 매치해야 깨진다(다중 HP 매치 블럭).

### 인접 매치 흐름 (과자 장애물)
```
ProcessMatching()  ← 매치 칸 파괴 후
  → DamageNeighborsOfMatches(matches)              ← 보드를 (x,y)로 스캔(결정론적)
    → BlockDamage.Damage(this, pos, NeighborMatch, _damageBuffer) ← 직교 인접 칸에 인접 매치 데미지
      → (damagedBy에 NeighborMatch 있는 블럭만 HP--) ← 일반 블럭은 면역이라 무영향
```
- 과자는 효과 동사가 없어도 `damagedBy:["NeighborMatch"]` 데이터만으로 파괴된다. **별도 기믹 클래스가 필요 없다.**

## 새 동작 추가 절차
대부분의 새 블럭은 **JSON에 BlockData만 추가**하면 된다(코드 변경 불필요):
- 일반 색 블럭, 장애물(과자·벽돌), 기존 동사 조합 블럭 → 데이터만.
- 예: "라인+원형 동시 폭발"은 `effects`에 `DestroyLine`과 `DestroyRadius`를 둘 다 넣으면 끝.

**진짜 새로운 동사**(예: "얼음으로 변환")가 필요할 때만 코드를 추가한다:
1. `Module/Effect/`에 `IBlockEffect` 구현 동사 클래스 작성(`Trigger` 지정 + `Apply` 구현, 파라미터는 생성자 주입).
2. `EffectFactory.Create()`에 `action 문자열 → 동사 인스턴스` 분기 추가(param 해석 포함).
3. Rule JSON `blocks[]`의 BlockData `effects`에 `{trigger, action, param}` 추가 → 상세: `DATA.md`.
- `inputType`(조작 방법)·`damagedBy`(피격 소스)·`effects`(행동)는 **서로 독립적인 데이터**다.
