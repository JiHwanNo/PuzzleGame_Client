using System.Collections.Generic;
using Puzzle.Core;

namespace Battle.Pivot
{
    /// <summary> 피벗 중 원래 링크 경로의 육각형 이웃 한 겹을 파괴 대상으로 추가합니다. </summary>
    public sealed class LinkPivotEffect
    {
        /// <summary> 대상 셀의 존재와 상태를 확인할 보드입니다. </summary>
        private readonly IPuzzleBoard _board;
        /// <summary> 매칭 확정 시 활성 여부를 확인할 독립 피벗 상태입니다. </summary>
        private readonly PivotMode _mode;
        /// <summary> 중복 검사에만 사용하며 순회 순서에는 의존하지 않습니다. </summary>
        private readonly HashSet<GridPos> _seen = new HashSet<GridPos>();

        /// <summary> 지정한 보드와 피벗 상태에 연결되는 링크 효과를 만듭니다. </summary>
        /// <param name="board">파괴 대상 셀을 조회할 보드</param>
        /// <param name="mode">추가 파괴를 허용할 피벗 상태</param>
        public LinkPivotEffect(IPuzzleBoard board, PivotMode mode)
        {
            _board = board;
            _mode = mode;
        }

        /// <summary> 원래 링크 경로의 6방향 이웃을 색상과 무관하게 한 번씩 추가합니다. </summary>
        /// <param name="shape">현재 보드의 셀 배치 형태</param>
        /// <param name="path">매칭이 확정된 원래 연결 경로</param>
        /// <param name="targets">기존 경로가 들어 있는 파괴 대상 목록</param>
        public void ExpandTargets(BoardShape shape, IReadOnlyList<GridPos> path, List<GridPos> targets)
        {
            if (!_mode.IsEnabled || !_mode.IsActive || shape != BoardShape.Hexagon)
            {
                return;
            }

            _seen.Clear();
            for (int i = 0; i < targets.Count; i++)
            {
                _seen.Add(targets[i]);
            }
            // 경로 순서와 좌표 오프셋 순서를 고정하여 리플레이의 파괴·기믹 순서를 보장합니다.
            for (int i = 0; i < path.Count; i++)
            {
                GridPos origin = path[i];
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        GridPos candidate = new GridPos(origin.X + dx, origin.Y + dy);
                        if (candidate.X < 0 || candidate.X >= _board.Width ||
                            candidate.Y < 0 || candidate.Y >= _board.Height ||
                            !GridPos.IsAdjacentHexagon(origin, candidate) || !_seen.Add(candidate))
                        {
                            continue;
                        }
                        PuzzleCell cell = _board.GetCell(candidate);
                        if (cell != null && cell.Block != null &&
                            (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator))
                        {
                            targets.Add(candidate);
                        }
                    }
                }
            }
        }
    }
}
