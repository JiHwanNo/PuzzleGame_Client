namespace Puzzle.Core
{
    /// <summary>
    /// 발화 시, 대상과 같은 색(blockId)의 블럭을 보드 전체에서 모두 파괴하는 효과 동사입니다. (구 RainbowBombGimmick)
    /// 보통 OnSwapped로 사용하며, 매치 성립 여부와 무관하게 스왑을 "소비"하므로 스왑 처리에서 매치 검사보다 먼저 발동합니다.
    /// </summary>
    public class DestroySameColorEffect : IBlockEffect
    {
        /// <summary> 발화 시점 (데이터 trigger 주입) </summary>
        private readonly EffectTrigger _trigger;

        /// <summary> 이 효과의 발화 시점 </summary>
        public EffectTrigger Trigger
        {
            get { return _trigger; }
        }

        /// <summary>
        /// 동색 전체 파괴 효과를 생성합니다.
        /// </summary>
        /// <param name="trigger">발화 시점</param>
        public DestroySameColorEffect(EffectTrigger trigger)
        {
            _trigger = trigger;
        }

        /// <summary>
        /// 대상(스왑 상대 등)의 색(blockId)을 읽어, 보드 내 같은 색 블럭을 모두 파괴하고 자신을 소비합니다.
        /// 대상 칸이 비어 있으면 발동하지 않고 일반 스왑으로 넘깁니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">자신의 좌표</param>
        /// <param name="context">발화 컨텍스트 (TargetPos = 대상 좌표, 스텝 피격 집합)</param>
        /// <returns>발동(소비)했으면 true</returns>
        public bool Apply(IPuzzleBoard board, GridPos myPos, EffectContext context)
        {
            if (board == null)
            {
                return false;
            }

            var targetCell = board.GetCell(context.TargetPos);
            string targetColor = targetCell?.Block?.GetBlockId();
            if (string.IsNullOrEmpty(targetColor))
            {
                // 대상 색이 없으면 발동하지 않음 → 일반 스왑 흐름으로 넘김 (보드 미변경)
                return false;
            }

            // 대상 색이 유효하므로 발동을 확정한다. 이후로는 반드시 자신을 소비하고 true를 반환한다.
            var damagedThisStep = context.DamagedThisStep;

            // 보드를 (x, y) 순서로 스캔하여 같은 색 블럭에 폭발 데미지 (결정론적 순서 보장)
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    GridPos pos = new GridPos(x, y);
                    if (pos == myPos)
                    {
                        // 자신은 마지막에 별도로 소비
                        continue;
                    }

                    var cell = board.GetCell(pos);
                    if (cell?.Block != null && cell.Block.GetBlockId() == targetColor)
                    {
                        BlockDamage.Damage(board, pos, DamageSource.Splash, damagedThisStep);
                    }
                }
            }

            // 자신은 "활동 소비"로 무조건 제거 (damagedBy 면역 데이터여도 고착되지 않도록 게이팅 분리)
            BlockDamage.Destroy(board, myPos, damagedThisStep);
            return true;
        }
    }
}
