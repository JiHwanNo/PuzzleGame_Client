using System;

namespace Battle.Pivot
{
    /// <summary> 피벗 충전량, 초당 소비량 및 콤보 틱 간격을 정의하는 게임 데이터입니다. </summary>
    [Serializable]
    public sealed class PivotSettings
    {
        /// <summary> 발동에 필요한 최대 게이지입니다. </summary>
        public int Capacity = 1000;
        /// <summary> 블럭 하나당 충전량입니다. 1단위는 0.01점이므로 기본값은 블럭 10개당 1점입니다. </summary>
        public int ChargePerBlockUnits = 10;
        /// <summary> 충전 보너스가 한 단계 증가하는 콤보 수입니다. </summary>
        public int ChargeComboStep = 10;
        /// <summary> 충전 보너스 한 단계당 블럭에 추가하는 0.01점 단위 충전량입니다. </summary>
        public int ChargeBonusPerStepUnits = 1;
        /// <summary> 활성 상태에서 초당 소비하는 기본 게이지 점수입니다. </summary>
        public int DrainPerSecond = 10;
        /// <summary> 빠른 콤보 한 단계마다 줄이는 소비 속도의 백분율입니다. </summary>
        public int DrainReductionPercentPerCombo = 25;
        /// <summary> 콤보를 이어가도 유지하는 기본 소비 속도의 최소 백분율입니다. </summary>
        public int MinimumDrainPercent = 25;
        /// <summary> 다음 유저 매칭 성공을 빠른 콤보로 인정하는 최대 틱 간격입니다. </summary>
        public int ComboWindowTicks = 50;

        /// <summary> 링크 피벗 중 육각형 이웃 한 겹을 함께 파괴할지 여부입니다. 기존 기록은 기본 비활성입니다. </summary>
        public bool LinkNeighborClear;

        /// <summary> 예외나 복사본 생성 없이 피벗 설정의 유효 범위를 확인합니다. </summary>
        /// <returns>정수 게이지와 소비 비율이 유효하면 true</returns>
        public bool IsValid() => Capacity > 0 && Capacity <= int.MaxValue / 100 && ChargePerBlockUnits > 0 &&
            ChargeComboStep > 0 && ChargeBonusPerStepUnits > 0 &&
            DrainPerSecond > 0 && DrainPerSecond <= int.MaxValue / 100 &&
            DrainReductionPercentPerCombo > 0 && DrainReductionPercentPerCombo <= 100 &&
            MinimumDrainPercent > 0 && MinimumDrainPercent <= 100 && ComboWindowTicks > 0;

        /// <summary> 대전 상대와 충전·소비·추가 파괴 조건이 모두 같은지 확인합니다. </summary>
        /// <param name="other">비교할 상대 기록의 피벗 설정</param>
        /// <returns>모든 피벗 설정값이 같으면 true</returns>
        public bool HasSameRules(PivotSettings other)
        {
            if (other == null)
            {
                return false;
            }
            return Capacity == other.Capacity && ChargePerBlockUnits == other.ChargePerBlockUnits &&
                ChargeComboStep == other.ChargeComboStep && ChargeBonusPerStepUnits == other.ChargeBonusPerStepUnits &&
                DrainPerSecond == other.DrainPerSecond && DrainReductionPercentPerCombo == other.DrainReductionPercentPerCombo &&
                MinimumDrainPercent == other.MinimumDrainPercent && ComboWindowTicks == other.ComboWindowTicks &&
                LinkNeighborClear == other.LinkNeighborClear;
        }

        /// <summary> 실행 중 외부 설정 변경의 영향을 받지 않도록 검증한 복사본을 만듭니다. </summary>
        /// <returns>값이 검증된 독립 설정 복사본</returns>
        public PivotSettings CopyValidated()
        {
            if (!IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(PivotSettings), "피벗 설정값은 양수여야 하며 게이지 범위와 1~100% 소비 비율을 확인해야 합니다.");
            }

            return new PivotSettings
            {
                Capacity = Capacity,
                ChargePerBlockUnits = ChargePerBlockUnits,
                ChargeComboStep = ChargeComboStep,
                ChargeBonusPerStepUnits = ChargeBonusPerStepUnits,
                DrainPerSecond = DrainPerSecond,
                DrainReductionPercentPerCombo = DrainReductionPercentPerCombo,
                MinimumDrainPercent = MinimumDrainPercent,
                ComboWindowTicks = ComboWindowTicks,
                LinkNeighborClear = LinkNeighborClear
            };
        }
    }
}
