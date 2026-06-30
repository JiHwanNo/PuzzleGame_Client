using System;
using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 셀 위에 놓여 유저가 조작하거나 매치되는 퍼즐 조각 클래스입니다.
    /// 블럭은 "속성 데이터(HP·조작방식·피격소스) + 효과 동사(IBlockEffect) 모음"으로 표현되며,
    /// 특수 행동은 상속이 아니라 데이터가 조합한 효과 동사가 담당하므로 이 단일 클래스로 통일됩니다.
    /// </summary>
    public class Block
    {
        /// <summary> 블럭의 속성 정보를 담고 있는 데이터 객체 </summary>
        protected BlockData _blockData;

        /// <summary> 데이터의 문자열 조작 방식을 파싱한 플래그(게임 로직용 int/enum) </summary>
        private readonly InputType _inputType;

        /// <summary> 이 블럭의 HP를 깎을 수 있는 데미지 소스 플래그 (데이터 damagedBy 파싱 결과) </summary>
        private readonly DamageSource _damagedBy;

        /// <summary> 이 블럭에 부착된 효과 동사 목록 (행동은 효과가 담당) </summary>
        public List<IBlockEffect> Effects { get; } = new List<IBlockEffect>();

        /// <summary> 현재 HP(내구도). 0이 되면 파괴된다. </summary>
        public int Hp { get; private set; }

        /// <summary> 블럭의 현재 논리적 상태 </summary>
        public BlockState State { get; protected set; } = BlockState.Idle;

        /// <summary>
        /// 지정된 데이터를 사용하여 새로운 블럭 인스턴스를 생성합니다.
        /// 조작 방식/피격 소스(문자열 목록)를 플래그로 변환해 캐싱하고, HP를 초기화합니다.
        /// </summary>
        /// <param name="data">블럭 설정 데이터</param>
        public Block(BlockData data)
        {
            _blockData = data;
            _inputType = ParseInputType(data);
            _damagedBy = ParseDamagedBy(data);
            // life 미지정(0 이하)은 1로 취급
            Hp = (data != null && data.life > 0) ? data.life : 1;
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
        /// 데이터의 피격 소스 문자열 목록(예: ["Match","Splash"])을 DamageSource 플래그로 변환합니다.
        /// 비어있으면 일반 블럭 기본값(Match|Splash)으로 취급합니다.
        /// </summary>
        /// <param name="data">블럭 설정 데이터</param>
        /// <returns>합성된 DamageSource 플래그</returns>
        private static DamageSource ParseDamagedBy(BlockData data)
        {
            if (data == null || data.damagedBy == null || data.damagedBy.Count == 0)
            {
                // 데이터 미지정 시 일반 블럭 기본값: 자기 매치 + 폭발
                return DamageSource.Match | DamageSource.Splash;
            }

            DamageSource result = DamageSource.None;
            for (int i = 0; i < data.damagedBy.Count; i++)
            {
                if (Enum.TryParse(data.damagedBy[i], true, out DamageSource flag))
                {
                    result |= flag;
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
        /// 이 블럭에 효과 동사를 부착합니다.
        /// </summary>
        /// <param name="effect">부착할 효과 동사</param>
        public void AddEffect(IBlockEffect effect)
        {
            if (effect == null)
            {
                return;
            }

            Effects.Add(effect);
        }

        /// <summary>
        /// 지정 소스의 데미지를 1 받습니다. 해당 소스에 면역(damagedBy 미포함)이면 무시합니다.
        /// </summary>
        /// <param name="source">데미지 소스</param>
        /// <returns>이 데미지로 HP가 0이 되어 파괴되어야 하면 true</returns>
        internal bool TakeDamage(DamageSource source)
        {
            // 해당 소스에 면역이면 HP를 깎지 않는다.
            if ((_damagedBy & source) == DamageSource.None)
            {
                return false;
            }

            Hp--;
            return Hp <= 0;
        }

        /// <summary>
        /// 이 블럭이 파괴될 때, 부착된 효과 중 OnDestroyed 트리거 동사를 발화합니다.
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">파괴된 위치</param>
        /// <param name="damagedThisStep">이번 스텝 피격 집합(중복 차단용). 효과 동사의 연쇄 데미지에 전파. null 허용</param>
        internal void FireDestroyed(IPuzzleBoard board, GridPos myPos, HashSet<GridPos> damagedThisStep = null)
        {
            EffectContext context = new EffectContext { TargetPos = myPos, DamagedThisStep = damagedThisStep };
            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].Trigger == EffectTrigger.OnDestroyed)
                {
                    Effects[i].Apply(board, myPos, context);
                }
            }
        }

        /// <summary>
        /// 이 블럭이 다른 블럭과 스왑될 때, 부착된 효과 중 OnSwapped 트리거 동사를 발화합니다.
        /// 효과 중 하나라도 스왑을 소비(처리)하면 true를 반환합니다. (예: 무지개 폭탄)
        /// </summary>
        /// <param name="board">현재 보드</param>
        /// <param name="myPos">자신의 좌표</param>
        /// <param name="targetPos">스왑 대상 좌표</param>
        /// <param name="damagedThisStep">이번 스텝 피격 집합(중복 차단용). 효과 동사의 연쇄 데미지에 전파. null 허용</param>
        /// <returns>효과가 스왑을 소비했으면 true</returns>
        internal bool FireSwapped(IPuzzleBoard board, GridPos myPos, GridPos targetPos, HashSet<GridPos> damagedThisStep = null)
        {
            EffectContext context = new EffectContext { TargetPos = targetPos, DamagedThisStep = damagedThisStep };
            bool consumed = false;
            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].Trigger == EffectTrigger.OnSwapped)
                {
                    if (Effects[i].Apply(board, myPos, context))
                    {
                        consumed = true;
                    }
                }
            }
            return consumed;
        }
    }
}
