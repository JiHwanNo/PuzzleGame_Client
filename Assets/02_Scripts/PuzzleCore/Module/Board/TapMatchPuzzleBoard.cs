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
            _currentOrderIndex = 0;
            gameSpec = spec;
            Random = new PuzzleRandom(spec.randomSeed);
            Objective = new ObjectiveManager(gameSpec?.rule.objectives, gameSpec?.rule.timeLimit ?? 0);

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

            // 탭 매치는 시작 시 별도의 매칭 체크 없이 대기 상태로 시작합니다.
            State = BoardState.Waiting;
        }

        public void Input(GridPos input)
        {
            if (State != BoardState.Waiting)
            {
                return;
            }

            if (_lastEnqueuedInput.HasValue && _lastEnqueuedInput.Value == input)
            {
                return;
            }

            _inputQueue.Enqueue(input);
            _lastEnqueuedInput = input;
            _recordedInputs.Add(new InputRecord(_frameCount, input));
            
            var cell = GetCell(input);
            if (cell?.Block != null)
            {
                // 클릭 피드백
                cell.Block.SetState(BlockState.Selected);
            }
        }

        public bool InputEnd()
        {
            _recordedInputEnds.Add(new InputEndRecord(_frameCount));

            if (State != BoardState.Waiting || _inputQueue.Count == 0)
            {
                foreach (var pos in _inputQueue)
                {
                    var c = GetCell(pos);
                    c?.Block?.SetState(BlockState.Idle);
                }
                _inputQueue.Clear();
                _lastEnqueuedInput = null;
                return false;
            }

            GridPos targetPos = _inputQueue.Dequeue();

            foreach (var pos in _inputQueue)
            {
                var c = GetCell(pos);
                c?.Block?.SetState(BlockState.Idle);
            }
            _inputQueue.Clear();
            _lastEnqueuedInput = null; // 탭 매치는 첫 클릭 위치만 사용

            var cell = GetCell(targetPos);
            if (cell?.Block == null)
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
                Objective?.OnMatchEvent(); // 콤보 증가 및 갱신

                uint burstOrder = _currentOrderIndex++;
                foreach (var pos in matched)
                {
                    var mCell = GetCell(pos);
                    if (mCell?.Block != null)
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
                        var nextCell = GetCell(next);
                        if (nextCell?.Block != null && nextCell.Block.GetBlockId() == targetId)
                        {
                            _connectedBuffer.Add(next);
                            _floodFillQueue.Enqueue(next);
                        }
                    }
                }
            }

            return _connectedBuffer;
        }

        public void Update()
        {
            foreach (var cell in Cells.Values)
            {
                cell.Update(this);
            }

            if (Objective != null && (Objective.IsAllObjectivesCleared() || Objective.IsTimeOver()))
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
                    _currentOrderIndex = 0;

                    foreach (var cell in Cells.Values)
                    {
                        cell.Block?.SetState(BlockState.Idle);
                    }

                    CheckAndShuffleIfNoMoves();
                    break;
            }
        }

        public void FixedUpdate()
        {
            _frameCount++;
            bool isWaiting = (State == BoardState.Waiting);
            Objective?.UpdateTimer(isWaiting);
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
                    if (cell == null) continue;

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

        private string GetBlockIdAt(GridPos pos)
        {
            var cell = GetCell(pos);
            bool isMatchable = cell?.Block != null && (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator);
            return isMatchable ? cell.Block.GetBlockId() : null;
        }

        private bool HasPossibleMoves()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    string id = GetBlockIdAt(new GridPos(x, y));
                    if (id == null) continue;

                    if (x < Width - 1 && id == GetBlockIdAt(new GridPos(x + 1, y))) return true;
                    if (y < Height - 1 && id == GetBlockIdAt(new GridPos(x, y + 1))) return true;
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
                    if (cell?.Block != null && (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator))
                    {
                        blocks.Add(cell.Block);
                        positions.Add(new GridPos(x, y));
                    }
                }
            }

            if (blocks.Count == 0) return;

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

            if (success)
            {
                uint shuffleOrder = _currentOrderIndex++;
                foreach (var pos in positions)
                {
                    var cell = GetCell(pos);
                    if (cell?.Block != null)
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
