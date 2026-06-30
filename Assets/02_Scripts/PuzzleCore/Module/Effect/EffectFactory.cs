using System;

namespace Puzzle.Core
{
    /// <summary>
    /// 효과 데이터(EffectData)를 받아 해당 효과 동사(IBlockEffect) 인스턴스를 생성하는 팩토리입니다.
    /// action 문자열로 동사를 고르고, param 문자열을 동사별로 해석해 파라미터를 주입합니다.
    /// 새 동사를 추가하려면 동사 클래스를 만들고 이 곳의 생성 분기를 추가합니다.
    /// </summary>
    public class EffectFactory
    {
        /// <summary>
        /// 효과 데이터로부터 효과 동사 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="data">효과 데이터 (trigger/action/param)</param>
        /// <returns>생성된 효과 동사. action이 비었거나 알 수 없으면 null</returns>
        public IBlockEffect Create(EffectData data)
        {
            if (data == null || string.IsNullOrEmpty(data.action))
            {
                return null;
            }

            switch (data.action)
            {
                case "DestroyRadius":
                    // trigger 미지정 시 기본 OnDestroyed, param = 반경(정수, 해석 실패 시 1)
                    return new DestroyRadiusEffect(ParseTrigger(data.trigger, EffectTrigger.OnDestroyed), ParseInt(data.param, 1));

                case "DestroyLine":
                    // trigger 미지정 시 기본 OnDestroyed, param = 라인 방향(Horizontal/Vertical/Cross, 해석 실패 시 Cross)
                    return new DestroyLineEffect(ParseTrigger(data.trigger, EffectTrigger.OnDestroyed), ParseLineDirection(data.param));

                case "DestroySameColor":
                    // trigger 미지정 시 기본 OnSwapped
                    return new DestroySameColorEffect(ParseTrigger(data.trigger, EffectTrigger.OnSwapped));

                default:
                    return null;
            }
        }

        /// <summary>
        /// 문자열을 발화 시점(EffectTrigger)으로 해석합니다. 미지정/해석 실패 시 동사별 기본값을 사용합니다.
        /// </summary>
        /// <param name="value">해석할 문자열 (예: "OnDestroyed")</param>
        /// <param name="fallback">미지정/해석 실패 시 기본값</param>
        /// <returns>해석된 발화 시점 또는 기본값</returns>
        private static EffectTrigger ParseTrigger(string value, EffectTrigger fallback)
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out EffectTrigger trigger) && trigger != EffectTrigger.None)
            {
                return trigger;
            }
            return fallback;
        }

        /// <summary>
        /// 문자열을 정수로 해석합니다. 실패 시 기본값을 반환합니다.
        /// </summary>
        /// <param name="value">해석할 문자열</param>
        /// <param name="fallback">해석 실패 시 기본값</param>
        /// <returns>해석된 정수 또는 기본값</returns>
        private static int ParseInt(string value, int fallback)
        {
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return fallback;
        }

        /// <summary>
        /// 문자열을 라인 방향으로 해석합니다. 실패 시 십자(Cross)를 반환합니다.
        /// </summary>
        /// <param name="value">해석할 문자열</param>
        /// <returns>해석된 라인 방향 또는 Cross</returns>
        private static LineDirection ParseLineDirection(string value)
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out LineDirection dir))
            {
                return dir;
            }
            return LineDirection.Cross;
        }
    }
}
