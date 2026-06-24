using System.Collections.Generic;
using Puzzle.Core;

/// <summary>
/// 저장 직전 스테이지 데이터를 "내용 기준"으로 정규화(trim)합니다.
/// 기획자가 빈 격자 어디에 셀을 찍든, 찍은 셀들의 바운딩 박스로 잘라 (0,0)부터 재매핑하고
/// 박스 내부의 빈 칸은 Close로 채워 stage_width/height를 내용 크기로 맞춥니다.
/// 인게임 PuzzleBoardView가 stage_width×stage_height를 중앙 정렬하므로, 결과 출력이 항상 화면 중앙에 옵니다.
/// </summary>
public class StageMapNormalizer
{
    /// <summary>
    /// 스테이지 데이터를 내용 바운딩 박스 기준으로 정규화한 새 데이터를 만듭니다.
    /// 원본은 변경하지 않으며(편집 좌표 보존), 찍힌 셀이 하나도 없으면 false를 반환합니다.
    /// </summary>
    /// <param name="source">정규화할 원본 스테이지 데이터입니다.</param>
    /// <param name="hexLayout">헥사(Even-Q) 배치 여부입니다. true면 X 시프트를 짝수로 내려 컬럼 패리티를 보존합니다.</param>
    /// <param name="normalized">정규화된 새 스테이지 데이터입니다. 실패 시 null입니다.</param>
    /// <returns>정규화 성공 여부입니다.</returns>
    public bool TryNormalize(StageData source, bool hexLayout, out StageData normalized)
    {
        normalized = null;
        if (source == null || source.cells == null || source.cells.Count == 0)
        {
            return false;
        }

        // 찍힌(빈 칸이 아닌) 셀만 정규화 대상으로 본다. Close 셀은 빈 칸과 동일하게 취급해 제외한다.
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        int contentCount = 0;

        for (int i = 0; i < source.cells.Count; i++)
        {
            CellData cell = source.cells[i];
            if (cell == null || cell.cell_type == (int)CellType.Close)
            {
                continue;
            }

            if (cell.x < minX)
            {
                minX = cell.x;
            }

            if (cell.y < minY)
            {
                minY = cell.y;
            }

            if (cell.x > maxX)
            {
                maxX = cell.x;
            }

            if (cell.y > maxY)
            {
                maxY = cell.y;
            }

            contentCount++;
        }

        if (contentCount == 0)
        {
            return false;
        }

        // 헥사(Even-Q) 배치에서는 컬럼 패리티(x%2)가 셀의 세로 오프셋과 인접성을 결정한다.
        // X를 홀수만큼 옮기면 모든 컬럼의 패리티가 뒤집혀 작성 의도와 다른 모양/연결로 저장되므로,
        // 헥사일 때는 시프트량을 짝수로 내려 패리티를 보존한다(맨 왼쪽 컬럼이 비는 만큼 폭이 1 늘 수 있음).
        // 사각 그리드(ThreeMatch/TapMatch)는 패리티 의존이 없어 minX 전체를 옮긴다.
        int shiftX = hexLayout ? (minX - (minX & 1)) : minX;
        int width = maxX - shiftX + 1;
        int height = maxY - minY + 1;

        // 바운딩 박스 크기의 격자를 만들고 찍힌 셀을 (0,0) 기준으로 재매핑해 덮어쓴다.
        CellData[,] grid = new CellData[width, height];
        for (int i = 0; i < source.cells.Count; i++)
        {
            CellData cell = source.cells[i];
            if (cell == null || cell.cell_type == (int)CellType.Close)
            {
                continue;
            }

            int nx = cell.x - shiftX;
            int ny = cell.y - minY;

            // 편집 모델(StageMapToolState)이 좌표당 셀 하나를 보장하므로 실제 중복은 발생하지 않는다.
            grid[nx, ny] = CloneCell(cell, nx, ny);
        }

        StageData result = new StageData
        {
            stage_id = source.stage_id,
            stage_width = width,
            stage_height = height,
            cells = new List<CellData>(width * height)
        };

        // 격자를 y, x 순서로 순회하며 빈 칸은 Close로 채운다(인게임이 요구하는 가득 찬 보드).
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                CellData cell = grid[x, y];
                if (cell == null)
                {
                    cell = CreateCloseCell(x, y);
                }

                result.cells.Add(cell);
            }
        }

        normalized = result;
        return true;
    }

    /// <summary>
    /// 원본 셀을 새 좌표로 깊은 복사합니다(생성 목록 리스트도 새로 복제).
    /// </summary>
    /// <param name="source">복사할 원본 셀입니다.</param>
    /// <param name="x">새 X 좌표입니다.</param>
    /// <param name="y">새 Y 좌표입니다.</param>
    /// <returns>복사된 셀 데이터입니다.</returns>
    private CellData CloneCell(CellData source, int x, int y)
    {
        List<string> generators = source.generator_block_ids != null
            ? new List<string>(source.generator_block_ids)
            : new List<string>();

        return new CellData
        {
            x = x,
            y = y,
            block_id = source.block_id,
            panel_id = source.panel_id,
            cell_type = source.cell_type,
            generator_block_ids = generators
        };
    }

    /// <summary>
    /// 바운딩 박스 내부의 빈 칸을 채울 Close 셀을 생성합니다.
    /// </summary>
    /// <param name="x">Close 셀의 X 좌표입니다.</param>
    /// <param name="y">Close 셀의 Y 좌표입니다.</param>
    /// <returns>생성된 Close 셀 데이터입니다.</returns>
    private CellData CreateCloseCell(int x, int y)
    {
        return new CellData
        {
            x = x,
            y = y,
            block_id = null,
            panel_id = 0,
            cell_type = (int)CellType.Close,
            generator_block_ids = new List<string>()
        };
    }
}
