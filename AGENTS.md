# PuzleBattleGame - Codex 작업 가이드라인

Unity 6000.0.38f1 (URP) 기반 퍼즐 배틀 게임 프레임워크.
작업을 시작할 때는 먼저 `docs/MAP.md`에서 대상 영역의 참고 문서를 확인한다.

---

## Codex 작업 원칙

- 작업 전 `docs/MAP.md`와 변경 대상 영역 문서를 읽고, 기존 구조와 패턴을 우선 따른다.
- 사용자 변경사항을 되돌리지 않는다. 작업 중 발견한 unrelated 변경은 그대로 둔다.
- `.meta`, 자동 생성 파일(`*Generated.cs`), 외부 DLL(`Plugins/*.dll`)은 요청 없이는 수정하지 않는다.
- 구현 후 가능한 범위에서 Unity 컴파일/테스트 또는 관련 정적 확인을 수행하고, 실행하지 못한 검증은 보고한다.
- 커밋/스테이징은 사용자가 요청한 경우에만 수행한다.
- 파일 검색은 `rg`/`rg --files`를 우선 사용한다.

---

## 코딩 규칙

### 파일 및 인코딩
- 새 C# 소스 파일은 **UTF-8 (BOM 포함)** 형식.
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
- 스타일 교정 시 기존 실행 로직/메서드 호출/조건문 결과를 변경하지 않는다.

### 에셋 및 리소스
- 모든 게임 리소스는 **Addressables** 시스템을 통해 관리.
- 에셋 로드 시 반드시 `AssetManager.cs` 경유. 직접 `Addressables.LoadAssetAsync` 호출 금지.
- 인게임 시각 요소는 **`Ingame` Sorting Layer**에서 렌더링.

### 디버깅
- 씬 뷰/GUI 디버그 정보는 **실제 뷰 객체(View Object) 상태**를 우선 반영.
- 씬 이동/데이터 로드 실패 시 명확한 에러 로그를 남긴다.

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
- 랜덤은 `PuzzleRandom`(시드 주입), 시간은 논리 프레임. 상세: `docs/ARCHITECTURE.md` §5.

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
| **문서 네비게이션** | **`docs/MAP.md`** |
| **커밋 전 코드 리뷰** | **`docs/CONVENTIONS.md`** |
| 아키텍처/게임 흐름/폴더 구조 | `docs/ARCHITECTURE.md` |
| 인게임 퍼즐 (보드, 블럭, 매칭, 뷰, 애니메이션, 리플레이) | `docs/INGAME.md` |
| 데이터/설정 (JSON, GameSpec, 추가 방법, ReplayData) | `docs/DATA.md` |
| UI/팝업/탭 (도메인 시스템, UIButton) | `docs/UI.md` |
| 씬/매니저/인프라 (씬 전환, AssetManager, Pool) | `docs/SCENE.md` |
| 서버 통신/API/공유 DTO/네트워크 레이어 | `docs/SERVER.md` |
| 변경 이력 | `docs/CHANGELOG.md` |

