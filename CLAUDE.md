# PuzleBattleGame - Claude Code 개발 가이드라인

> Codex와 작업할 때의 기본 가이드는 `AGENTS.md`를 사용한다.
> 이 문서는 Claude Code 호환을 위해 유지한다.

Unity 6000.0.38f1 (URP) 기반 퍼즐 배틀 게임 프레임워크.
세부 작업 시 `docs/MAP.md`를 읽고 해당 참고 문서로 이동.

기능별 구현 파일은 [docs/MAP.md](docs/MAP.md)의 작업별 빠른 경로와 각 영역 문서의 **콘텐츠와 C# 매핑** 표에서 확인한다. C# 파일 추가·이동·책임 변경 시 해당 매핑도 함께 갱신한다.

---

## 문서와 코드 동기화 (매 작업 필수)

[AGENTS.md의 문서와 코드 동기화 지침](AGENTS.md#문서와-코드-동기화-매-작업-필수)을 동일하게 적용한다. 커밋 여부와 관계없이 매 작업 완료 전 문서 영향을 점검하고, 필요한 매핑·본문·진입 링크를 같은 작업에서 갱신·검증하며 결과를 보고한다.

---

## 코딩 규칙

### 파일 및 인코딩
- 모든 소스 파일은 **UTF-8 (BOM 포함)** 형식.
- 코드 내 모든 설명 및 주석은 **한글**로 작성.
- 모든 메서드 및 매개변수에 한글 XML 주석(`///`) 작성 필수.
- 복잡한 로직이나 데이터 구조를 수정할 때는 의도를 명확히 설명하는 주석을 추가.

### 명명 규칙
| 대상 | 규칙 | 예시 |
|------|------|------|
| 클래스/메서드/공개 필드/구조체/열거형 | `PascalCase` | `PuzzleGameController` |
| 인터페이스 | `I` + `PascalCase` | `IPuzzleBoard` |
| 비공개 필드 | `_camelCase` | `_board`, `_frameCount` |
| 지역 변수/매개변수 | `camelCase` | `targetPos`, `stageData` |

### 코드 스타일
- **Allman 스타일**: 모든 제어문은 단 한 줄이어도 반드시 중괄호 `{}` 사용, 중괄호는 새 줄에서 시작.
```csharp
if (condition)
{
    DoSomething();
}
```
- 스타일 교정 시 기존 실행 로직/메서드 호출/조건문 결과를 절대 변경하지 않는다.

### 명시적인 변수 타입

- 지역 변수는 `var` 대신 실제 타입을 명시한다. `foreach` 변수와 `out var` 선언에도 동일하게 적용한다.
- 예: `var cell = GetCell(pos);` 대신 `PuzzleCell cell = GetCell(pos);`로 작성한다.
- 기존 코드 수정 시 추론되던 타입과 동일한 타입을 사용하여 오버로드 선택, 값 복사 및 실행 동작을 유지한다.

### 람다 및 null 처리

- 람다는 **본문이 3줄 이하인 경우에만 허용**한다. 그보다 길면 이름 있는 메서드 또는 지역 함수로 분리한다.
- 본문 줄 수는 중괄호만 있는 줄, 빈 줄, 주석만 있는 줄을 제외한 코드 줄로 센다. 여러 문장을 한 줄에 합쳐 기준을 맞추지 않는다.
- 단순 반환 메서드·프로퍼티와 단순 필드 접근자는 `=>` 식 본문으로 간결하게 작성한다. 식 본문은 람다와 구분하며, 분기·null 검사·여러 처리 단계가 필요하면 일반 블록을 유지한다.
- `??`, `?.` 사용은 지양하고 명시적인 null 검사와 `if`문으로 작성한다.
- 변경 시 기존 null 처리, 평가 순서, 콜백의 호출 시점과 캡처 변수의 수명을 유지한다.

### 에셋 및 리소스
- 모든 게임 리소스는 **Addressables** 시스템을 통해 관리.
- 에셋 로드 시 반드시 `AssetManager.cs` 경유. 직접 `Addressables.LoadAssetAsync` 호출 금지.
- 인게임 시각 요소는 **`Ingame` Sorting Layer**에서 렌더링.

### 디버깅
- 씬 뷰/GUI 디버그 정보는 **실제 뷰 객체(View Object) 상태**를 우선 반영.
- 씬 이동/데이터 로드 실패 시 명확한 에러 로그 남길 것.

---

## 아키텍처 개요

MVC 엄격 분리, 데이터 기반 생성, 결정론적 리플레이, 인터페이스 기반 확장.
상세는 `docs/ARCHITECTURE.md` 참고.

---

## 주의사항 (Known Pitfalls)

### 데이터 타입 (struct vs class)
- JSON 직렬화 데이터 중 **struct**: `RuleData`, `ObjectiveData`, `InputRecord`, `InputEndRecord`, `GridPos`
- JSON 직렬화 데이터 중 **class**: `GameSpec`, `GameRuleContainer`, `BlockData`, `StageData`, `CellData`, `ReplayData`
- struct는 `== null` 비교 불가. 파싱 실패 시 기본값(zero)이 들어가므로, class 컨테이너의 null 체크로 파싱 실패를 감지.
- 실제 사례: `StageInjection`에서 `ruleContainer.rule == null` 비교 시 CS0019 컴파일 에러 발생.

### Model 레이어 로깅
- `PuzzleCore/Module/` 하위 클래스는 `UnityEngine` 종속성 금지이므로 `Debug.LogError` 사용 불가.
- 대신 `Action<string> OnLog` 델리게이트 + `Log()` 메서드로 외부에 로그 전달.

### 정렬 안정성
- `List.Sort()`는 **불안정 정렬**. 동일 키 요소의 상대 순서 보장 안 됨.
- 삽입 순서 의존 로직은 **처리 순서 분리** 또는 **명시적 타이브레이커** 필요.
- 사례: `FetchActions()` 정렬 시 Fall/CreateAndFall 순서 뒤바뀜 → `ExecuteBatchMovement` 루프 분리로 해결.

### 뷰 액션 처리 순서
- `ExecuteBatchMovement`는 Move/Fall → CreateAndFall 순서 분리 필수. 상세: `docs/INGAME.md` "ExecuteBatchMovement 처리 순서 규칙".

### 기믹 컴포지션 / 연출 책임 분리
- 블럭은 단일 `Block` 클래스 + 기믹 부착(`IGimmick`) 구조. `BaseBlock`/`NormalBlock`/`BombBlock` 서브클래스는 폐기됨 — 새 행동은 상속이 아니라 기믹으로 추가.
- 조작 가능 여부(스왑/링크/터치)는 데이터(`inputType` 플래그)로 보드에서 게이팅. 블럭/기믹에 `return true` 하드코딩 금지.
- **기믹/`Block`은 `frame`/`orderIndex`(연출 데이터)를 다루지 않는다.** 파괴 큐 등록은 `GimmickUtil.DestroyBlock` → `board.AddView`가 담당하며, frame 스탬프/order 부여는 `AddView`의 책임.
- 파괴 연쇄는 `destroyed.FireDestroyed(board, pos)` → 부착 기믹 `OnDestroyed` 경로로만 발화. `GimmickUtil.DestroyBlock`은 빈 칸/막힘·잠금 셀에서 종료하므로 무한 연쇄가 방지됨.
- `inputType`/`gimmickIds`는 JSON 문자열 목록 → `Enum.TryParse`로 enum 변환. 새 값은 `GimmickType` enum + `GimmickFactory` 분기를 함께 갱신. 상세: `docs/INGAME.md`, `docs/DATA.md`.

### 결정론 (Model 레이어)
- `UnityEngine.Random` / `System.Random` / `Time.deltaTime` / `DateTime.Now` / Dictionary 순회 의존 금지.
- 랜덤은 `PuzzleRandom`(시드 주입), 시간은 논리 프레임. 상세: `docs/ARCHITECTURE.md` §4.

### 최적화 체크리스트
- LINQ 제거 시: 정렬 안정성, 지연 평가(Lazy Evaluation) 차이 확인.
- 컬렉션 재사용 시: 반환 참조의 외부 보관 여부 확인 (예: `_connectedBuffer`).
- 코루틴 전달 리스트: yield 중 외부 변경 여부 확인.
- Dictionary foreach 대신 List 기반 병렬 인덱스 (Enumerator GC 방지).
- `using System.Linq` 제거 후 `.Last()`, `.Any()`, `.Where()` 잔존 확인.

### FetchActions() 리스트 스왑 패턴
- 내부 `_views` 리스트를 **참조 스왑** 방식으로 반환 (`var res = _views; _views = new List<>(); return res;`).
- 반환된 리스트는 호출자 소유, 다음 호출/코루틴 yield 중 안전.

### ExecuteBatchMovement 리스트 기반 매핑
- Dictionary 대신 `_batchActions`/`_batchViews` 병렬 인덱스 리스트 사용 (GC 할당 방지, 삽입 순서 보장).

### Pool/Addressable 생명주기
- `PoolManager`는 `DontDestroyOnLoad`로 씬 전환 후에도 유지.
- `AssetManager.ReleaseAll()`은 `MarkPersistent()` 되지 않은 에셋 해제.
- 풀 인스턴스가 해제된 에셋을 참조할 수 있으므로 씬 전환 전후 상태 주의.

---

## 작업별 참고 문서

| 작업 | 문서 |
|------|------|
| **커밋 전 코드 리뷰 (필독)** | **`docs/CONVENTIONS.md`** |
| 아키텍처/게임 흐름/폴더 구조 | `docs/ARCHITECTURE.md` |
| 인게임 퍼즐 (보드, 블럭, 매칭, 뷰, 애니메이션, 리플레이) | `docs/INGAME.md` |
| 데이터/설정 (JSON, GameSpec, 추가 방법, ReplayData) | `docs/DATA.md` |
| UI/팝업/탭 (도메인 시스템, UIButton) | `docs/UI.md` |
| 씬/매니저/인프라 (씬 전환, AssetManager, Pool) | `docs/SCENE.md` |
| 서버 통신/API/공유 DTO/네트워크 레이어 | `docs/SERVER.md` |
| 변경 이력 | `docs/CHANGELOG.md` |
