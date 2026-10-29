using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 스테이지의 클리어 목표(Score, Block Collection 등)와 현재 달성 수치를 실시간으로 관리하는 클래스입니다.
    /// 보드 로직에서 블럭이 파괴되거나 점수를 획득할 때 이 매니저를 통해 수치를 갱신하며,
    /// 모든 목표가 달성되었는지를 판별하여 게임 종료(Finish) 시점을 결정하는 역할을 합니다.
    /// </summary>
    public class ObjectiveManager
    {
        /// <summary> 현재 스테이지에서 획득한 총 점수 </summary>
        public int CurrentScore { get; private set; }

        /// <summary> 현재 누적된 액티브 콤보 수 </summary>
        public int ComboCount { get; private set; }

        /// <summary> 콤보가 유지되기 위해 남은 프레임 수 </summary>
        public int ComboTimerFrames { get; private set; }

        /// <summary> 현재 피버 타임(Fever Time)이 발동 중인지 여부 </summary>
        public bool IsFeverTime { get; private set; }

        /// <summary> 피버 타임이 종료되기까지 남은 프레임 수 </summary>
        public int FeverTimerFrames { get; private set; }

        /// <summary> 스테이지 종료까지 남은 시간 (초) </summary>
        public float RemainingTime => (float)_remainingFrames / GameSpec.LogicFramesPerSecond;

        /// <summary> 실수 누적 오차 없이 차감하는 남은 논리 틱 수입니다. </summary>
        private int _remainingFrames;
        /// <summary> 목표 달성으로 조기 종료할 수 있는지 결정하는 모드입니다. </summary>
        private readonly GameMode _mode;

        /// <summary> 초기 설정된 제한 시간 (초) </summary>
        public float TotalLimitTime { get; private set; }

        public const int COMBO_TIME_LIMIT = 150; // 3초 (50fps 기준)
        public const int FEVER_TIME_LIMIT = 350; // 7초 (50fps 기준)
        public const int FEVER_COMBO_THRESHOLD = 7; // 피버 발동에 필요한 콤보 수

        /// <summary> 
        /// 각 목표 대상별 현재 달성 수치를 저장하는 딕셔너리입니다.
        /// Key: 목표 블럭의 ID (CollectBlock 타입인 경우)
        /// Value: 현재까지 파괴/수집된 개수
        /// </summary>
        private Dictionary<string, int> _currentCounts = new Dictionary<string, int>();
        
        /// <summary> 스테이지 시작 시 설정된 클리어 목표 데이터 목록 </summary>
        private List<ObjectiveData> _objectives;

        /// <summary>
        /// 지정된 목표 데이터를 바탕으로 매니저를 초기화합니다.
        /// </summary>
        /// <param name="objectives">RuleData로부터 전달받은 목표 리스트</param>
        /// <param name="timeLimit">스테이지 제한 시간 (초)</param>
        /// <param name="mode">스테이지 또는 대전 종료 규칙</param>
        public ObjectiveManager(List<ObjectiveData> objectives, float timeLimit = 0, GameMode mode = GameMode.Stage)
        {
            _objectives = objectives;
            if (_objectives == null)
            {
                _objectives = new List<ObjectiveData>();
            }
            _mode = mode;
            TotalLimitTime = mode == GameMode.Battle ? GameSpec.BattleDurationSeconds : timeLimit;
            _remainingFrames = (int)System.Math.Ceiling(System.Math.Max(0, TotalLimitTime) * GameSpec.LogicFramesPerSecond);
            CurrentScore = 0;
            ComboCount = 0;
            ComboTimerFrames = 0;
            IsFeverTime = false;
            FeverTimerFrames = 0;
            _currentCounts = new Dictionary<string, int>();
        }

        /// <summary>
        /// 매 고정 프레임(FixedUpdate)마다 호출되어 콤보, 피버 타임, 스테이지 제한 시간을 갱신합니다.
        /// </summary>
        /// <param name="isWaiting">보드가 현재 유저의 입력을 대기(Waiting) 중인지 여부</param>
        public void UpdateTimer(bool isWaiting)
        {
            // 정수 틱을 차감하여 두 보드가 정확히 같은 3,000번째 틱에 종료되도록 합니다.
            if (_remainingFrames > 0)
            {
                _remainingFrames--;
            }

            // 2. 피버 타임 및 콤보 타이머 갱신
            if (IsFeverTime)
            {
                FeverTimerFrames--;
                if (FeverTimerFrames <= 0)
                {
                    IsFeverTime = false;
                    ComboCount = 0; // 피버 종료 시 콤보 초기화
                }
            }
            else
            {
                // 유저가 입력 가능한 대기 상태일 때만 콤보 타이머가 감소합니다 (연출 시간은 콤보 타이머에서 제외)
                if (isWaiting && ComboTimerFrames > 0)
                {
                    ComboTimerFrames--;
                    if (ComboTimerFrames <= 0)
                    {
                        ComboCount = 0; // 시간 초과로 콤보 초기화
                    }
                }
            }
        }

        /// <summary>
        /// 시간이 모두 소진되었는지 확인합니다.
        /// </summary>
        public bool IsTimeOver() => TotalLimitTime > 0 && _remainingFrames <= 0;

        /// <summary> 모드에 따라 목표 달성 또는 제한 시간 종료를 판정합니다. </summary>
        /// <returns>현재 게임을 종료할 수 있으면 true</returns>
        public bool ShouldFinish() => IsTimeOver() || (_mode == GameMode.Stage && IsAllObjectivesCleared());

        /// <summary>
        /// 유저 조작 또는 연쇄 작용으로 매칭이 발생했을 때 호출되어 콤보를 갱신합니다.
        /// </summary>
        public void OnMatchEvent()
        {
            if (!IsFeverTime)
            {
                ComboCount++;
                ComboTimerFrames = COMBO_TIME_LIMIT;

                if (ComboCount >= FEVER_COMBO_THRESHOLD)
                {
                    IsFeverTime = true;
                    FeverTimerFrames = FEVER_TIME_LIMIT;
                }
            }
        }

        /// <summary>
        /// 특정 블럭이 파괴되었을 때 호출되어 점수를 합산하고 수집 카운트를 갱신합니다.
        /// </summary>
        /// <param name="blockId">파괴된 블럭의 고유 아이디</param>
        /// <param name="score">해당 블럭 파괴로 얻는 기본 점수 (기본값 10점)</param>
        public void OnBlockDestroyed(string blockId, int score = 10)
        {
            float multiplier = 1.0f;
            if (IsFeverTime)
            {
                multiplier = 2.0f; // 피버 타임 시 모든 점수 2배
            }
            else if (ComboCount > 0)
            {
                // 1콤보 = 1.0x, 2콤보 = 1.2x, 3콤보 = 1.4x ... 최대 3.0x
                multiplier = 1.0f + (ComboCount - 1) * 0.2f;
                if (multiplier > 3.0f) multiplier = 3.0f;
            }

            // 1. 전체 점수 누적 (배율 적용)
            CurrentScore += (int)(score * multiplier);

            if (string.IsNullOrEmpty(blockId))
                return;

            // 2. 특정 블럭 수집 카운트 갱신
            if (_currentCounts.ContainsKey(blockId))
            {
                _currentCounts[blockId]++;
            }
            else
            {
                _currentCounts[blockId] = 1;
            }
        }

        /// <summary>
        /// 현재 설정된 모든 클리어 조건이 만족되었는지 검사합니다.
        /// </summary>
        /// <returns>모든 목표를 달성했다면 true, 하나라도 미달성이라면 false</returns>
        public bool IsAllObjectivesCleared()
        {
            if (_objectives == null || _objectives.Count == 0)
            {
                return false;
            }

            foreach (var obj in _objectives)
            {
                switch (obj.type)
                {
                    case ObjectiveType.Score:
                        // 목표 점수에 도달했는지 체크
                        if (CurrentScore < obj.count)
                        {
                            return false;
                        }
                        break;

                    case ObjectiveType.CollectBlock:
                        // 특정 블럭을 목표 개수만큼 모았는지 체크
                        _currentCounts.TryGetValue(obj.targetId, out int count);
                        if (count < obj.count)
                        {
                            return false;
                        }
                        break;

                    default:
                        // 알 수 없는 목표 종류도 검사 없이 성공 처리하지 않습니다.
                        return false;
                }
            }
            // 모든 루프를 통과하면 모든 목표 달성
            return true;
        }
    }
}
