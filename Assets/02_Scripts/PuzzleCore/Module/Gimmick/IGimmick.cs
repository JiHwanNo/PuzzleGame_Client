using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 블럭이나 판넬 등 호스트에 부착되어, 보드 이벤트(파괴/터치/스왑) 시점에
    /// 능동적인 행동을 수행하는 "기믹"의 공통 계약입니다.
    /// 상태와 로직은 이 인터페이스를 구현한 구체 클래스가 보유합니다.
    /// </summary>
    public interface IGimmick
    {
        /// <summary>
        /// 호스트에 부착될 때 호출됩니다. (초기화/소유자 참조 보관용)
        /// </summary>
        /// <param name="owner">이 기믹을 부착한 블럭</param>
        void OnAttach(Block owner);

        /// <summary>
        /// 호스트가 파괴될 때 호출됩니다. (예: 폭탄의 주변 폭발)
        /// 연출 프레임/순서는 보드의 연출 큐(AddView)가 담당하므로 기믹은 다루지 않습니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">파괴된 좌표</param>
        void OnDestroyed(IPuzzleBoard board, GridPos myPos);

        /// <summary>
        /// 호스트가 터치(클릭)될 때 호출됩니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">터치된 좌표</param>
        /// <returns>기믹이 입력을 처리했으면 true</returns>
        bool OnTouched(IPuzzleBoard board, GridPos myPos);

        /// <summary>
        /// 호스트가 다른 블럭과 스왑될 때 호출됩니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">자신의 좌표</param>
        /// <param name="targetPos">스왑 대상 좌표</param>
        /// <returns>기믹이 입력을 처리했으면 true</returns>
        bool OnSwapped(IPuzzleBoard board, GridPos myPos, GridPos targetPos);
    }

    /// <summary>
    /// 기믹을 부착할 수 있는 호스트(블럭/판넬 등)의 공통 계약입니다.
    /// </summary>
    public interface IGimmickHost
    {
        /// <summary> 이 호스트에 부착된 기믹 목록 </summary>
        List<IGimmick> Gimmicks { get; }

        /// <summary>
        /// 기믹을 부착합니다.
        /// </summary>
        /// <param name="gimmick">부착할 기믹</param>
        void AddGimmick(IGimmick gimmick);
    }

    /// <summary>
    /// 모든 훅에 빈 기본 구현을 제공하는 기믹 베이스 클래스입니다.
    /// 구체 기믹은 필요한 훅만 override 하면 됩니다.
    /// </summary>
    public abstract class GimmickBase : IGimmick
    {
        /// <inheritdoc/>
        public virtual void OnAttach(Block owner)
        {
        }

        /// <inheritdoc/>
        public virtual void OnDestroyed(IPuzzleBoard board, GridPos myPos)
        {
        }

        /// <inheritdoc/>
        public virtual bool OnTouched(IPuzzleBoard board, GridPos myPos)
        {
            return false;
        }

        /// <inheritdoc/>
        public virtual bool OnSwapped(IPuzzleBoard board, GridPos myPos, GridPos targetPos)
        {
            return false;
        }
    }
}
