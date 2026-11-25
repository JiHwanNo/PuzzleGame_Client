using System;

namespace Puzzle.Core
{
    /// <summary>
    /// BlockData로부터 블럭을 생성하고, gimmickIds에 명시된 기믹을 부착하는 팩토리 클래스입니다.
    /// inputType(조작 방법)과 gimmickIds(행동)는 서로 독립적인 데이터입니다.
    /// JSON에는 가독성을 위해 기믹을 문자열로 두고, 내부에서는 GimmickType enum으로 변환해 사용합니다.
    /// 보드 생성 시 인스턴스화되어 캐싱되어 사용됩니다.
    /// </summary>
    public class PuzzleBlockFactory
    {
        /// <summary> 기믹 타입으로부터 기믹 인스턴스를 생성하는 팩토리 </summary>
        private readonly GimmickFactory _gimmickFactory = new GimmickFactory();

        /// <summary>
        /// 데이터로부터 블럭을 생성하고, gimmickIds(문자열)를 GimmickType으로 변환해 기믹을 부착합니다.
        /// </summary>
        /// <param name="data">블럭 설정 데이터</param>
        /// <returns>생성된 블럭 객체</returns>
        public Block Create(BlockData data)
        {
            if (data == null)
            {
                return null;
            }

            // 모든 블럭은 단일 클래스(Block)로 생성한다.
            Block block = new Block(data);

            // 데이터에 명시된 기믹 문자열 id들을 enum으로 변환해 부착한다.
            if (data.gimmickIds != null)
            {
                for (int i = 0; i < data.gimmickIds.Count; i++)
                {
                    // 데이터는 문자열(가독용), 내부 처리는 enum(int)으로 변환
                    if (Enum.TryParse(data.gimmickIds[i], true, out GimmickType type))
                    {
                        IGimmick gimmick = _gimmickFactory.Create(type);
                        if (gimmick != null)
                        {
                            block.AddGimmick(gimmick);
                        }
                    }
                }
            }

            return block;
        }
    }
}
