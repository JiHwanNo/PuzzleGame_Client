# 블럭 기믹 확장 지침

## 생성과 입력

`PuzzleBlockFactory.Create`는 `BlockData`로 `Block`을 만들고 `gimmickIds`의 문자열을 `GimmickType`으로 변환한다. `GimmickFactory.Create`가 인스턴스를 반환하면 `Block.AddGimmick`으로 부착한다. 보드는 `inputType` 플래그로 조작 가능 여부를 판정한다.

## 파괴 연쇄

`Block.FireDestroyed`는 부착된 기믹의 `OnDestroyed`를 호출한다. `BombGimmick`은 반경 안의 대상 칸을 `GimmickUtil.DestroyBlock`에 전달한다. 이 메서드는 대상 블럭을 칸에서 제거하고 파괴 뷰를 등록한 뒤 제거한 블럭의 `FireDestroyed`를 호출한다. 연출 프레임과 순서는 보드의 `AddView`가 부여한다.

## 새 동작 추가 절차

1. 기존 폭탄을 쓰는 블럭은 Rule JSON의 `blocks[]`에 `"gimmickIds": ["Bomb"]`를 지정한다. 일반 블럭은 이 목록을 생략하거나 비워 둔다.
2. 새 행동은 `GimmickBase`를 상속해 구현하고, `GimmickType`과 `GimmickFactory.Create`에 해당 타입과 생성 분기를 등록한다.
3. 등록한 타입 이름을 `gimmickIds`에 넣고, 허용할 조작은 `inputType`으로 지정한다.
