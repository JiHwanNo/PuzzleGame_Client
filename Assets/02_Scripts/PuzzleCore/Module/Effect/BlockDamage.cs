using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 효과 동사/보드가 보드 상의 블럭에 데미지를 가할 때 공통으로 사용하는 헬퍼입니다.
    /// "이번 스텝 중복 차단 → 데미지 소스 게이팅(블럭의 damagedBy 확인) → HP 감소 → 0이면 파괴(목표 갱신 + 파괴 큐 + 연쇄 효과)"를 한 곳에 모읍니다.
    /// 연출 프레임/순서는 보드의 AddView가 스탬프하므로 여기서는 다루지 않습니다.
    /// </summary>
    internal static class BlockDamage
    {
        /// <summary>
        /// 지정 좌표의 블럭에 데미지를 1 가합니다. 같은 스텝에 이미 피격된 칸이거나 해당 소스에 면역이면 무시되고,
        /// HP가 0이 되면 파괴하며 부착된 효과 동사를 연쇄 발화합니다.
        /// 빈 칸/막힘·잠금 셀은 아무것도 하지 않으므로 무한 연쇄가 방지됩니다.
        /// </summary>
        /// <param name="board">대상 보드</param>
        /// <param name="pos">데미지를 가할 좌표</param>
        /// <param name="source">데미지 소스 (Match/NeighborMatch/Splash)</param>
        /// <param name="damagedThisStep">이번 스텝에 이미 피격된 좌표 집합. null이면 중복 차단 없음. "한 스텝 = 칸당 최대 1대" 보장용</param>
        /// <returns>이 데미지로 블럭이 실제로 파괴되었으면 true (빈 칸/면역/HP 잔존/중복 피격이면 false)</returns>
        public static bool Damage(IPuzzleBoard board, GridPos pos, DamageSource source, HashSet<GridPos> damagedThisStep = null)
        {
            var cell = board?.GetCell(pos);
            if (cell?.Block == null)
            {
                // 빈 칸 → 연쇄 종료
                return false;
            }

            // 막힘/잠금 등 플레이 불가 셀은 데미지 대상에서 제외
            if (!cell.IsPlayable)
            {
                return false;
            }

            // 같은 스텝에 이미 피격된 칸이면 중복 데미지를 가하지 않는다. (폭탄 Splash + 인접 매치 동시 적중 방지)
            if (damagedThisStep != null && !damagedThisStep.Add(pos))
            {
                return false;
            }

            Block target = cell.Block;

            // 데미지 소스 게이팅 + HP 감소. 면역이거나 HP가 남았으면 파괴하지 않는다.
            if (!target.TakeDamage(source))
            {
                return false;
            }

            board.Objective?.OnBlockDestroyed(target.GetBlockId());

            // 먼저 비워 재진입(중복 파괴/무한 연쇄)을 차단
            cell.Block = null;

            // 연출 프레임/순서는 보드의 AddView가 스탬프한다.
            board.AddView(new BoardViewAction
            {
                type = ViewType.Destroy,
                position = pos
            });

            // 파괴된 블럭의 OnDestroyed 효과 연쇄 발화 (폭탄이 폭탄을 터뜨림). 스텝 집합을 그대로 전파.
            target.FireDestroyed(board, pos, damagedThisStep);
            return true;
        }

        /// <summary>
        /// 지정 좌표의 블럭을 "활동 소비"로 무조건 제거합니다. (예: 무지개가 발동하며 자기 자신을 소비)
        /// 외부 데미지가 아니므로 damagedBy/HP 게이팅을 적용하지 않습니다. 빈 칸/막힘·잠금 셀은 무시합니다.
        /// </summary>
        /// <param name="board">대상 보드</param>
        /// <param name="pos">제거할 좌표</param>
        /// <param name="damagedThisStep">이번 스텝 피격 집합(중복 차단용). null 허용</param>
        /// <returns>실제로 제거되었으면 true</returns>
        public static bool Destroy(IPuzzleBoard board, GridPos pos, HashSet<GridPos> damagedThisStep = null)
        {
            var cell = board?.GetCell(pos);
            if (cell?.Block == null)
            {
                return false;
            }

            if (!cell.IsPlayable)
            {
                return false;
            }

            // 이미 이번 스텝에 처리된 칸이면 중복 제거하지 않는다.
            if (damagedThisStep != null && !damagedThisStep.Add(pos))
            {
                return false;
            }

            Block target = cell.Block;
            board.Objective?.OnBlockDestroyed(target.GetBlockId());
            cell.Block = null;
            board.AddView(new BoardViewAction
            {
                type = ViewType.Destroy,
                position = pos
            });
            target.FireDestroyed(board, pos, damagedThisStep);
            return true;
        }
    }
}
