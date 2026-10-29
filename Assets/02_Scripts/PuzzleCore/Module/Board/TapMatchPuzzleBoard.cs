using System;
using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 터치 매치(Tap to Blast) 방식의 보드 로직입니다.
    /// 스와이프 없이 터치 시 인접한 같은 색 블럭을 모두 찾아 2개 이상일 때 파괴합니다.
    /// </summary>
    public class TapMatchPuzzleBoard : IPuzzleBoard
    {
        public BoardState State { get; private set; } = BoardState.Waiting;
        public PuzzleRandom Random { get; private set; }
        public ObjectiveManager Objective { get; private set; }
        public Dictionary<GridPos, PuzzleCell> Cells { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private PuzzleBlockFactory _blockFactory;
        private Queue<GridPos> _inputQueue = new Queue<GridPos>();
        /// <summary> 마지막으로 큐에 추가된 입력 좌표 (Queue.Last() O(n) 방지) </summary>
        private GridPos? _lastEnqueuedInput = null;
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

        /// <summary> GetConnectedBlocks() 재사용 HashSet (매 호출마다 할당 방지) </summary>
        private HashSet<GridPos> _connectedBuffer = new HashSet<GridPos>();

        /// <summary> GetConnectedBlocks() 재사용 Queue (매 호출마다 할당 방지) </summary>
        private Queue<GridPos> _floodFillQueue = new Queue<GridPos>();

        /// <summary> GetConnectedBlocks() 인접 방향 배열 (매 호출마다 할당 방지) </summary>
        private static readonly GridPos[] _adjacentDirs = { GridPos.Up, GridPos.Down, GridPos.Left, GridPos.Right };

        public void Initialize(GameSpec spec)
        {
            _blockFactory = new PuzzleBlockFactory();
            _inputQueue = new Queue<GridPos>();
            _lastEnqueuedInput = null;
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
            Objective = new ObjectiveManager(gameSpec.rule.objectives, gameSpec.rule.timeLimit, spec.mode);

            if (gameSpec != null && gameSpec.stageData != null)
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

            // 탭 매치는 시작 시 별도의 매칭 체크 없이 대기 상태로 시작합니다.
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
            if (State != BoardState.Waiting)
            {
                return;
            }

            // 막힘/잠금 셀은 선택과 연결의 시작점으로 사용할 수 없습니다.
            if (GetBlockIdAt(input) == null)
            {
                return;
            }

            if (_lastEnqueuedInput.HasValue && _lastEnqueuedInput.Value == input)
            {
                return;
            }

            _inputQueue.Enqueue(input);
            _lastEnqueuedInput = input;
            _recordedInputs.Add(new InputRecord(_frameCount, input, _inputSequence++));
            
            var cell = GetCell(input);
            if (cell != null && cell.Block != null)
            {
                // 클릭 피드백
                cell.Block.SetState(BlockState.Selected);
            }
        }

        /// <summary> 고정 틱에서 입력 종료와 매칭 여부를 판정합니다. </summary>
        private bool ProcessInputEnd()
        {
            _recordedInputEnds.Add(new InputEndRecord(_frameCount, _inputSequence++));

            if (State != BoardState.Waiting || _inputQueue.Count == 0)
            {
                foreach (var pos in _inputQueue)
                {
                    var c = GetCell(pos);
                    if (c != null && c.Block != null)
                    {
                        c.Block.SetState(BlockState.Idle);
                    }
                }
                _inputQueue.Clear();
                _lastEnqueuedInput = null;
                return false;
            }

            GridPos targetPos = _inputQueue.Dequeue();

            foreach (var pos in _inputQueue)
            {
                var c = GetCell(pos);
                if (c != null && c.Block != null)
                {
                    c.Block.SetState(BlockState.Idle);
                }
            }
            _inputQueue.Clear();
            _lastEnqueuedInput = null; // 탭 매치는 첫 클릭 위치만 사용

            var cell = GetCell(targetPos);
            if (GetBlockIdAt(targetPos) == null)
            {
                return false;
            }

            // 터치 가능 여부는 블럭 데이터(inputType)로 판정한다.
            if (!cell.Block.GetInputType().HasFlag(InputType.Touch))
            {
                return false;
            }

            cell.Block.SetState(BlockState.Idle); // 선택 피드백 취소

            string targetId = cell.Block.GetBlockId();
            var matched = GetConnectedBlocks(targetPos, targetId);

            // 툰 블라스트 룰: 2개 이상일 때만 파괴 가능
            if (matched.Count >= 2)
            {
                if (Objective != null)
                {
                    Objective.OnMatchEvent(); // 콤보 증가 및 갱신
                }

                uint burstOrder = _currentOrderIndex++;
                foreach (var pos in matched)
                {
                    var mCell = GetCell(pos);
                    if (mCell != null && mCell.Block != null)
                    {
                        Block destroyed = mCell.Block;
                        destroyed.SetState(BlockState.Matched);
                        Objective.OnBlockDestroyed(destroyed.GetBlockId());
                        mCell.Block = null;

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

                State = BoardState.Falling;
                return true;
            }
            else
            {
                // 블럭 1개만 클릭했을 때는 파괴되지 않음
                return false;
            }
        }

        /// <summary>
        /// Flood Fill 알고리즘을 사용하여 인접한 동일 블럭들을 모두 찾습니다.
        /// 사전 할당된 컬렉션을 재사용하여 GC 할당을 방지합니다.
        /// </summary>
        private HashSet<GridPos> GetConnectedBlocks(GridPos start, string targetId)
        {
            _connectedBuffer.Clear();
            _floodFillQueue.Clear();

            _floodFillQueue.Enqueue(start);
            _connectedBuffer.Add(start);

            while (_floodFillQueue.Count > 0)
            {
                GridPos curr = _floodFillQueue.Dequeue();

                for (int i = 0; i < _adjacentDirs.Length; i++)
                {
                    GridPos next = curr + _adjacentDirs[i];
                    if (!_connectedBuffer.Contains(next))
                    {
                        // 막힘/잠금 셀을 연결 경로에서 제외하여 그 너머로도 탐색하지 않습니다.
                        if (GetBlockIdAt(next) == targetId)
                        {
                            _connectedBuffer.Add(next);
                            _floodFillQueue.Enqueue(next);
                        }
                    }
                }
            }

            return _connectedBuffer;
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
                    break;

                case BoardState.Falling:
                    ProcessFallingAndFilling();
                    
                    // 탭 매치에서는 연쇄 폭발(Cascade) 없이 바로 대기 상태로 돌아감
                    State = BoardState.Waiting;

                    foreach (var cell in Cells.Values)
                    {
                        if (cell.Block != null)
                        {
                            cell.Block.SetState(BlockState.Idle);
                        }
                    }

                    // 같은 논리 틱의 파괴·낙하보다 셔플이 뒤에 실행되도록 순서를 유지합니다.
                    CheckAndShuffleIfNoMoves();
                    _currentOrderIndex = 0;
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
            if (Objective != null)
            {
                Objective.UpdateTimer(State == BoardState.Waiting);
            }
            UpdateState();
            _frameCount++;
        }

        private void ProcessFallingAndFilling()
        {
            bool anyChanged = false;
            uint fallOrder = _currentOrderIndex++;

            for (int x = 0; x < Width; x++)
            {
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

                PuzzleCell generator = null;
                for (int y = Height - 1; y >= 0; y--)
                {
                    var c = GetCell(new GridPos(x, y));
                    if (c != null && c.CellType == CellType.Generator)
                    {
                        generator = c;
                        break;
                    }
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
        }

        public void Pause(bool pause) { }
        /// <summary> 기록한 좌표 입력 목록의 복사본을 반환합니다. </summary>
        public List<InputRecord> GetRecordedInputs() => new List<InputRecord>(_recordedInputs);
        /// <summary> 기록한 입력 종료 목록의 복사본을 반환합니다. </summary>
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
            if (customOrder.HasValue)
            {
                view.orderIndex = customOrder.Value;
            }
            else
            {
                view.orderIndex = _currentOrderIndex++;
            }
            _views.Add(view);
        }

        /// <summary> 지정한 좌표의 셀을 반환합니다. </summary>
        /// <param name="pos">조회할 그리드 좌표</param>
        public PuzzleCell GetCell(GridPos pos)
        {
            if (Cells.TryGetValue(pos, out var cell))
            {
                return cell;
            }
            return null;
        }

        private string GetBlockIdAt(GridPos pos)
        {
            var cell = GetCell(pos);
            bool isMatchable = (cell != null && cell.Block != null) && (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator);
            return isMatchable ? cell.Block.GetBlockId() : null;
        }

        private bool HasPossibleMoves()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    string id = GetBlockIdAt(new GridPos(x, y));
                    if (id == null || !GetCell(new GridPos(x, y)).Block.GetInputType().HasFlag(InputType.Touch))
                    {
                        continue;
                    }

                    // 실제 탭 판정처럼 시작 블럭만 Touch가 필요하며 연결 대상은 색상으로 판정합니다.
                    // 조작 가능한 블럭이 쌍의 오른쪽/위쪽에 있을 수도 있으므로 네 방향을 확인합니다.
                    for (int i = 0; i < _adjacentDirs.Length; i++)
                    {
                        if (id == GetBlockIdAt(new GridPos(x, y) + _adjacentDirs[i]))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void CheckAndShuffleIfNoMoves()
        {
            if (HasPossibleMoves()) return;

            List<Block> blocks = new List<Block>();
            List<GridPos> positions = new List<GridPos>();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cell = GetCell(new GridPos(x, y));
                    if ((cell != null && cell.Block != null) && (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator))
                    {
                        blocks.Add(cell.Block);
                        positions.Add(new GridPos(x, y));
                    }
                }
            }

            if (blocks.Count == 0) return;

            // 실패 시 원래 블럭 참조와 배치를 그대로 복원하여 뷰와 일치시킵니다.
            List<Block> originalBlocks = new List<Block>(blocks);
            int maxAttempts = 100;
            bool success = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                for (int i = blocks.Count - 1; i > 0; i--)
                {
                    int j = Random.Next(0, i + 1);
                    var temp = blocks[i];
                    blocks[i] = blocks[j];
                    blocks[j] = temp;
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    GetCell(positions[i]).Block = blocks[i];
                }

                if (HasPossibleMoves())
                {
                    success = true;
                    break;
                }
            }

            if (!success)
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    GetCell(positions[i]).Block = originalBlocks[i];
                }
                return;
            }

            if (success)
            {
                uint shuffleOrder = _currentOrderIndex++;
                foreach (var pos in positions)
                {
                    var cell = GetCell(pos);
                    if (cell != null && cell.Block != null)
                    {
                        AddView(new BoardViewAction
                        {
                            type = ViewType.Create,
                            frame = (uint)_frameCount,
                            position = pos,
                            blockData = cell.Block
                        }, shuffleOrder);
                    }
                }
            }
        }
    }
}
