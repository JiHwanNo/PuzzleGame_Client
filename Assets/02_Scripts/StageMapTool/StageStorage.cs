using System.IO;
using Puzzle.Core;
using UnityEngine;

/// <summary>
/// 모드별 스테이지 JSON을 다운로드 경로 또는 Resources에서 로드하는 저장소입니다.
/// </summary>
public static class StageStorage
{
    /// <summary> 스테이지 Resources 루트 경로입니다. </summary>
    private const string StageRoot = "Stage";

    /// <summary> 모드별 최소 스테이지 번호입니다. </summary>
    public const int MinStageId = 1;

    /// <summary> 모드별 최대 스테이지 번호입니다. </summary>
    public const int MaxStageId = 100;

    /// <summary>
    /// 퍼즐 타입과 스테이지 번호로 스테이지 JSON을 로드합니다.
    /// </summary>
    /// <param name="puzzleType">스테이지가 속한 퍼즐 모드입니다.</param>
    /// <param name="stageId">로드할 스테이지 번호입니다.</param>
    /// <param name="json">로드된 JSON 문자열입니다.</param>
    /// <returns>스테이지 JSON 로드 성공 여부입니다.</returns>
    public static bool TryLoadStageJson(PuzzleType puzzleType, int stageId, out string json)
    {
        if (!TryGetResourceKey(puzzleType, stageId, out string resourceKey))
        {
            json = null;
            return false;
        }

        return TryLoadStageJson(resourceKey, out json);
    }

    /// <summary>
    /// 스테이지 리소스 키로 스테이지 JSON을 로드합니다.
    /// </summary>
    /// <param name="resourceKey">Resources 기준 스테이지 키입니다.</param>
    /// <param name="json">로드된 JSON 문자열입니다.</param>
    /// <returns>스테이지 JSON 로드 성공 여부입니다.</returns>
    public static bool TryLoadStageJson(string resourceKey, out string json)
    {
        json = null;

        if (string.IsNullOrEmpty(resourceKey))
        {
            Debug.LogError("[StageStorage] 스테이지 리소스 키가 비어 있습니다.");
            return false;
        }

        string downloadedPath = GetDownloadedPath(resourceKey);
        if (File.Exists(downloadedPath))
        {
            json = File.ReadAllText(downloadedPath);
            return true;
        }

        TextAsset textAsset = Resources.Load<TextAsset>(resourceKey);
        if (textAsset != null)
        {
            json = textAsset.text;
            return true;
        }

        Debug.LogError($"[StageStorage] 스테이지 JSON을 찾을 수 없습니다. 다운로드 경로: {downloadedPath}, Resources 키: {resourceKey}");
        return false;
    }

    /// <summary>
    /// 퍼즐 타입과 스테이지 번호로 Resources 기준 키를 반환합니다.
    /// </summary>
    /// <param name="puzzleType">스테이지가 속한 퍼즐 모드입니다.</param>
    /// <param name="stageId">스테이지 번호입니다.</param>
    /// <returns>Resources.Load에 사용할 스테이지 키입니다.</returns>
    public static string GetResourceKey(PuzzleType puzzleType, int stageId)
    {
        if (!TryGetResourceKey(puzzleType, stageId, out string resourceKey))
        {
            return null;
        }

        return resourceKey;
    }

    /// <summary>
    /// 퍼즐 타입과 스테이지 번호를 검증한 뒤 Resources 기준 키를 반환합니다.
    /// </summary>
    /// <param name="puzzleType">스테이지가 속한 퍼즐 모드입니다.</param>
    /// <param name="stageId">스테이지 번호입니다.</param>
    /// <param name="resourceKey">Resources.Load에 사용할 스테이지 키입니다.</param>
    /// <returns>스테이지 키 생성 성공 여부입니다.</returns>
    public static bool TryGetResourceKey(PuzzleType puzzleType, int stageId, out string resourceKey)
    {
        resourceKey = null;

        if (!IsValidStageId(stageId))
        {
            Debug.LogError($"[StageStorage] 스테이지 번호가 범위를 벗어났습니다. stageId: {stageId}, 허용 범위: {MinStageId}~{MaxStageId}");
            return false;
        }

        if (!TryGetModeFolder(puzzleType, out string modeFolder))
        {
            Debug.LogError($"[StageStorage] 지원하지 않는 퍼즐 타입입니다. puzzleType: {puzzleType}");
            return false;
        }

        resourceKey = $"{StageRoot}/{modeFolder}/{GetStageFileName(stageId)}";
        return true;
    }

    /// <summary>
    /// 스테이지 번호가 허용 범위 안에 있는지 확인합니다.
    /// </summary>
    /// <param name="stageId">검사할 스테이지 번호입니다.</param>
    /// <returns>유효한 스테이지 번호인지 여부입니다.</returns>
    public static bool IsValidStageId(int stageId)
    {
        return stageId >= MinStageId && stageId <= MaxStageId;
    }

    /// <summary>
    /// 퍼즐 타입에 대응하는 스테이지 폴더명을 반환합니다.
    /// </summary>
    /// <param name="puzzleType">폴더명을 구할 퍼즐 모드입니다.</param>
    /// <param name="modeFolder">스테이지 모드 폴더명입니다.</param>
    /// <returns>지원하는 퍼즐 타입인지 여부입니다.</returns>
    public static bool TryGetModeFolder(PuzzleType puzzleType, out string modeFolder)
    {
        modeFolder = null;

        switch (puzzleType)
        {
            case PuzzleType.ThreeMatch:
                modeFolder = "ThreeMatch";
                return true;
            case PuzzleType.Link:
                modeFolder = "Link";
                return true;
            case PuzzleType.TapMatch:
                modeFolder = "TapMatch";
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 퍼즐 타입에 대응하는 스테이지 폴더명을 반환합니다.
    /// </summary>
    /// <param name="puzzleType">폴더명을 구할 퍼즐 모드입니다.</param>
    /// <returns>스테이지 모드 폴더명입니다.</returns>
    public static string GetModeFolder(PuzzleType puzzleType)
    {
        if (TryGetModeFolder(puzzleType, out string modeFolder))
        {
            return modeFolder;
        }

        return "Unknown";
    }

    /// <summary>
    /// 스테이지 번호에 대응하는 파일명을 반환합니다.
    /// </summary>
    /// <param name="stageId">스테이지 번호입니다.</param>
    /// <returns>확장자를 제외한 스테이지 파일명입니다.</returns>
    public static string GetStageFileName(int stageId)
    {
        return $"Stage_{stageId:000}";
    }

    /// <summary>
    /// 스테이지 리소스 키에 대응하는 다운로드 파일 경로를 반환합니다.
    /// </summary>
    /// <param name="resourceKey">Resources 기준 스테이지 키입니다.</param>
    /// <returns>다운로드 스테이지 JSON 파일 경로입니다.</returns>
    public static string GetDownloadedPath(string resourceKey)
    {
        string normalizedKey = resourceKey.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(Application.persistentDataPath, normalizedKey + ".json");
    }
}

