using System;
using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 셀 위에 놓여 유저가 조작하거나 매치되는 퍼즐 조각 클래스입니다.
    /// 조작 가능 여부(스왑/링크/터치)는 블럭 데이터(inputType)로 판정하고,
    /// 특수 행동은 부착된 기믹(IGimmick)이 담당하므로 블럭은 이 단일 클래스로 통일됩니다.
    /// </summary>
    public class Block : IGimmickHost
    {
        /// <summary> 블럭의 속성 정보를 담고 있는 데이터 객체 </summary>
        protected BlockData _blockData;

        /// <summary> 데이터의 문자열 조작 방식을 파싱한 플래그(게임 로직용 int/enum) </summary>
        private readonly InputType _inputType;

        /// <summary> 이 블럭에 부착된 기믹 목록 (행동은 기믹이 담당) </summary>
        public List<IGimmick> Gimmicks { get; } = new List<IGimmick>();

        /// <summary> 블럭의 현재 논리적 상태 </summary>
        public BlockState State { get; protected set; } = BlockState.Idle;

        /// <summary>
        /// 지정된 데이터를 사용하여 새로운 블럭 인스턴스를 생성합니다.
        /// 데이터의 조작 방식(문자열 목록)을 InputType 플래그로 변환해 캐싱합니다.
        /// </summary>
        /// <param name="data">블럭 설정 데이터</param>
        public Block(BlockData data)
        {
            _blockData = data;
            _inputType = ParseInputType(data);
        }

        /// <summary>
        /// 데이터의 조작 방식 문자열 목록(예: ["Swap","Touch"])을 InputType 플래그로 변환합니다.
        /// 데이터는 가독성을 위해 문자열, 게임 로직은 enum(int)으로 다룹니다.
        /// </summary>
        /// <param name="data">블럭 설정 데이터</param>
        /// <returns>합성된 InputType 플래그 (없으면 None)</returns>
        private static InputType ParseInputType(BlockData data)
        {
            InputType result = InputType.None;
            if (data != null && data.inputType != null)
            {
                for (int i = 0; i < data.inputType.Count; i++)
                {
                    if (Enum.TryParse(data.inputType[i], true, out InputType flag))
                    {
                        result |= flag;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 이 블럭의 조작 방식(스왑/링크/터치 가능 여부) 플래그를 반환합니다.
        /// </summary>
        /// <returns>InputType 플래그</returns>
        public InputType GetInputType()
        {
            return _inputType;
        }

        /// <summary>
        /// 블럭의 상태를 변경합니다.
        /// </summary>
        /// <param name="newState">새로운 상태</param>
        public virtual void SetState(BlockState newState)
        {
            State = newState;
        }

        /// <summary>
        /// 블럭의 고유 아이디를 반환합니다.
        /// </summary>
        /// <returns>블럭 아이디</returns>
        public string GetBlockId()
        {
            return _blockData?.blockId;
        }

        /// <summary>
        /// 매 프레임마다 블럭의 상태를 업데이트합니다.
        /// </summary>
        /// <param name="board">현재 보드 객체</param>
        /// <param name="myPos">블럭의 현재 위치</param>
        internal virtual void Update(IPuzzleBoard board, GridPos myPos)
        {
        }

        /// <summary>
        /// 이 블럭에 기믹을 부착합니다.
        /// </summary>
        /// <param name="gimmick">부착할 기믹</param>
        public void AddGimmick(IGimmick gimmick)
        {
            if (gimmick == null)
            {
                return;
            }

            Gimmicks.Add(gimmick);
            gimmick.OnAttach(this);
        }

        /// <summary>
        /// 이 블럭이 파괴될 때, 부착된 모든 기믹의 파괴 훅을 발화합니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">파괴된 위치</param>
        internal void FireDestroyed(IPuzzleBoard board, GridPos myPos)
        {
            for (int i = 0; i < Gimmicks.Count; i++)
            {
                Gimmicks[i].OnDestroyed(board, myPos);
            }
        }
    }
}
