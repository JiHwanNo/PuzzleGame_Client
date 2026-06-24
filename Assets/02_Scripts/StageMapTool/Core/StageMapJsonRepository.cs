using System.Collections.Generic;
using System.IO;
using Puzzle.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 스테이지 맵 툴에서 StageData JSON을 로드하고 저장합니다.
/// </summary>
public class StageMapJsonRepository
{
    /// <summary> 새 스테이지 기본 가로 크기입니다. </summary>
    private const int DefaultWidth = 9;

    /// <summary> 새 스테이지 기본 세로 크기입니다. </summary>
    private const int DefaultHeight = 9;

    /// <summary>
    /// 스테이지 JSON을 로드하거나 없으면 새 스테이지를 생성합니다.
    /// </summary>
    /// <param name="puzzleType">로드할 퍼즐 모드입니다.</param>
    /// <param name="stageId">로드할 스테이지 번호입니다.</param>
    /// <returns>로드 또는 생성된 스테이지 데이터입니다.</returns>
    public StageData LoadOrCreate(PuzzleType puzzleType, int stageId)
    {
        if (StageStorage.TryLoadStageJson(puzzleType, stageId, out string json))
        {
            StageData stageData = JsonUtility.FromJson<StageData>(json);
            if (stageData != null)
            {
                EnsureCellDefaults(stageData);
                return stageData;
            }
        }

        return CreateEmptyStage(stageId, DefaultWidth, DefaultHeight);
    }

    /// <summary>
    /// 기본 크기(9×9)의 빈 스테이지를 생성합니다.
    /// </summary>
    /// <param name="stageId">생성할 스테이지 번호입니다.</param>
    /// <returns>셀이 비어 있는 스테이지 데이터입니다.</returns>
    public StageData CreateEmptyStage(int stageId)
    {
        return CreateEmptyStage(stageId, DefaultWidth, DefaultHeight);
    }

    /// <summary>
    /// 셀이 모두 비어 있는("+") 새 스테이지 데이터를 생성합니다.
    /// 기획자가 빈 격자에서 셀을 직접 찍어 만드는 흐름의 시작 상태입니다.
    /// </summary>
    /// <param name="stageId">생성할 스테이지 번호입니다.</param>
    /// <param name="width">생성할 보드 가로 크기입니다.</param>
    /// <param name="height">생성할 보드 세로 크기입니다.</param>
    /// <returns>셀이 비어 있는 스테이지 데이터입니다.</returns>
    public StageData CreateEmptyStage(int stageId, int width, int height)
    {
        return new StageData
        {
            stage_id = stageId,
            stage_width = width,
            stage_height = height,
            cells = new List<CellData>()
        };
    }

    /// <summary>
    /// 스테이지 데이터를 Resources 기본 파일 경로에 저장합니다.
    /// </summary>
    /// <param name="puzzleType">저장할 퍼즐 모드입니다.</param>
    /// <param name="stageId">저장할 스테이지 번호입니다.</param>
    /// <param name="stageData">저장할 스테이지 데이터입니다.</param>
    /// <returns>저장 성공 여부입니다.</returns>
    public bool SaveToResources(PuzzleType puzzleType, int stageId, StageData stageData)
    {
        if (!StageStorage.TryGetModeFolder(puzzleType, out string modeFolder))
        {
            Debug.LogError($"[StageMapJsonRepository] 지원하지 않는 퍼즐 타입입니다. puzzleType: {puzzleType}");
            return false;
        }

        string path = Path.Combine(Application.dataPath, "Resources", "Stage", modeFolder, StageStorage.GetStageFileName(stageId) + ".json");
        bool saved = SaveToPath(path, stageId, stageData);
#if UNITY_EDITOR
        if (saved)
        {
            AssetDatabase.Refresh();
        }
#endif
        return saved;
    }

    /// <summary>
    /// 스테이지 데이터를 다운로드 우선 경로에 저장합니다.
    /// </summary>
    /// <param name="puzzleType">저장할 퍼즐 모드입니다.</param>
    /// <param name="stageId">저장할 스테이지 번호입니다.</param>
    /// <param name="stageData">저장할 스테이지 데이터입니다.</param>
    /// <returns>저장 성공 여부입니다.</returns>
    public bool SaveToDownloaded(PuzzleType puzzleType, int stageId, StageData stageData)
    {
        string resourceKey = StageStorage.GetResourceKey(puzzleType, stageId);
        if (string.IsNullOrEmpty(resourceKey))
        {
            return false;
        }

        return SaveToPath(StageStorage.GetDownloadedPath(resourceKey), stageId, stageData);
    }

    /// <summary>
    /// 새 기본 스테이지 데이터를 생성합니다.
    /// </summary>
    /// <param name="stageId">생성할 스테이지 번호입니다.</param>
    /// <param name="width">생성할 보드 가로 크기입니다.</param>
    /// <param name="height">생성할 보드 세로 크기입니다.</param>
    /// <returns>생성된 스테이지 데이터입니다.</returns>
    public StageData CreateDefaultStage(int stageId, int width, int height)
    {
        StageData stageData = new StageData
        {
            stage_id = stageId,
            stage_width = width,
            stage_height = height,
            cells = new List<CellData>(width * height)
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                stageData.cells.Add(new CellData
                {
                    x = x,
                    y = y,
                    block_id = null,
                    panel_id = 0,
                    cell_type = (int)(y == height - 1 ? CellType.Generator : CellType.Normal),
                    generator_block_ids = new List<string>()
                });
            }
        }

        return stageData;
    }

    /// <summary>
    /// 스테이지 데이터를 지정된 파일 경로에 저장합니다.
    /// </summary>
    /// <param name="path">저장할 파일 경로입니다.</param>
    /// <param name="stageId">저장 기준 스테이지 번호입니다.</param>
    /// <param name="stageData">저장할 스테이지 데이터입니다.</param>
    /// <returns>저장 성공 여부입니다.</returns>
    private bool SaveToPath(string path, int stageId, StageData stageData)
    {
        if (stageData == null)
        {
            Debug.LogError("[StageMapJsonRepository] 저장할 스테이지 데이터가 없습니다.");
            return false;
        }

        stageData.stage_id = stageId;
        EnsureCellDefaults(stageData);
        SortCells(stageData);

        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(stageData, true);
        File.WriteAllText(path, json);
        Debug.Log($"[StageMapJsonRepository] 스테이지 저장 완료: {path}");
        return true;
    }

    /// <summary>
    /// 셀 목록의 null 필드를 기본값으로 보정합니다.
    /// </summary>
    /// <param name="stageData">보정할 스테이지 데이터입니다.</param>
    private void EnsureCellDefaults(StageData stageData)
    {
        if (stageData.cells == null)
        {
            stageData.cells = new List<CellData>();
            return;
        }

        for (int i = 0; i < stageData.cells.Count; i++)
        {
            CellData cell = stageData.cells[i];
            if (cell != null && cell.generator_block_ids == null)
            {
                cell.generator_block_ids = new List<string>();
            }
        }
    }

    /// <summary>
    /// JSON diff 안정성을 위해 셀 목록을 좌표 순서로 정렬합니다.
    /// </summary>
    /// <param name="stageData">정렬할 스테이지 데이터입니다.</param>
    private void SortCells(StageData stageData)
    {
        if (stageData.cells == null)
        {
            return;
        }

        stageData.cells.Sort(CompareCell);
    }

    /// <summary>
    /// 셀 좌표를 y, x 순서로 비교합니다.
    /// </summary>
    /// <param name="left">왼쪽 셀입니다.</param>
    /// <param name="right">오른쪽 셀입니다.</param>
    /// <returns>정렬 비교 결과입니다.</returns>
    private int CompareCell(CellData left, CellData right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        int yCompare = left.y.CompareTo(right.y);
        if (yCompare != 0)
        {
            return yCompare;
        }

        return left.x.CompareTo(right.x);
    }
}
