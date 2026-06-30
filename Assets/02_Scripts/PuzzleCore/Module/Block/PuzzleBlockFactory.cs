namespace Puzzle.Core
{
    /// <summary>
    /// BlockData로부터 블럭을 생성하고, effects에 명시된 효과 동사를 부착하는 팩토리 클래스입니다.
    /// inputType(조작 방법)·damagedBy(피격 소스)·effects(행동)는 서로 독립적인 데이터입니다.
    /// JSON에는 가독성을 위해 문자열로 두고, 내부에서는 enum/동사 인스턴스로 변환해 사용합니다.
    /// 보드 생성 시 인스턴스화되어 캐싱되어 사용됩니다.
    /// </summary>
    public class PuzzleBlockFactory
    {
        /// <summary> 효과 데이터로부터 효과 동사 인스턴스를 생성하는 팩토리 </summary>
        private readonly EffectFactory _effectFactory = new EffectFactory();

        /// <summary>
        /// 데이터로부터 블럭을 생성하고, effects(데이터)를 효과 동사로 변환해 부착합니다.
        /// </summary>
        /// <param name="data">블럭 설정 데이터</param>
        /// <returns>생성된 블럭 객체</returns>
        public Block Create(BlockData data)
        {
            if (data == null)
            {
                return null;
            }

            // 모든 블럭은 단일 클래스(Block)로 생성한다. (HP/피격소스는 Block 생성자가 데이터에서 초기화)
            Block block = new Block(data);

            // 데이터에 명시된 효과 동사들을 생성해 부착한다.
            if (data.effects != null)
            {
                for (int i = 0; i < data.effects.Count; i++)
                {
                    IBlockEffect effect = _effectFactory.Create(data.effects[i]);
                    if (effect != null)
                    {
                        block.AddEffect(effect);
                    }
                }
            }

            return block;
        }
    }
}
