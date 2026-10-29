using System;
using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 게임 전체의 사양과 데이터를 관리하는 클래스입니다.
    /// 스테이지 정보, 게임 규칙, 사용 가능한 블록 목록을 포함합니다.
    /// </summary>
    [Serializable]
    public class GameSpec
    {
        /// <summary> 초당 논리 틱 수입니다. Unity의 고정 간격은 0.02초로 설정합니다. </summary>
        public const int LogicFramesPerSecond = 50;
        /// <summary> 대전 한 판의 공통 제한 시간입니다. </summary>
        public const int BattleDurationSeconds = 60;
        /// <summary> 대전 한 판의 총 논리 틱 수입니다. </summary>
        public const int BattleFrameCount = LogicFramesPerSecond * BattleDurationSeconds;
        /// <summary> 게임 종료 규칙입니다. 기존 데이터의 기본값은 스테이지입니다. </summary>
        public GameMode mode;
        /// <summary> 현재 스테이지의 세부 구성 데이터 </summary>
        public StageData stageData;
        
        /// <summary> 게임 진행에 적용되는 규칙 데이터 </summary>
        public RuleData rule;
        
        /// <summary> 게임 내에서 사용 가능한 모든 블록의 정의 목록 </summary>
        public List<BlockData> blocks;

        /// <summary> 결정론적 리플레이를 위한 난수 시드 </summary>
        public int randomSeed;

        /// <summary> blockId → BlockData 빠른 조회용 캐시 (최초 GetBlock 호출 시 구축) </summary>
        [NonSerialized]
        private Dictionary<string, BlockData> _blockCache;

        /// <summary>
        /// 현재 설정된 게임 규칙 데이터를 반환합니다.
        /// </summary>
        /// <returns>적용 중인 RuleData 객체</returns>
        public RuleData GetRule()
        {
            return rule;
        }

        /// <summary>
        /// 특정 블록 ID에 해당하는 블록 정보를 검색하여 반환합니다.
        /// </summary>
        /// <param name="blockId">검색할 블록의 고유 식별자</param>
        /// <returns>블록 데이터 (없을 경우 null)</returns>
        public BlockData GetBlock(string blockId)
        {
            if (blocks == null)
            {
                return null;
            }

            if (_blockCache == null)
            {
                _blockCache = new Dictionary<string, BlockData>(blocks.Count);
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (blocks[i] != null && !_blockCache.ContainsKey(blocks[i].blockId))
                    {
                        _blockCache.Add(blocks[i].blockId, blocks[i]);
                    }
                }
            }

            return _blockCache.TryGetValue(blockId, out BlockData data) ? data : null;
        }
    }

    /// <summary>
    /// 스테이지의 전체적인 구조와 크기를 정의하는 데이터 클래스입니다.
    /// </summary>
    [Serializable]
    public class StageData
    {
        /// <summary> 스테이지의 고유 식별 번호 </summary>
        public int stage_id;
        
        /// <summary> 스테이지 보드의 가로 너비 (칸 수) </summary>
        public int stage_width;
        
        /// <summary> 스테이지 보드의 세로 높이 (칸 수) </summary>
        public int stage_height;
        
        /// <summary> 스테이지를 구성하는 개별 셀(Cell)들의 상세 데이터 목록 </summary>
        public List<CellData> cells;
    }

    /// <summary>
    /// 스테이지 보드 상의 특정 칸(Cell)에 대한 정보를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    public class CellData
    {
        /// <summary> 셀의 X 좌표 (열 위치) </summary>
        public int x;
        
        /// <summary> 셀의 Y 좌표 (행 위치) </summary>
        public int y;
        
        /// <summary> 해당 셀에 배치될 기본 블록의 ID </summary>
        public string block_id;
        
        /// <summary> 셀에 깔린 패널(바닥)의 유형 ID </summary>
        public int panel_id;
        
        /// <summary> 셀 자체의 특성이나 속성 (예: 빈 칸, 장애물 칸 등) </summary>
        public int cell_type;

        /// <summary> 생성기(Generator)일 경우 생성될 블럭들의 아이디 목록 </summary>
        public List<string> generator_block_ids;
    }
}
