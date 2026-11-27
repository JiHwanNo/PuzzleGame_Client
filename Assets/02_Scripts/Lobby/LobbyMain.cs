using System.Collections.Generic;
using System.IO;
using Puzzle.Core;
using TMPro;
using UnityEngine;

/// <summary>로비의 첫 기록 튜토리얼과 리플레이 상대 대전 진입을 담당한다.</summary>
public class LobbyMain : MonoBehaviour
{
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private LobbyDefenseBoard.StagePlan[] _defenseStages = LobbyDefenseBoard.DefaultStages();

    /// <summary>로비 탭이 열리면 방치형 전장을 표시하고 방어전을 시작하거나 이어간다.</summary>
    private System.Collections.IEnumerator Start()
    {
        // 로비를 직접 실행하면 공유 씬이 임시 카메라를 교체한 뒤 전장을 연결한다.
        while (CameraController.MainCamera == null)
        {
            yield return null;
        }
        if (!LobbyUnit.InitializeCatalog() && LobbyDefenseView.HasSavedProgress)
        {
            _statusText.text = "방어 유닛 정보를 불러오지 못했습니다. 로비에 다시 들어와 주세요.";
            yield break;
        }
        LobbyDefenseView field = LobbyDefenseView.Create(_statusText, CameraController.MainCamera, _defenseStages);
        LobbyNavigation.Create(_statusText, field);
        _statusText.text = "플레이 시 라이프 1개 사용";
    }

    /// <summary>호환 기록을 찾아 튜토리얼 또는 상대 대전을 준비하고 게임 씬으로 이동한다.</summary>
    public void OnClickPlay()
    {
        if (Main.Instance == null || Main.Instance.IsMovingScene)
        {
            return;
        }
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (PlayerLives.GetRemaining(now, out int seconds) == 0)
        {
            _statusText.text = $"라이프 충전까지 {seconds / 60:00}:{seconds % 60:00}";
            return;
        }
        if (!PrepareBattle(null))
        {
            _statusText.text = "게임을 준비하지 못했습니다. 다시 시도해 주세요.";
            return;
        }
        if (PlayerLives.TrySpend(now))
        {
            Main.Instance.MoveScene(SceneEnum.LobbyScene, SceneEnum.GameScene);
        }
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

    /// <summary><paramref name="ruleAddress"/> 규칙 또는 null이면 모든 규칙의 최신 호환 기록을 찾고, 없으면 첫 기록을 준비한다.</summary>
    internal static bool PrepareBattle(string ruleAddress)
    {
        ReplayData opponent = null;
        GameSpec spec = null;
        Dictionary<(string Rule, string Stage), GameSpec> candidateSpecs = new Dictionary<(string Rule, string Stage), GameSpec>();
        bool readable = TryGetReplayFiles(out string[] files);
        if (!readable && ruleAddress == null)
        {
            return false;
        }
        if (readable)
        {
            for (int i = files.Length - 1; i >= 0; i--)
            {
                ReplayData candidate = ReplayStorage.Load(files[i]);
                if (candidate == null || (ruleAddress != null && candidate.ruleAddress != ruleAddress) || !candidate.IsCompleteBattleRecord() ||
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
            if (ruleAddress == null)
            {
                ruleAddress = "TapMatchRule";
            }
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
