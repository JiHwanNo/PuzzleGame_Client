using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 퍼즐 보드의 한 칸(Cell)을 구성하는 컨테이너 클래스입니다.
    /// 바닥면(Panel) 정보와 그 위에 놓인 물체(Block) 정보를 관리합니다.
    /// </summary>
    public class PuzzleCell
    {
        /// <summary> 보드 내에서 이 셀이 위치한 그리드 좌표 </summary>
        public GridPos Position { get; }
        
        /// <summary> 이 셀의 종류 (일반, 생성기, 장애물 등) </summary>
        public CellType CellType { get; set; }

        /// <summary> 블럭이 놓이고 매치/낙하/데미지 대상이 되는 "플레이 가능" 셀인지 여부 (일반 또는 생성기) </summary>
        public bool IsPlayable
        {
            get { return CellType == CellType.Normal || CellType == CellType.Generator; }
        }

        /// <summary> 이 셀 위에 놓인 블럭 객체. null이면 빈 공간입니다. </summary>
        public Block Block { get; set; }
        
        /// <summary> 이 셀의 바닥판(Panel) 객체 </summary>
        public PuzzlePanel Panel { get; set; }

        /// <summary> 연출이나 특정 로직으로 인해 셀이 잠겼는지 여부 </summary>
        private bool _isLocked;
        /// <summary> 잠금 상태 프로퍼티 </summary>
        public bool IsLocked
        {
            get => _isLocked;
            set => _isLocked = value;
        }

        /// <summary> 이 셀이 생성기(Generator)일 경우, 생성 가능한 블럭 ID 리스트 </summary>
        public List<string> generatorBlockIds = new List<string>();

        /// <summary>
        /// 새로운 셀을 지정된 위치에 생성합니다.
        /// </summary>
        /// <param name="position">이 셀의 그리드 좌표</param>
        public PuzzleCell(GridPos position)
        {
            Position = position;
            CellType = CellType.Normal; 
            Block = null;
            Panel = null;
            _isLocked = false;
        }

        /// <summary>
        /// 생성기 셀인 경우, 설정된 목록 중 하나를 랜덤하게 골라 블럭을 생성합니다.
        /// </summary>
        /// <param name="spec">게임 사양서 (블럭 데이터 참조용)</param>
        /// <param name="random">공용 난수 객체</param>
        /// <param name="factory">블럭 생성 팩토리</param>
        /// <returns>생성된 블럭 객체</returns>
        public Block GenerateBlock(GameSpec spec, PuzzleRandom random, PuzzleBlockFactory factory)
        {
            if (CellType != CellType.Generator || generatorBlockIds.Count == 0 || factory == null)
            {
                return null;
            }

            // 리스트 중 랜덤하게 하나 선택 (원본 로직 보존)
            int index = random.Next(0, generatorBlockIds.Count);
            string blockId = generatorBlockIds[index];

            BlockData bData = spec.GetBlock(blockId);
            if (bData != null)
            {
                return factory.Create(bData);
            }

            return null;
        }

        /// <summary>
        /// 셀 내부의 블럭과 패널 상태를 업데이트합니다.
        /// </summary>
        /// <param name="board">현재 보드 객체</param>
        public void Update(IPuzzleBoard board)
        {
            if (Block != null)
            {
                Block.Update(board, Position);
            }

            if (Panel != null)
            {
                Panel.Update();
            }
        }
    }
}
