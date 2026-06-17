namespace Puzzle.Core
{
    /// <summary>
    /// 호스트가 파괴될 때 중심으로부터 원형(유클리드 거리) 반경 내 블럭을 함께 파괴하는 폭탄 기믹입니다.
    /// 기존 BombBlock(서브클래스)을 컴포지션(부착) 방식으로 이전한 레퍼런스 구현입니다.
    /// </summary>
    public class BombGimmick : GimmickBase
    {
        /// <summary> 폭발 반경 (셀 단위, 원형 범위 기준) </summary>
        private readonly int _radius;

        /// <summary>
        /// 폭탄 기믹을 생성합니다.
        /// </summary>
        /// <param name="radius">폭발 반경 (기본 1)</param>
        public BombGimmick(int radius = 1)
        {
            _radius = radius < 1 ? 1 : radius;
        }

        /// <summary>
        /// 파괴 시 주변 반경의 블럭을 연쇄 파괴합니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">폭탄이 파괴된 좌표</param>
        public override void OnDestroyed(IPuzzleBoard board, GridPos myPos)
        {
            if (board == null)
            {
                return;
            }

            // 자기 자신을 중심으로 원형 반경 내 블럭을 파괴 (유클리드 거리 기준)
            int sqrRadius = _radius * _radius;
            for (int dx = -_radius; dx <= _radius; dx++)
            {
                for (int dy = -_radius; dy <= _radius; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        // 중심(자기 자신)은 이미 파괴되었으므로 건너뜀
                        continue;
                    }

                    // 원형 범위 밖(거리 > 반경)은 제외하여 사각형이 아닌 원형 폭발로 만든다
                    if (dx * dx + dy * dy > sqrRadius)
                    {
                        continue;
                    }

                    GridPos target = new GridPos(myPos.X + dx, myPos.Y + dy);
                    GimmickUtil.DestroyBlock(board, target);
                }
            }
        }
    }
}
