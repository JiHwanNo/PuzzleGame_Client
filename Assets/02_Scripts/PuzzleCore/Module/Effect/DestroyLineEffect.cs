namespace Puzzle.Core
{
    /// <summary>
    /// 발화 시 자신을 지나는 행/열(또는 십자) 라인 전체를 함께 파괴하는 효과 동사입니다. (구 LineBombGimmick)
    /// 발화 시점(Trigger)과 방향(가로/세로/십자)은 데이터로 주입합니다.
    /// </summary>
    public class DestroyLineEffect : IBlockEffect
    {
        /// <summary> 발화 시점 (데이터 trigger 주입) </summary>
        private readonly EffectTrigger _trigger;

        /// <summary> 라인 제거 방향 (가로/세로/십자) </summary>
        private readonly LineDirection _direction;

        /// <summary> 이 효과의 발화 시점 </summary>
        public EffectTrigger Trigger
        {
            get { return _trigger; }
        }

        /// <summary>
        /// 라인 폭발 효과를 생성합니다.
        /// </summary>
        /// <param name="trigger">발화 시점</param>
        /// <param name="direction">제거 방향 (기본 십자)</param>
        public DestroyLineEffect(EffectTrigger trigger, LineDirection direction = LineDirection.Cross)
        {
            _trigger = trigger;
            _direction = direction;
        }

        /// <summary>
        /// 자신을 지나는 행/열(또는 십자) 라인의 블럭에 폭발 데미지를 가합니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">라인 중심 좌표</param>
        /// <param name="context">발화 컨텍스트 (스텝 피격 집합 사용)</param>
        /// <returns>항상 false (소비 개념 없음)</returns>
        public bool Apply(IPuzzleBoard board, GridPos myPos, EffectContext context)
        {
            if (board == null)
            {
                return false;
            }

            // 가로(행) 라인 제거
            if (_direction == LineDirection.Horizontal || _direction == LineDirection.Cross)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (x == myPos.X)
                    {
                        // 중심(자기 자신)은 이미 파괴되었으므로 건너뜀
                        continue;
                    }

                    BlockDamage.Damage(board, new GridPos(x, myPos.Y), DamageSource.Splash, context.DamagedThisStep);
                }
            }

            // 세로(열) 라인 제거
            if (_direction == LineDirection.Vertical || _direction == LineDirection.Cross)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (y == myPos.Y)
                    {
                        // 중심(자기 자신)은 이미 파괴되었으므로 건너뜀
                        continue;
                    }

                    BlockDamage.Damage(board, new GridPos(myPos.X, y), DamageSource.Splash, context.DamagedThisStep);
                }
            }

            return false;
        }
    }
}
