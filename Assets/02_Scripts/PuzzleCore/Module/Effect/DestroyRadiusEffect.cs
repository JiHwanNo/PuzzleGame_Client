namespace Puzzle.Core
{
    /// <summary>
    /// 발화 시 중심으로부터 원형(유클리드 거리) 반경 내 블럭을 함께 파괴하는 효과 동사입니다. (구 BombGimmick)
    /// 발화 시점(Trigger)은 데이터로 주입하며, 보통 OnDestroyed(파괴 시 폭발)로 사용합니다.
    /// </summary>
    public class DestroyRadiusEffect : IBlockEffect
    {
        /// <summary> 발화 시점 (데이터 trigger 주입) </summary>
        private readonly EffectTrigger _trigger;

        /// <summary> 폭발 반경 (셀 단위, 원형 범위 기준) </summary>
        private readonly int _radius;

        /// <summary> 이 효과의 발화 시점 </summary>
        public EffectTrigger Trigger
        {
            get { return _trigger; }
        }

        /// <summary>
        /// 원형 폭발 효과를 생성합니다.
        /// </summary>
        /// <param name="trigger">발화 시점</param>
        /// <param name="radius">폭발 반경 (기본 1, 최소 1)</param>
        public DestroyRadiusEffect(EffectTrigger trigger, int radius = 1)
        {
            _trigger = trigger;
            _radius = radius < 1 ? 1 : radius;
        }

        /// <summary>
        /// 자신을 중심으로 원형 반경 내 블럭에 폭발 데미지를 가합니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">폭발 중심 좌표</param>
        /// <param name="context">발화 컨텍스트 (스텝 피격 집합 사용)</param>
        /// <returns>항상 false (소비 개념 없음)</returns>
        public bool Apply(IPuzzleBoard board, GridPos myPos, EffectContext context)
        {
            if (board == null)
            {
                return false;
            }

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
                    BlockDamage.Damage(board, target, DamageSource.Splash, context.DamagedThisStep);
                }
            }

            return false;
        }
    }
}
