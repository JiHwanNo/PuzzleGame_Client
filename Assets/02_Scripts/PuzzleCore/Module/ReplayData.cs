using System;
using System.Collections.Generic;
using Battle.Pivot;

namespace Puzzle.Core
{
    /// <summary>
    /// 리플레이 재생에 필요한 모든 데이터를 담는 직렬화 가능한 클래스입니다.
    /// 동일한 시드와 입력 시퀀스를 재현하면 결정론적으로 같은 게임 결과를 얻을 수 있습니다.
    /// </summary>
    [Serializable]
    public class ReplayData
    {
        /// <summary> 종료 규칙과 기록 길이를 포함하는 현재 저장 형식입니다. </summary>
        public const int CurrentFormatVersion = 1;
        /// <summary> 유저 조작 성공만 콤보로 세는 현재 판정 버전입니다. </summary>
        public const int CurrentComboRuleVersion = 1;
        /// <summary> 콤보 판정 버전입니다. 없는 기존 기록의 0은 자동 연쇄 포함 규칙을 사용합니다. </summary>
        public int comboRuleVersion;
        /// <summary> 없으면 이전 형식으로 읽으며 대전 상대로 사용하지 않습니다. </summary>
        public int formatVersion;
        /// <summary> 기록 당시의 종료 규칙입니다. </summary>
        public GameMode mode;
        /// <summary> 기록 당시의 제한 시간(초)입니다. </summary>
        public float timeLimit;
        /// <summary> 실행을 완료한 총 논리 틱 수입니다. 첫 입력 프레임은 0입니다. </summary>
        public ulong frameCount;
        /// <summary> 기록 종료 시점의 점수입니다. 재생 결과 검증에 사용합니다. </summary>
        public int finalScore;

        /// <summary> 기록 당시 피벗 규칙입니다. 없는 기존 기록은 기본 설정으로 표시합니다. </summary>
        public PivotSettings pivotSettings;

        /// <summary> 동일한 60초 규칙으로 완료한 대전 기록인지 검사합니다. </summary>
        /// <returns>대전 상대로 사용할 수 있는 기록이면 true</returns>
        public bool IsCompleteBattleRecord()
        {
            if (formatVersion != CurrentFormatVersion || mode != GameMode.Battle ||
                comboRuleVersion < 0 || comboRuleVersion > CurrentComboRuleVersion ||
                System.Math.Abs(timeLimit - GameSpec.BattleDurationSeconds) > 0.0001f || float.IsNaN(timeLimit) || frameCount != GameSpec.BattleFrameCount ||
                finalScore < 0 || inputs == null || inputEnds == null ||
                string.IsNullOrEmpty(ruleAddress) || string.IsNullOrEmpty(stageAddress))
            {
                return false;
            }
            if (pivotSettings != null && !pivotSettings.IsValid())
            {
                return false;
            }
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].frame >= frameCount || (i > 0 && inputs[i].frame < inputs[i - 1].frame))
                {
                    return false;
                }
            }
            for (int i = 0; i < inputEnds.Count; i++)
            {
                if (inputEnds[i].frame >= frameCount || (i > 0 && inputEnds[i].frame < inputEnds[i - 1].frame))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary> 현재 콤보 및 피벗 규칙과 동일한 조건의 대전 상대인지 검사합니다. </summary>
        /// <param name="currentSettings">현재 플레이에 적용할 피벗 설정</param>
        /// <returns>완주 기록이며 현재 규칙으로 점수를 비교할 수 있으면 true</returns>
        public bool HasCompatibleBattleRules(PivotSettings currentSettings)
        {
            if (!IsCompleteBattleRecord() || comboRuleVersion != CurrentComboRuleVersion ||
                currentSettings == null || !currentSettings.IsValid())
            {
                return false;
            }
            return currentSettings.HasSameRules(pivotSettings);
        }

        /// <summary> 규칙 JSON의 Addressable 에셋 주소 </summary>
        public string ruleAddress;

        /// <summary> 스테이지 JSON의 Addressable 에셋 주소 </summary>
        public string stageAddress;

        /// <summary> 게임에 사용된 난수 시드 </summary>
        public int randomSeed;

        /// <summary> 유저의 입력(클릭/터치) 기록 리스트 </summary>
        public List<InputRecord> inputs;

        /// <summary> 유저의 입력 종료(포인터 릴리즈) 기록 리스트 </summary>
        public List<InputEndRecord> inputEnds;

        /// <summary> 리플레이가 기록된 일시 (ISO 8601 형식) </summary>
        public string recordedAt;
    }
}
