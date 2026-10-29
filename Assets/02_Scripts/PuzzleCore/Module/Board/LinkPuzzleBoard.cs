using System;
using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 같은 색상의 블럭을 3개 이상 선으로 연결하여 매치하는 퍼즐의 보드 모델입니다.
    /// (Hexagon 및 Quadrangle 모두 지원 가능하도록 설계됨)
    /// </summary>
    public class LinkPuzzleBoard : IPuzzleBoard
    {
        public BoardState State { get; private set; } = BoardState.Waiting;
        public PuzzleRandom Random { get; private set; }
        public ObjectiveManager Objective { get; private set; }
        public Dictionary<GridPos, PuzzleCell> Cells { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public Action<string> OnLog { get; set; }

        private PuzzleBlockFactory _blockFactory;
        
        /// <summary> 현재 유저가 드래그하여 연결 중인 블럭들의 좌표 리스트 </summary>
        private List<GridPos> _linkPath = new List<GridPos>();
        
        private List<BoardViewAction> _views = new List<BoardViewAction>();
        private List<InputRecord> _recordedInputs = new List<InputRecord>();
        private List<InputEndRecord> _recordedInputEnds = new List<InputEndRecord>();
        private ulong _frameCount;
        /// <summary> 좌표 입력과 종료 입력을 수신 순서대로 보관합니다. null은 입력 종료입니다. </summary>
        private readonly Queue<GridPos?> _pendingInputs = new Queue<GridPos?>();
        /// <summary> 좌표 입력과 종료 입력에 공통으로 부여하는 기록 순서입니다. </summary>
        private ulong _inputSequence;
        internal GameSpec gameSpec;
        private uint _currentOrderIndex = 0;

        private void Log(string message) => OnLog?.Invoke(message);

        public void Initialize(GameSpec spec)
        {
            _blockFactory = new PuzzleBlockFactory();
            _linkPath = new List<GridPos>();
            Cells = new Dictionary<GridPos, PuzzleCell>();
            _views = new List<BoardViewAction>();
            _recordedInputs = new List<InputRecord>();
            _recordedInputEnds = new List<InputEndRecord>();
            _frameCount = 0;
            _pendingInputs.Clear();
            _inputSequence = 0;
            _currentOrderIndex = 0;
            gameSpec = spec;
            Random = new PuzzleRandom(spec.randomSeed);
            Objective = new ObjectiveManager(gameSpec?.rule.objectives, gameSpec?.rule.timeLimit ?? 0, spec.mode);

            if (gameSpec?.stageData != null)
            {
                Width = gameSpec.stageData.stage_width;
                Height = gameSpec.stageData.stage_height;
                foreach (var cellData in gameSpec.stageData.cells)
                {
                    GridPos pos = new GridPos(cellData.x, cellData.y);
                    PuzzleCell cell = new PuzzleCell(pos)
                    {
                        CellType = (CellType)cellData.cell_type
                    };

                    if (cell.CellType == CellType.Generator && cellData.generator_block_ids != null)
                    {
                        cell.generatorBlockIds.AddRange(cellData.generator_block_ids);
                    }

                    if (!string.IsNullOrEmpty(cellData.block_id))
                    {
                        BlockData bData = gameSpec.GetBlock(cellData.block_id);
                        if (bData != null)
                        {
                            cell.Block = _blockFactory.Create(bData);
                        }
                    }
                    Cells[pos] = cell;
                }
            }

            State = BoardState.Waiting;
        }

        /// <summary> 좌표 입력을 다음 고정 틱의 처리 대기열에 추가합니다. </summary>
        /// <param name="input">입력한 셀 좌표</param>
        public void Input(GridPos input)
        {
            if (State != BoardState.Finish)
            {
                _pendingInputs.Enqueue(input);
            }
        }

        /// <summary> 입력 종료를 다음 고정 틱의 처리 대기열에 추가합니다. </summary>
        /// <returns>종료 이벤트가 대기열에 등록되었는지 여부입니다. 매칭 결과가 아닙니다.</returns>
        public bool InputEnd()
        {
            if (State == BoardState.Finish)
            {
                return false;
            }
            _pendingInputs.Enqueue(null);
            return true;
        }

        /// <summary> 고정 틱에서 좌표 입력을 판정합니다. </summary>
        /// <param name="input">입력한 셀 좌표</param>
        private void ProcessInput(GridPos input)
        {
            if (State != BoardState.Waiting) return;

            var targetCell = GetCell(input);
            if (targetCell?.Block == null) return;
            if (targetCell.CellType != CellType.Normal && targetCell.CellType != CellType.Generator) return;

            _recordedInputs.Add(new InputRecord(_frameCount, input, _inputSequence++));

            // 1. 첫 번째 선택
            if (_linkPath.Count == 0)
            {
                if (targetCell.Block.GetInputType().HasFlag(InputType.Link))
                {
                    _linkPath.Add(input);
                    targetCell.Block.SetState(BlockState.Selected);
                }
                return;
            }

            // 2. 이미 경로에 존재하는 블럭인지 확인
            if (_linkPath.Contains(input))
            {
                // 되돌리기(Backtracking): 직전 노드로 마우스를 되돌렸을 경우
                if (_linkPath.Count > 1 && _linkPath[_linkPath.Count - 2] == input)
                {
                    GridPos lastPos = _linkPath[_linkPath.Count - 1];
                    var lastCell = GetCell(lastPos);
                    lastCell?.Block?.SetState(BlockState.Idle);
                    _linkPath.RemoveAt(_linkPath.Count - 1);
                }
                return;
            }

            // 3. 새 노드 연결 시도
            GridPos currentPos = _linkPath[_linkPath.Count - 1];
            var currentCell = GetCell(currentPos);
            if (currentCell?.Block == null)
            {
                Log($"[LinkPuzzleBoard] 링크 경로의 마지막 셀({currentPos.X},{currentPos.Y})에 블럭이 없습니다.");
                return;
            }

            // 색상(ID) 동일 여부 체크
            if (currentCell.Block.GetBlockId() != targetCell.Block.GetBlockId()) return;

            // 인접 여부 체크 (육각형 or 사각형)
            bool isAdjacent = false;
            if (gameSpec.rule.boardShape == BoardShape.Hexagon)
            {
                isAdjacent = GridPos.IsAdjacentHexagon(currentPos, input);
            }
            else
            {
                isAdjacent = GridPos.IsAdjacentSquare(currentPos, input);
            }

            if (!isAdjacent) return;

            // 블럭 능력 체크 (링크 가능 여부는 데이터 inputType로 판정)
            if (targetCell.Block.GetInputType().HasFlag(InputType.Link))
            {
                _linkPath.Add(input);
                targetCell.Block.SetState(BlockState.Selected);
            }
        }

        /// <summary> 고정 틱에서 입력 종료와 매칭 여부를 판정합니다. </summary>
        private bool ProcessInputEnd()
        {
            _recordedInputEnds.Add(new InputEndRecord(_frameCount, _inputSequence++));

            // 이미 확정된 매칭 경로는 같은 틱에 추가로 들어온 종료 입력으로 취소하지 않습니다.
            if (State != BoardState.Waiting)
            {
                return false;
            }

            if (_linkPath.Count == 0)
            {
                return false;
            }

            if (_linkPath.Count >= 3)
            {
                // 3개 이상 연결 시 파괴 페이즈로 전환
                State = BoardState.Matching;
                return true;
            }
            else
            {
                // 연결 실패 시 초기화
                ClearPath();
                return false;
            }
        }

        private void ClearPath()
        {
            foreach (var pos in _linkPath)
            {
                var cell = GetCell(pos);
                cell?.Block?.SetState(BlockState.Idle);
            }
            _linkPath.Clear();
        }

        /// <summary> 고정 틱에서 셀과 보드 상태를 갱신합니다. </summary>
        private void UpdateState()
        {
            foreach (var cell in Cells.Values)
            {
                cell.Update(this);
            }

            if (Objective != null && Objective.ShouldFinish())
            {
                State = BoardState.Finish;
                return;
            }

            switch (State)
            {
                case BoardState.Waiting:
                    // 입력 대기 중에는 보드 상태를 변경하지 않음 (Input 로직에서 _linkPath가 갱신됨)
                    break;

                case BoardState.Matching:
                    if (ProcessMatching())
                    {
                        State = BoardState.Falling;
                    }
                    else
                    {
                        State = BoardState.Waiting;
                        _currentOrderIndex = 0;
                        ClearPath();
                    }
                    break;

                case BoardState.Falling:
                    ProcessFallingAndFilling();
                    // 링크 게임은 자동 매칭이 없으므로 낙하/보충 후 바로 대기 상태로 전환
                    State = BoardState.Waiting;
                    _currentOrderIndex = 0;
                    break;

                case BoardState.Filling:
                    State = BoardState.Waiting;
                    break;
            }
        }

        /// <summary> 입력 판정, 상태 전환, 타이머 갱신을 한 논리 틱에서 처리합니다. </summary>
        public void FixedUpdate()
        {
            if (State == BoardState.Finish)
            {
                _pendingInputs.Clear();
                return;
            }

            // 첫 틱은 0번이며, 입력을 기록한 틱의 상태 전환을 마친 뒤 프레임을 증가시킵니다.
            while (_pendingInputs.Count > 0)
            {
                GridPos? input = _pendingInputs.Dequeue();
                if (input.HasValue)
                {
                    ProcessInput(input.Value);
                }
                else
                {
                    ProcessInputEnd();
                }
            }
            Objective?.UpdateTimer(State == BoardState.Waiting);
            UpdateState();
            _frameCount++;
        }

        private bool ProcessMatching()
        {
            if (_linkPath == null || _linkPath.Count < 3) return false;

            Objective?.OnMatchEvent();
            uint burstOrder = _currentOrderIndex++;

            foreach (var pos in _linkPath)
            {
                var cell = GetCell(pos);
                if (cell?.Block != null)
                {
                    Block destroyed = cell.Block;
                    destroyed.SetState(BlockState.Matched);
                    Objective.OnBlockDestroyed(destroyed.GetBlockId());
                    cell.Block = null;

                    AddView(new BoardViewAction
                    {
                        type = ViewType.Destroy,
                        frame = (uint)_frameCount,
                        position = pos
                    }, burstOrder);

                    // 파괴된 블럭에 부착된 기믹 발화 (폭탄 등)
                    destroyed.FireDestroyed(this, pos);
                }
            }

            _linkPath.Clear();
            return true;
        }

        private bool ProcessFallingAndFilling()
        {
            bool anyChanged = false;
            uint fallOrder = _currentOrderIndex++;

            for (int x = 0; x < Width; x++)
            {
                // Write Index 방식의 수직 낙하
                int writeY = 0;
                for (int y = 0; y < Height; y++)
                {
                    var cell = GetCell(new GridPos(x, y));
                    if (cell == null)
                    {
                        // 누락된 셀은 낙하 경계로 취급하여 그 위 블럭이 기존 블럭을 덮어쓰지 않게 합니다.
                        writeY = y + 1;
                        continue;
                    }

                    if (cell.CellType == CellType.Close || cell.CellType == CellType.Lock)
                    {
                        writeY = y + 1;
                        continue;
                    }

                    if (cell.Block != null)
                    {
                        if (writeY != y)
                        {
                            var targetCell = GetCell(new GridPos(x, writeY));
                            if (targetCell != null)
                            {
                                Block movingBlock = cell.Block;
                                cell.Block = null;
                                targetCell.Block = movingBlock;
                                targetCell.Block.SetState(BlockState.Falling);

                                AddView(new BoardViewAction
                                {
                                    type = ViewType.Fall,
                                    frame = (uint)_frameCount,
                                    position = new GridPos(x, y),
                                    targetPosition = new GridPos(x, writeY)
                                }, fallOrder);

                                anyChanged = true;
                            }
                        }
                        writeY++;
                    }
                }

                // 빈 공간에 새로운 블럭 보충
                PuzzleCell generator = null;
                for (int y = Height - 1; y >= 0; y--)
                {
                    var c = GetCell(new GridPos(x, y));
                    if (c?.CellType == CellType.Generator) { generator = c; break; }
                }

                if (generator != null)
                {
                    int spawnSeq = 0;
                    for (int y = 0; y < Height; y++)
                    {
                        var cell = GetCell(new GridPos(x, y));
                        if (cell != null && cell.Block == null && 
                            (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator))
                        {
                            cell.Block = generator.GenerateBlock(gameSpec, Random, _blockFactory);
                            if (cell.Block != null)
                            {
                                cell.Block.SetState(BlockState.Falling);
                                GridPos spawnPos = new GridPos(x, Height + spawnSeq);
                                spawnSeq++;

                                AddView(new BoardViewAction
                                {
                                    type = ViewType.CreateAndFall,
                                    frame = (uint)_frameCount,
                                    position = spawnPos,
                                    targetPosition = new GridPos(x, y),
                                    blockData = cell.Block
                                }, fallOrder);

                                anyChanged = true;
                            }
                        }
                    }
                }
            }

            if (!anyChanged) _currentOrderIndex--;
            return anyChanged;
        }

        public void Pause(bool pause) { }

        public List<InputRecord> GetRecordedInputs() => new List<InputRecord>(_recordedInputs);
        public List<InputEndRecord> GetRecordedInputEnds() => new List<InputEndRecord>(_recordedInputEnds);

        public List<BoardViewAction> FetchActions()
        {
            // LINQ 대신 List.Sort로 GC 할당 방지
            _views.Sort((a, b) =>
            {
                int cmp = a.frame.CompareTo(b.frame);
                return cmp != 0 ? cmp : a.orderIndex.CompareTo(b.orderIndex);
            });
            // 리스트 복사 대신 참조 스왑으로 GC 할당 방지
            var res = _views;
            _views = new List<BoardViewAction>();
            return res;
        }

        public void AddView(BoardViewAction view, uint? customOrder = null)
        {
            // 연출 프레임/순서는 큐 소유자(보드)가 스탬프한다. (Model의 연출 데이터 책임 분리)
            view.frame = (uint)_frameCount;
            view.orderIndex = customOrder ?? _currentOrderIndex++;
            _views.Add(view);
        }

        public PuzzleCell GetCell(GridPos pos) => Cells.TryGetValue(pos, out var c) ? c : null;
        
        /// <summary>
        /// 뷰(View)에서 LineRenderer를 그리기 위해 현재 연결된 경로를 반환합니다.
        /// 원본 리스트의 읽기 전용 참조를 반환하여 매 프레임 새 리스트 할당을 방지합니다.
        /// 반환된 리스트를 외부에서 수정하지 않아야 합니다.
        /// </summary>
        public IReadOnlyList<GridPos> GetCurrentLinkPath()
        {
            return _linkPath;
        }
    }
}