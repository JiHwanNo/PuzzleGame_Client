# PuzleBattleGame - 코드 리뷰 체크리스트

커밋 전 리뷰 에이전트가 **반드시 읽고 기준으로 삼는 문서**. 모든 항목은 위반 시 **수정 대상**이다.
코딩 규칙은 `../../../AGENTS.md`, 아키텍처는 `ARCHITECTURE.md`, 세부 영역은 각 영역별 MD(`INGAME.md`, `DATA.md` 등)를 참조.

---

## 0. 리뷰 원칙

- **스타일 교정 시 실행 로직/메서드 호출/조건문 결과를 절대 변경하지 않는다.**
- 판단이 애매하면 수정하지 말고 그대로 둔다.
- 리팩토링/개선 제안은 하지 않는다. 이번 커밋 범위 밖이다.
- `.meta`, JSON 데이터, 자동 생성 파일(`*.cs.meta`, `*Generated.cs`)은 검토 제외.
- 외부 DLL(`Plugins/*.dll`) 관련 파일은 검토 제외.

---

## 1. 코딩 규칙 (AGENTS.md 준수)

### 1-1. 파일 및 인코딩
- [ ] 새로 추가된 C# 파일은 **UTF-8 (BOM 포함)**.
- [ ] 모든 주석/설명은 **한글**.
- [ ] 모든 메서드와 매개변수에 **한글 XML 주석(`///`)** 작성.
- [ ] 복잡한 로직/데이터 구조 수정 시 의도 설명 주석 추가.

### 1-2. 명명 규칙

| 대상 | 규칙 | 예시 |
|------|------|------|
| 클래스/메서드/공개 필드/구조체/열거형 | `PascalCase` | `PuzzleGameController` |
| 인터페이스 | `I` + `PascalCase` | `IPuzzleBoard` |
| 비공개 필드 | `_camelCase` | `_board`, `_frameCount` |
| 지역 변수/매개변수 | `camelCase` | `targetPos`, `stageData` |

**위반 예시 → 수정**
```csharp
// ✗ 위반
private int boardSize;        // 비공개 필드인데 _camelCase 아님
public void do_something() {} // PascalCase 아님

// ✓ 수정
private int _boardSize;
public void DoSomething() {}
```

### 1-3. Allman 스타일 (중괄호)

- [ ] 모든 제어문은 **단 한 줄이어도 반드시 중괄호 `{}`** 사용.
- [ ] 중괄호는 **새 줄에서 시작**.

**위반 예시 → 수정**
```csharp
// ✗ 위반
if (condition) DoSomething();
if (condition) {
    DoSomething();
}
for (int i = 0; i < 10; i++) DoSomething();

// ✓ 수정
if (condition)
{
    DoSomething();
}
for (int i = 0; i < 10; i++)
{
    DoSomething();
}
```

### 1-4. 에셋 및 리소스
- [ ] 에셋 로드는 **`AssetManager` 경유**. `Addressables.LoadAssetAsync` **직접 호출 금지**.
- [ ] 인게임 시각 요소는 **`Ingame` Sorting Layer**에서 렌더링.

---

## 2. 아키텍처 규칙 (MVC 엄격 분리)

### 2-1. Model 레이어 제약 (`PuzzleCore/Module/`)
- [ ] **`UnityEngine` 종속성 금지** — `using UnityEngine;` 없어야 함.
- [ ] `Debug.Log`, `Debug.LogError`, `MonoBehaviour`, `GameObject`, `Transform` 등 사용 금지.
- [ ] 로깅이 필요하면 **`Action<string> OnLog` 델리게이트 + `Log()` 메서드** 사용.

**위반 예시 → 수정**
```csharp
// ✗ 위반 (Model 레이어)
using UnityEngine;
public class ThreeMatchPuzzleBoard
{
    public void Process()
    {
        Debug.LogError("failed");
    }
}

// ✓ 수정
public class ThreeMatchPuzzleBoard
{
    public Action<string> OnLog;
    private void Log(string msg) { OnLog?.Invoke(msg); }
    public void Process()
    {
        Log("failed");
    }
}
```

### 2-2. View/Controller 역할
- [ ] View는 Model의 `BoardViewAction` 이벤트를 수신하여 **시각적 연출만** 담당.
- [ ] Controller는 유저 입력 감지/큐 저장, 틱 단위로 Model Update 호출.
- [ ] 데이터 기반 생성: JSON → `GameSpec` 병합 후 Model 주입. **하드코딩 금지**.

### 2-3. 결정론적 리플레이 (Model 레이어)
- [ ] 랜덤은 **`PuzzleRandom` + `GameSpec.randomSeed`** 기반. `UnityEngine.Random` / `System.Random` 직접 사용 금지.
- [ ] 시간은 **논리 프레임** 기반. `Time.deltaTime` / `Time.time` / `DateTime.Now` 사용 금지.
- [ ] **`Dictionary`/`HashSet` 순회 순서에 의존하는 로직 금지** → `List` + 명시적 정렬.
- [ ] `float == float` 직접 비교 금지 → `Mathf.Approximately` 또는 정수 프레임.
- [ ] 프레임 전진은 **`FixedUpdate`**에서만.
- [ ] 허용 영역(View 애니메이션, UI 연출, 시드 생성, 파일명 타임스탬프)은 예외. 상세: `ARCHITECTURE.md` §4.

---

## 3. Known Pitfalls (AGENTS.md 주의사항)

### 3-1. struct vs class null 비교
- [ ] 다음 타입은 **struct**이므로 `== null` 비교 **컴파일 에러**(CS0019):
  - `RuleData`, `ObjectiveData`, `InputRecord`, `InputEndRecord`, `GridPos`
- [ ] null 체크는 **class 컨테이너**에서 수행: `GameSpec`, `GameRuleContainer`, `BlockData`, `StageData`, `CellData`, `ReplayData`.

**위반 예시 → 수정**
```csharp
// ✗ 위반 (RuleData는 struct)
if (ruleContainer.rule == null) { ... }

// ✓ 수정 — class 컨테이너로 체크
if (ruleContainer == null) { ... }
```

### 3-2. 정렬 안정성
- [ ] `List.Sort()`는 **불안정 정렬**. 같은 키 요소의 상대 순서 보장 안 됨.
- [ ] 삽입 순서에 의존하는 로직이 있으면 **처리 순서 분리** 또는 **명시적 타이브레이커** 추가.
- [ ] 실제 사례: `FetchActions()` 정렬 시 Fall과 CreateAndFall 순서 뒤바뀜 → `ExecuteBatchMovement` 루프 분리로 해결.

### 3-3. 뷰 액션 처리 순서
- [ ] `ExecuteBatchMovement`에서 **Move/Fall을 반드시 먼저** 처리한 후 CreateAndFall 처리.
- [ ] 순서가 바뀌면 CreateAndFall의 `HandleImmediateDestroy`가 이동 예정 블럭을 파괴.
- [ ] 상세: `INGAME.md` "ExecuteBatchMovement 처리 순서 규칙".

### 3-4. LINQ 잔존 확인
- [ ] `using System.Linq` 제거 후 `.Last()`, `.Any()`, `.Where()`, `.Select()`, `.ToList()` 등이 **남아있지 않은지 확인**.
- [ ] LINQ 재도입 시 정렬 안정성/지연 평가 차이 확인.

### 3-5. 컬렉션 재사용
- [ ] 반환된 버퍼/리스트 참조가 외부에서 보관되는 곳이 없는지 확인.
  - 예: `GetConnectedBlocks`의 `_connectedBuffer`.
- [ ] 코루틴에 전달된 리스트가 yield 중 외부에서 변경되지 않는지 확인.

### 3-6. Pool/Addressable 생명주기
- [ ] `PoolManager`는 `DontDestroyOnLoad`로 씬 전환 후에도 풀 유지.
- [ ] `AssetManager.ReleaseAll()`은 `MarkPersistent()` 되지 않은 에셋 해제.
- [ ] 풀에 남은 인스턴스가 해제된 에셋을 참조하지 않는지 씬 전환 전후 확인.

---

## 4. 명백한 버그 패턴

- [ ] **null 역참조 가능성**: 초기화되지 않은 참조 접근.
- [ ] **배열/리스트 범위 초과**: `[i]` 접근 전 `Count`/`Length` 체크 누락.
- [ ] **자원 누수**: `IDisposable.Dispose()` 누락, 이벤트 구독(`+=`) 후 해제(`-=`) 누락.
- [ ] **무한 루프**: 탈출 조건 없는 `while (true)`, 카운터 증가 누락.
- [ ] **상수 조건문**: `if (true)`, `if (false)`, `if (x == x)` 같은 의미 없는 비교.
- [ ] **오타 필드/메서드 참조**: 유사한 이름(`Postion` vs `Position`) 오타로 의도와 다른 멤버 참조.
- [ ] **대입 vs 비교**: `if (x = 5)` (대입) vs `if (x == 5)` (비교).
- [ ] **부동소수점 비교**: `float == float`은 오차 위험. `Mathf.Approximately` 또는 epsilon 비교 권장.

---

## 5. 수정 후 체크

- [ ] 커밋/리뷰 작업에서 사용자가 스테이징을 요청한 경우, 수정한 파일을 `git add <파일경로>`로 재스테이징.
- [ ] 커밋 메시지는 변경 의도가 드러나는 **한글 문장**으로 작성.
- [ ] 수정 결과는 **한 줄 요약**으로 보고.
  - 예: `수정 3건: Allman 위반 2, Debug.LogError 1 (Model 레이어)`
  - 문제 없으면: `문제 없음`.

---

## 참고 문서

| 상황 | 읽을 곳 |
|------|---------|
| 프로젝트 코딩 규칙 전체 | `../../../AGENTS.md` |
| MVC 분리/리플레이/게임 흐름 | `ARCHITECTURE.md` |
| 게임 루프/뷰 동기화/블럭 | `INGAME.md` |
| 데이터 타입/JSON | `DATA.md` |
| 서버/DTO | `SERVER.md` |
