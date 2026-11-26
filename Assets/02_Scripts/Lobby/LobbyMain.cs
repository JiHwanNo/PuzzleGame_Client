using System.Collections.Generic;
using System.IO;
using Puzzle.Core;
using TMPro;
using UnityEngine;

/// <summary>로비의 스테이지 준비 팝업과 대전·리플레이 진입을 담당합니다.</summary>
public class LobbyMain : MonoBehaviour
{
    [SerializeField] private TMP_Text _statusText;

    /// <summary>로비 탭이 열리면 방치형 전장을 표시하고 방어전을 시작하거나 이어간다.</summary>
    private void Start()
    {
        LobbyDefenseView.Create(_statusText);
    }

    /// <summary>로비에서 스테이지 준비 팝업을 엽니다.</summary>
    public void OnClickStartStage()
    {
        DomainManager.Instance.OpenPopup("PopupReady");
    }

    /// <summary>최근 기록의 조건으로 게임을 시작하고 상대 보드에 기록을 재생합니다.</summary>
    private void OnClickReplay()
    {
        if (!TryGetReplayFiles(out string[] files))
        {
            _statusText.text = "플레이 기록 폴더를 읽을 수 없습니다. 잠시 후 다시 시도해 주세요.";
            return;
        }
        if (files.Length == 0)
        {
            _statusText.text = "아직 플레이 기록이 없습니다.\nStage 또는 Battle을 한 판 마치면 다시 볼 수 있어요.";
            return;
        }

        ReplayData replayData = null;
        for (int i = files.Length - 1; i >= 0; i--)
        {
            ReplayData candidate = ReplayStorage.Load(files[i]);
            if (candidate == null)
            {
                continue;
            }
            GameMode mode = candidate.formatVersion == ReplayData.CurrentFormatVersion ? candidate.mode : GameMode.Stage;
            if (StageInjection.Instance.MakeGameSpec(candidate.ruleAddress, candidate.stageAddress, mode))
            {
                replayData = candidate;
                break;
            }
        }
        if (replayData == null)
        {
            _statusText.text = "재생할 수 있는 기록이 없습니다.\nStage 또는 Battle에서 새 기록을 만들어 보세요.";
            return;
        }

        GameSpec spec = StageInjection.Instance.GetGameSpec();
        spec.randomSeed = replayData.randomSeed;
        if (replayData.formatVersion == ReplayData.CurrentFormatVersion)
        {
            spec.rule.timeLimit = replayData.timeLimit;
        }

        // 이전 스테이지 기록은 원래 종료 규칙으로 재생하며 대전 기록으로 바꾸지 않습니다.
        StageInjection.Instance.SetReplayData(replayData);
        Main.Instance.MoveScene(SceneEnum.LobbyScene, SceneEnum.GameScene);
    }

    /// <summary>로컬 기록 경로를 파일명 순으로 정렬해 <paramref name="files"/>에 반환한다.</summary>
    private static bool TryGetReplayFiles(out string[] files)
    {
        files = System.Array.Empty<string>();
        try
        {
            files = Directory.GetFiles(ReplayStorage.GetReplayDirectoryPath(), "replay_*.json");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (System.Exception exception) when (exception is IOException || exception is System.UnauthorizedAccessException)
        {
            Debug.LogWarning($"[LobbyMain] 리플레이 목록을 읽지 못했습니다. {exception.Message}");
            return false;
        }
    }

    /// <summary>90초 대전용 준비 팝업을 엽니다.</summary>
    private void OnClickBattle()
    {
        DomainManager.Instance.OpenPopup("PopupReady", popup => popup.GetComponent<PopupReady>().ConfigureBattle());
    }

    /// <summary>추첨된 규칙에 맞는 최신 완주 기록을 찾아 같은 조건의 대전을 준비합니다.</summary>
    /// <param name="ruleAddress">준비 팝업에서 확정한 퍼즐 규칙 주소입니다.</param>
    internal static bool PrepareBattle(string ruleAddress)
    {
        ReplayData opponent = null;
        GameSpec spec = null;
        Dictionary<(string Rule, string Stage), GameSpec> candidateSpecs = new Dictionary<(string Rule, string Stage), GameSpec>();
        if (TryGetReplayFiles(out string[] files))
        {
            for (int i = files.Length - 1; i >= 0; i--)
            {
                ReplayData candidate = ReplayStorage.Load(files[i]);
                if (candidate == null || candidate.ruleAddress != ruleAddress || !candidate.IsCompleteBattleRecord() ||
                    candidate.comboRuleVersion != ReplayData.CurrentComboRuleVersion ||
                    candidate.numericRuleVersion != ReplayData.CurrentNumericRuleVersion)
                {
                    continue;
                }
                (string Rule, string Stage) key = (candidate.ruleAddress, candidate.stageAddress);
                if (!candidateSpecs.TryGetValue(key, out GameSpec candidateSpec))
                {
                    // 같은 주소의 기록이 많아도 이번 탐색에서는 데이터를 한 번만 파싱합니다.
                    bool loaded = StageInjection.Instance.MakeGameSpec(candidate.ruleAddress, candidate.stageAddress, GameMode.Battle);
                    candidateSpec = loaded ? StageInjection.Instance.GetGameSpec() : null;
                    candidateSpecs.Add(key, candidateSpec);
                }
                if (candidateSpec == null)
                {
                    continue;
                }
                if (candidate.HasCompatibleBattleRules(candidateSpec.pivotSettings, candidateSpec.rule.puzzleType == PuzzleType.Link))
                {
                    // 다른 후보를 읽은 뒤 캐시된 후보가 선택되면 실제 씬 주입 데이터도 복원합니다.
                    if (!ReferenceEquals(candidateSpec, StageInjection.Instance.GetGameSpec()))
                    {
                        if (!StageInjection.Instance.MakeGameSpec(candidate.ruleAddress, candidate.stageAddress, GameMode.Battle))
                        {
                            continue;
                        }
                        candidateSpec = StageInjection.Instance.GetGameSpec();
                    }
                    opponent = candidate;
                    spec = candidateSpec;
                    break;
                }
            }
        }

        if (spec == null)
        {
            if (!StageInjection.Instance.MakeGameSpec(ruleAddress, "Stage", GameMode.Battle))
            {
                Debug.LogError("[LobbyMain] 대전용 데이터를 준비하지 못했습니다.");
                return false;
            }
            spec = StageInjection.Instance.GetGameSpec();
        }
        spec.rule.timeLimit = GameSpec.BattleDurationSeconds;
        if (opponent != null)
        {
            spec.randomSeed = opponent.randomSeed;
        }
        StageInjection.Instance.SetReplayData(opponent);
        return true;
    }
}
