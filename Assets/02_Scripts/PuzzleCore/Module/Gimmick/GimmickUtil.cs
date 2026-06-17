namespace Puzzle.Core
{
    /// <summary>
    /// 기믹이 보드 상의 블럭을 파괴할 때 공통으로 사용하는 헬퍼입니다.
    /// 파괴 처리(목표 갱신 + 파괴 큐 등록 + 연쇄 기믹 발화)를 한 곳에 모읍니다.
    /// 연출 프레임/순서는 보드의 AddView가 스탬프하므로 여기서는 다루지 않습니다.
    /// </summary>
    internal static class GimmickUtil
    {
        /// <summary>
        /// 지정 좌표의 블럭을 파괴하고, 그 블럭에 부착된 기믹을 연쇄 발화합니다.
        /// 이미 빈 칸이면 아무것도 하지 않으므로 무한 연쇄가 방지됩니다.
        /// </summary>
        /// <param name="board">대상 보드</param>
        /// <param name="pos">파괴할 좌표</param>
        public static void DestroyBlock(IPuzzleBoard board, GridPos pos)
        {
            var cell = board?.GetCell(pos);
            if (cell?.Block == null)
            {
                // 빈 칸 → 연쇄 종료
                return;
            }

            // 막힘/잠금 셀은 폭발 대상에서 제외
            if (cell.CellType != CellType.Normal && cell.CellType != CellType.Generator)
            {
                return;
            }

            Block destroyed = cell.Block;
            board.Objective?.OnBlockDestroyed(destroyed.GetBlockId());

            // 먼저 비워 재진입(중복 파괴/무한 연쇄)을 차단
            cell.Block = null;

            // 연출 프레임/순서는 보드의 AddView가 스탬프한다.
            board.AddView(new BoardViewAction
            {
                type = ViewType.Destroy,
                position = pos
            });

            // 파괴된 블럭의 기믹 연쇄 발화 (폭탄이 폭탄을 터뜨림)
            destroyed.FireDestroyed(board, pos);
        }
    }
}
