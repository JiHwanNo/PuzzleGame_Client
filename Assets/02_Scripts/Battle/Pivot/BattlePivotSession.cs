using System;
using Puzzle.Core;

namespace Battle.Pivot
{
    /// <summary> 보드의 파괴 통계와 종료 상태를 독립 피벗 모듈에 전달하는 연결 계층입니다. </summary>
    public sealed class BattlePivotSession : IDisposable
    {
        /// <summary> 이 세션이 관찰할 보드입니다. </summary>
        private readonly IPuzzleBoard _board;
        /// <summary> 직전 틱까지 반영한 누적 파괴 수입니다. </summary>
        private int _lastDestroyedCount;
        /// <summary> 직전 틱까지 반영한 유저 매칭 성공 횟수입니다. </summary>
        private int _lastPlayerMatches;
        /// <summary> 이전 기록의 파괴 발생 기준 피벗 콤보를 복원할지 여부입니다. </summary>
        private readonly bool _useLegacyDestructionCombos;
        /// <summary> 매칭 대상 확장 이벤트를 구독한 링크 보드입니다. </summary>
        private readonly LinkPuzzleBoard _linkBoard;
        /// <summary> 피벗 활성 상태에서만 이웃을 추가하는 링크 전용 효과입니다. </summary>
        private readonly LinkPivotEffect _linkEffect;
        /// <summary> 이 보드만의 독립적인 피벗 상태입니다. </summary>
        public PivotMode Mode { get; }

        /// <summary> 보드와 게임 모드에 맞춰 피벗 세션을 만듭니다. </summary>
        /// <param name="board">초기화가 완료된 관찰 대상 보드</param>
        /// <param name="gameMode">피벗 허용 여부를 결정할 게임 모드</param>
        /// <param name="settings">피벗 충전과 유지 설정</param>
        /// <param name="useLegacyDestructionCombos">이전 기록의 파괴 기준 콤보를 사용할지 여부</param>
        public BattlePivotSession(IPuzzleBoard board, GameMode gameMode, PivotSettings settings,
            bool useLegacyDestructionCombos = false)
        {
            if (board == null || board.Objective == null)
            {
                throw new ArgumentException("피벗 세션에는 초기화된 보드가 필요합니다.", nameof(board));
            }
            _board = board;
            _lastDestroyedCount = board.Objective.TotalDestroyedBlocks;
            _lastPlayerMatches = board.Objective.TotalPlayerMatches;
            _useLegacyDestructionCombos = useLegacyDestructionCombos;
            Mode = new PivotMode(gameMode == GameMode.Battle, settings);
            if (Mode.IsEnabled && settings.LinkNeighborClear && board is LinkPuzzleBoard linkBoard)
            {
                _linkBoard = linkBoard;
                _linkEffect = new LinkPivotEffect(board, Mode);
                _linkBoard.MatchTargetsExpanding += _linkEffect.ExpandTargets;
            }
        }

        /// <summary> 보드가 한 틱 진행한 직후 파괴 증가분과 종료 여부를 전달합니다. </summary>
        public void AdvanceAfterBoardTick()
        {
            int destroyedCount = _board.Objective.TotalDestroyedBlocks;
            int destroyedThisTick = destroyedCount - _lastDestroyedCount;
            _lastDestroyedCount = destroyedCount;
            int playerMatches = _board.Objective.TotalPlayerMatches;
            int matchesThisTick = playerMatches - _lastPlayerMatches;
            _lastPlayerMatches = playerMatches;
            if (_board.State == BoardState.Finish)
            {
                Dispose();
                return;
            }
            if (_useLegacyDestructionCombos)
            {
                matchesThisTick = destroyedThisTick > 0 ? 1 : 0;
            }
            Mode.AdvanceTick(destroyedThisTick, matchesThisTick);
        }

        /// <summary> 종료 또는 재초기화 시 효과 구독과 피벗 상태를 정리합니다. </summary>
        public void Dispose()
        {
            if (_linkBoard != null && _linkEffect != null)
            {
                _linkBoard.MatchTargetsExpanding -= _linkEffect.ExpandTargets;
            }
            Mode.Stop();
        }
    }
}
