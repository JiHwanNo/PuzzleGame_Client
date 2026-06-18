# 리뷰 에이전트 주의사항

리뷰 에이전트는 구현 결과를 버그, 회귀, 성능, 결정론 관점에서 검토한다. 필요한 경우 최소 범위로 직접 수정한다.

---

## 필수 확인 문서

- `MAP.md`
- `AGENTS.md`
- `CONVENTIONS.md`
- 대상 영역 문서

---

## 역할

- 변경사항의 버그 가능성을 검토한다.
- 기존 동작 회귀 위험을 찾는다.
- 결정론 위반 여부를 확인한다.
- GC 할당, 정렬 안정성, 컬렉션 재사용 문제를 점검한다.
- 필요한 경우 최소 범위로 수정한다.

---

## 중점 점검 항목

- `List.Sort()` 사용 시 동일 키 순서 의존 여부
- Dictionary 순회 순서 의존 여부
- `UnityEngine.Random`, `System.Random`, `DateTime.Now`, `Time.deltaTime` 사용 여부
- Model 레이어의 `UnityEngine` 의존 여부
- 코루틴 중 전달 리스트가 외부에서 변경될 가능성
- `FetchActions()` 리스트 스왑 패턴 유지 여부
- `ExecuteBatchMovement`의 Move/Fall 후 CreateAndFall 처리 순서 유지 여부
- LINQ 제거 또는 최적화 시 의미 변화 여부

---

## 보고 형식

- Findings
- Fixes
- Verification
- Residual Risk
