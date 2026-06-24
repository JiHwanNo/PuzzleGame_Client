using System.Collections.Generic;
using Puzzle.Core;
using UnityEngine;

/// <summary>
/// 스테이지 맵 툴에서 현재 규칙(Rule)의 블럭 정의 목록을 로드합니다.
/// 프로젝트 규약대로 AssetManager 경유 Addressable 로드를 사용합니다.
/// </summary>
public class StageMapRuleProvider
{
    /// <summary>
    /// 규칙 주소로 블럭 정의 목록을 로드합니다. 실패 시 빈 목록을 반환합니다.
    /// </summary>
    /// <param name="ruleAddress">규칙 Addressable 주소입니다(예: "ThreeMatchRule").</param>
    /// <returns>규칙에 정의된 블럭 목록입니다.</returns>
    public List<BlockData> LoadBlocks(string ruleAddress)
    {
        if (string.IsNullOrEmpty(ruleAddress))
        {
            return new List<BlockData>();
        }

        TextAsset asset = AssetManager.Instance.LoadAsset<TextAsset>(ruleAddress);
        if (asset == null)
        {
            Debug.LogError($"[StageMapRuleProvider] 규칙 에셋 로드 실패: {ruleAddress}");
            return new List<BlockData>();
        }

        GameRuleContainer container = JsonUtility.FromJson<GameRuleContainer>(asset.text);
        if (container == null || container.blocks == null)
        {
            Debug.LogError($"[StageMapRuleProvider] 규칙 JSON 파싱 실패: {ruleAddress}");
            return new List<BlockData>();
        }

        return container.blocks;
    }
}
