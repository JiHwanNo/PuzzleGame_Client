using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 효과 동사 발화에 필요한 부가 정보를 담는 컨텍스트입니다. (스왑 대상 좌표, 이번 스텝 피격 집합 등)
    /// </summary>
    public struct EffectContext
    {
        /// <summary> 스왑 등으로 발화될 때의 상대(대상) 좌표 </summary>
        public GridPos TargetPos;

        /// <summary>
        /// 이번 파괴/매치 해소 스텝에 이미 데미지를 받은 좌표 집합.
        /// "한 스텝 = 칸당 최대 1대" 규칙을 위해 효과 동사가 `BlockDamage.Damage`에 그대로 전달한다.
        /// null이면 중복 차단을 적용하지 않는다.
        /// </summary>
        public HashSet<GridPos> DamagedThisStep;
    }

    /// <summary>
    /// 블럭에 부착되어 특정 시점(Trigger)에 "어떤 동작을 수행한다"를 정의하는 효과 동사의 계약입니다.
    /// 기믹은 더 이상 클래스(명사)가 아니라, 데이터가 조합하는 동사(verb)의 집합으로 표현됩니다.
    /// 동사는 상태를 갖지 않고(파라미터는 생성 시 주입) 보드를 인자로 받아 동작만 수행합니다.
    /// </summary>
    public interface IBlockEffect
    {
        /// <summary> 이 효과가 발화되는 시점 (데이터 trigger로 주입) </summary>
        EffectTrigger Trigger { get; }

        /// <summary>
        /// 효과를 실행합니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">효과를 보유한 블럭의 좌표</param>
        /// <param name="context">발화 시점의 부가 정보 (스왑 대상, 이번 스텝 피격 집합 등)</param>
        /// <returns>이 효과가 입력/이벤트를 소비(처리)했으면 true (예: 무지개 스왑 소비)</returns>
        bool Apply(IPuzzleBoard board, GridPos myPos, EffectContext context);
    }
}
