using System.IO;
using Puzzle.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 준비(Ready) 팝업.
/// PopupHandler를 상속받아 시작/닫기/리플레이 버튼 로직을 구현합니다.
/// </summary>
public class PopupReady : PopupHandler
{

    /// <summary>
    /// 랜덤 퍼즐 모드 선택에 사용할 규칙 에셋 주소 목록입니다.
    /// </summary>
    private static readonly string[] RulePaths = new string[]
    {
        "LinkMatchRule",
        "ThreeMatchRule",
        "TapMatchRule"
    };

    /// <summary>
    /// 시작 버튼 클릭 시 랜덤 퍼즐 모드로 게임 데이터를 준비하고 게임 씬으로 이동합니다.
    /// Link, 3매치, 탭 블라스트 중 하나를 선택하고 스테이지의 목표/제한 시간을 적용합니다.
    /// </summary>
    private void OnClickStart()
    {
        // 3가지 퍼즐 모드 중 랜덤 선택
        string rulePath = RulePaths[Random.Range(0, RulePaths.Length)];
        string stagePath = "Stage";

        if (!StageInjection.Instance.MakeGameSpec(rulePath, stagePath))
        {
            Debug.LogError("[PopupReady] GameSpec 준비에 실패했습니다.");
            return;
        }

        Debug.Log($"[PopupReady] 선택된 퍼즐 모드: {rulePath}");
        Main.Instance.MoveScene(SceneEnum.LobbyScene, SceneEnum.GameScene);
    }

    /// <summary>
    /// 리플레이 테스트 버튼 클릭 시 가장 최근 리플레이를 로드하고 게임을 시작합니다.
    /// 유저는 정상 플레이하며, 상대방 보드에 리플레이가 재생됩니다.
    /// </summary>
    private void OnClickReplay()
    {
        // 가장 최근 리플레이 파일 탐색
        string replayDir = ReplayStorage.GetReplayDirectoryPath();
        if (!Directory.Exists(replayDir))
        {
            Debug.LogError("리플레이 디렉터리가 존재하지 않습니다.");
            return;
        }

        string[] files = Directory.GetFiles(replayDir, "replay_*.json");
        if (files.Length == 0)
        {
            Debug.LogError("저장된 리플레이 파일이 없습니다.");
            return;
        }

        // 파일명 기준 정렬하여 가장 최근 파일 선택
        System.Array.Sort(files);
        string latestFile = files[files.Length - 1];

        ReplayData replayData = ReplayStorage.Load(latestFile);
        if (replayData == null)
        {
            Debug.LogError("리플레이 데이터 로드에 실패했습니다.");
            return;
        }

        // 유저 게임 데이터 준비 (리플레이와 동일한 규칙/스테이지 사용)
        if (!StageInjection.Instance.MakeGameSpec(replayData.ruleAddress, replayData.stageAddress))
        {
            Debug.LogError("[PopupReady] 리플레이용 GameSpec 준비에 실패했습니다.");
            return;
        }

        GameSpec spec = StageInjection.Instance.GetGameSpec();
        spec.randomSeed = replayData.randomSeed;
        spec.mode = replayData.formatVersion == ReplayData.CurrentFormatVersion ? replayData.mode : GameMode.Stage;
        if (replayData.formatVersion == ReplayData.CurrentFormatVersion)
        {
            spec.rule.timeLimit = replayData.timeLimit;
        }

        // 이전 스테이지 기록은 원래 종료 규칙으로 재생하며 대전 기록으로 바꾸지 않습니다.
        StageInjection.Instance.SetReplayData(replayData);
        Main.Instance.MoveScene(SceneEnum.LobbyScene, SceneEnum.GameScene);
    }

    /// <summary> 완주한 60초 기록과 대전하거나, 기록이 없으면 첫 대전 기록을 만듭니다. </summary>
    private void OnClickBattle()
    {
        ReplayData opponent = null;
        string directory = ReplayStorage.GetReplayDirectoryPath();
        if (Directory.Exists(directory))
        {
            string[] files = Directory.GetFiles(directory, "replay_*.json");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            for (int i = files.Length - 1; i >= 0; i--)
            {
                ReplayData candidate = ReplayStorage.Load(files[i]);
                if (candidate != null && candidate.IsCompleteBattleRecord())
                {
                    opponent = candidate;
                    break;
                }
            }
        }

        string ruleAddress = opponent != null ? opponent.ruleAddress : RulePaths[Random.Range(0, RulePaths.Length)];
        string stageAddress = opponent != null ? opponent.stageAddress : "Stage";
        if (!StageInjection.Instance.MakeGameSpec(ruleAddress, stageAddress))
        {
            Debug.LogError("[PopupReady] 대전용 데이터를 준비하지 못했습니다.");
            return;
        }
        GameSpec spec = StageInjection.Instance.GetGameSpec();
        spec.mode = GameMode.Battle;
        spec.rule.timeLimit = GameSpec.BattleDurationSeconds;
        if (opponent != null)
        {
            spec.randomSeed = opponent.randomSeed;
        }
        StageInjection.Instance.SetReplayData(opponent);
        Main.Instance.MoveScene(SceneEnum.LobbyScene, SceneEnum.GameScene);
    }

    /// <summary>
    /// 닫기 버튼 클릭 시 호출됩니다.
    /// </summary>
    private void OnClickClose()
    {
        ClosePopup();
    }
}
