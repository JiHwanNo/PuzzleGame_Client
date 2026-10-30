using System;

namespace Battle.Pivot
{
    /// <summary> 퍼즐 보드와 Unity에 의존하지 않는 피벗 충전 및 유지 상태 머신입니다. </summary>
    public sealed class PivotMode
    {
        /// <summary> 실행 시작 시 고정한 설정입니다. </summary>
        private readonly PivotSettings _settings;
        /// <summary> 직전 유저 매칭 성공 이후 흐른 틱 수이며 콤보 판정 범위까지만 증가합니다. </summary>
        private long _ticksSincePlayerMatch;
        /// <summary> 소비량 계산에서 정수 단위 미만으로 남은 분자입니다. </summary>
        private long _drainRemainder;
        /// <summary> 초당 논리 틱 수입니다. 호출자는 동일한 주기로 진행해야 합니다. </summary>
        public const int TicksPerSecond = 50;
        /// <summary> 게이지 1점을 표현하는 내부 정수 단위 수입니다. </summary>
        public const int UnitsPerPoint = 100;
        /// <summary> 이번 실행에서 피벗 기능이 허용되는지 여부입니다. </summary>
        public bool IsEnabled { get; }
        /// <summary> 게이지를 소비하며 피벗 모드가 유지 중인지 여부입니다. </summary>
        public bool IsActive { get; private set; }
        /// <summary> 현재 충전량 또는 잔여 게이지이며 1단위는 0.01점입니다. </summary>
        public int GaugeUnits { get; private set; }
        /// <summary> 표시용 게이지 점수입니다. 판정에는 정수 단위를 사용합니다. </summary>
        public float GaugePoints => (float)GaugeUnits / UnitsPerPoint;
        /// <summary> 최대 게이지 점수입니다. </summary>
        public int Capacity => _settings.Capacity;
        /// <summary> 현재 적용되는 기본 소비 속도의 백분율입니다. </summary>
        public int CurrentDrainPercent { get; private set; }
        /// <summary> 피버와 독립적으로 세는 빠른 연속 유저 매칭 콤보입니다. </summary>
        public int ComboCount { get; private set; }
        /// <summary> 종료되어 추가 입력과 틱을 받지 않는지 여부입니다. </summary>
        public bool IsStopped { get; private set; }

        /// <summary> 독립 피벗 상태를 생성합니다. </summary>
        /// <param name="isEnabled">현재 게임에서 피벗을 허용할지 여부</param>
        /// <param name="settings">활성 기능에 적용할 설정이며 비활성 기능에서는 검증하지 않음</param>
        public PivotMode(bool isEnabled, PivotSettings settings)
        {
            if (isEnabled)
            {
                if (settings == null)
                {
                    throw new ArgumentNullException(nameof(settings));
                }
                _settings = settings.CopyValidated();
            }
            else
            {
                // 스테이지에서는 대전 설정 오류가 본게임 실행을 막지 않도록 무시합니다.
                _settings = new PivotSettings();
            }
            IsEnabled = isEnabled;
            ResetCombo();
        }

        /// <summary> 한 틱의 파괴 결과를 반영하고 활성 게이지를 소비합니다. </summary>
        /// <param name="destroyedBlocks">이번 틱에 실제로 파괴된 블럭 수</param>
        /// <param name="successfulPlayerMatches">이번 틱에 유저 조작으로 매칭을 성공한 횟수</param>
        public void AdvanceTick(int destroyedBlocks, int successfulPlayerMatches = 0)
        {
            if (destroyedBlocks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(destroyedBlocks));
            }
            if (successfulPlayerMatches < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(successfulPlayerMatches));
            }
            if (!IsEnabled || IsStopped)
            {
                return;
            }

            _ticksSincePlayerMatch = Math.Min((long)_settings.ComboWindowTicks + 1, _ticksSincePlayerMatch + 1);
            if (_ticksSincePlayerMatch > _settings.ComboWindowTicks)
            {
                ComboCount = 0;
                CurrentDrainPercent = 100;
            }
            int rapidCombos = ComboCount > 0 ? successfulPlayerMatches : Math.Max(0, successfulPlayerMatches - 1);
            if (successfulPlayerMatches > 0)
            {
                // 자동 매칭과 폭탄·피벗 추가 파괴는 유저 매칭 성공 횟수를 늘리지 않습니다.
                ComboCount = (int)Math.Min(int.MaxValue, (long)ComboCount + successfulPlayerMatches);
                _ticksSincePlayerMatch = 0;
            }

            if (!IsActive)
            {
                // 10콤보부터 블럭당 0.11점, 20콤보부터 0.12점으로 정수 단위 충전합니다.
                long chargePerBlock = _settings.ChargePerBlockUnits +
                    (long)(ComboCount / _settings.ChargeComboStep) * _settings.ChargeBonusPerStepUnits;
                long remaining = (long)_settings.Capacity * UnitsPerPoint - GaugeUnits;
                // 실제 부족량까지만 곱셈에 사용하여 큰 입력에서도 오버플로를 막습니다.
                long charge = Math.Min(remaining, Math.Min(remaining, chargePerBlock) * destroyedBlocks);
                GaugeUnits += (int)charge;
                if (GaugeUnits == _settings.Capacity * UnitsPerPoint)
                {
                    IsActive = true;
                    CurrentDrainPercent = 100;
                    _drainRemainder = 0;
                }
                // 발동한 틱에는 소비하지 않아 기본 유지 시간을 온전히 제공합니다.
                return;
            }

            if (rapidCombos > 0)
            {
                CurrentDrainPercent = (int)Math.Max(_settings.MinimumDrainPercent,
                    CurrentDrainPercent - (long)_settings.DrainReductionPercentPerCombo * rapidCombos);
            }

            // 초당 소비량과 감속 비율의 나머지를 이월해 작은 소비량도 누락하지 않습니다.
            long drainNumerator = (long)_settings.DrainPerSecond * UnitsPerPoint * CurrentDrainPercent + _drainRemainder;
            const int drainDenominator = TicksPerSecond * 100;
            long drain = drainNumerator / drainDenominator;
            _drainRemainder = drainNumerator % drainDenominator;
            GaugeUnits = (int)Math.Max(0, GaugeUnits - drain);
            if (GaugeUnits == 0)
            {
                IsActive = false;
                ResetCombo();
            }
        }

        /// <summary> 게임 종료 시 활성 상태와 게이지를 정리하고 더 이상 진행하지 않습니다. </summary>
        public void Stop()
        {
            IsStopped = true;
            IsActive = false;
            GaugeUnits = 0;
            ResetCombo();
        }

        /// <summary> 소비 속도와 빠른 콤보 판정 기준을 초기화합니다. </summary>
        private void ResetCombo()
        {
            CurrentDrainPercent = 100;
            ComboCount = 0;
            _drainRemainder = 0;
            _ticksSincePlayerMatch = (long)_settings.ComboWindowTicks + 1;
        }
    }
}
