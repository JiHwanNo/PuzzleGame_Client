namespace Puzzle.Core
{
    /// <summary>
    /// 기믹 타입(enum)을 받아 해당 기믹 인스턴스를 생성하는 팩토리입니다.
    /// 타입 하나당 동작이 고정되며, 파라미터(반경 등)는 여기서 주입합니다.
    /// 새 기믹을 추가하려면 GimmickType 값과 이 곳의 생성 분기를 함께 추가합니다.
    /// </summary>
    public class GimmickFactory
    {
        /// <summary>
        /// 기믹 타입에 해당하는 기믹 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="type">기믹 타입</param>
        /// <returns>생성된 기믹. None이거나 알 수 없는 타입이면 null</returns>
        public IGimmick Create(GimmickType type)
        {
            switch (type)
            {
                case GimmickType.Bomb:
                    // 원형 폭탄: 파괴 시 주변 반경 2칸을 함께 파괴
                    return new BombGimmick(2);
                default:
                    return null;
            }
        }
    }
}
