using System;
using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 3매치(Three Match) 퍼즐 게임의 보드 상태와 핵심 로직을 관리하는 Model 클래스입니다.
    /// 조작 -> 매칭 -> 낙하 -> 채우기 -> 재매칭의 순차적 루프를 통해 안정적인 연쇄 반응을 보장합니다.
    /// </summary>
    public class ThreeMatchPuzzleBoard : IPuzzleBoard
    {
        /// <summary> 보드의 현재 논리적 상태 </summary>
        public BoardState State { get; private set; } = BoardState.Waiting;

        /// <summary> 게임 내 공용 난수 생성기 </summary>
        public PuzzleRandom Random { get; private set; }

        /// <summary> 스테이지 목표 관리자 </summary>
        public ObjectiveManager Objective { get; private set; }

        /// <summary> 좌표별 셀 데이터를 저장하는 딕셔너리 </summary>
        public Dictionary<GridPos, PuzzleCell> Cells { get; private set; }

        /// <summary> 보드의 가로 너비 </summary>
        public int Width { get; private set; }

        /// <summary> 보드의 세로 높이 </summary>
        public int Height { get; private set; }

        /// <summary> 보드 내부 로직에서 발생하는 로그를 외부로 전달합니다. </summary>
        public Action<string> OnLog { get; set; }

        /// <summary> 블럭을 생성하는 팩토리 인스턴스 </summary>
        private PuzzleBlockFactory _blockFactory;

        /// <summary> 현재 유저가 선택(클릭)한 블럭의 좌표 </summary>
        private GridPos? _selectedPos = null;

        /// <summary> 유저 입력 좌표 대기열 </summary>
        private Queue<GridPos> _inputQueue = new Queue<GridPos>();

        /// <summary> 마지막으로 큐에 추가된 입력 좌표 (Queue.Last() O(n) 방지) </summary>
        private GridPos? _lastEnqueuedInput = null;

        /// <summary> 화면에 요청할 시각적 연출 액션 리스트 </summary>
        private List<BoardViewAction> _views = new List<BoardViewAction>();

        /// <summary> 지금까지의 모든 유저 조작 기록 </summary>
        private List<InputRecord> _recordedInputs = new List<InputRecord>();

        /// <summary> 지금까지의 모든 입력 종료 기록 </summary>
        private List<InputEndRecord> _recordedInputEnds = new List<InputEndRecord>();

        /// <summary> 현재 보드 프레임 번호 (리플레이용) </summary>
        private ulong _frameCount;

        /// <summary> 현재 보드에 적용된 게임 사양서 </summary>
        internal GameSpec gameSpec;

        /// <summary> 연출의 시각적 순서를 정하는 인덱스 (프레임과 무관하게 증가) </summary>
        private uint _currentOrderIndex = 0;

        /// <summary> FindMatches() 재사용 HashSet (매 호출마다 할당 방지) </summary>
        private HashSet<GridPos> _matchBuffer = new HashSet<GridPos>();

        /// <summary> 한 번의 매치 해소/스왑 스텝에서 이미 데미지를 받은 칸 집합 (재사용, "한 스텝 = 칸당 최대 1대" 보장) </summary>
        private readonly HashSet<GridPos> _damageBuffer = new HashSet<GridPos>();

        /// <summary>
        /// 보드 내부 로직을 수행하며 로그를 전달합니다.
        /// </summary>
        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }

        /// <summary>
        /// 전달된 사양서를 바탕으로 보드를 초기화합니다.
        /// </summary>
        public void Initialize(GameSpec spec)
        {
            _blockFactory = new PuzzleBlockFactory();
            _selectedPos = null;
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

            // 시작 시 매칭 체크부터 수행
            State = BoardState.Matching;
        }

        public void Input(GridPos input)
        {
            if (State != BoardState.Waiting)
            {
                return;
            }

            // 중복 입력 방지 (마지막 입력과 같으면 무시)
            if (_lastEnqueuedInput.HasValue && _lastEnqueuedInput.Value == input)
            {
                return;
            }

            _inputQueue.Enqueue(input);
            _lastEnqueuedInput = input;
            _recordedInputs.Add(new InputRecord(_frameCount, input));

            // 개별 블럭 조작 로직 (스테이트 기반)
            var targetCell = GetCell(input);
            if (targetCell?.Block == null)
            {
                return;
            }

            if (_selectedPos.HasValue)
            {
                GridPos prev = _selectedPos.Value;
                if (prev == input)
                {
                    // 같은 블럭 클릭 시 선택 취소
                    targetCell.Block.SetState(BlockState.Idle);
                    _selectedPos = null;
                }
                else if (IsAdjacent(prev, input))
                {
                    // 인접 블럭 클릭 시 즉시 스왑 시도 (Tap-Tap Swap 지원)
                    ProcessSwapInput(prev, input);
                    _selectedPos = null;
                    _inputQueue.Clear();
                    _lastEnqueuedInput = null;
                }
                else
                {
                    // 인접하지 않은 블럭 클릭 시 이전 선택 취소 후 새로 선택
                    var prevCell = GetCell(prev);
                    prevCell?.Block?.SetState(BlockState.Idle);

                    _selectedPos = input;
                    targetCell.Block.SetState(BlockState.Selected);
                }
            }
            else
            {
                // 첫 선택
                _selectedPos = input;
                targetCell.Block.SetState(BlockState.Selected);
            }
        }

        public bool InputEnd()
        {
            _recordedInputEnds.Add(new InputEndRecord(_frameCount));

            if (State != BoardState.Waiting || _inputQueue.Count == 0)
            {
                _inputQueue.Clear();
                _lastEnqueuedInput = null;
                return false;
            }

            GridPos first = _inputQueue.Dequeue();
            GridPos last = first;
            while (_inputQueue.Count > 0)
            {
                GridPos current = _inputQueue.Dequeue();
                if (IsAdjacent(first, current))
                {
                    last = current;
                    break;
                }
            }
            _inputQueue.Clear();
            _lastEnqueuedInput = null;

            if (first != last)
            {
                ProcessSwapInput(first, last);
                _selectedPos = null; // 스왑 시도 후 선택 해제
            }
            return true;
        }

        private void ProcessSwapInput(GridPos first, GridPos second)
        {
            var cellA = GetCell(first);
            var cellB = GetCell(second);
            if (cellA?.Block == null || cellB?.Block == null)
            {
                Log($"[ThreeMatchBoard] 스왑 대상 셀에 블럭이 없습니다. first=({first.X},{first.Y}), second=({second.X},{second.Y})");
                return;
            }

            // 스왑 가능 여부는 블럭 데이터(inputType)로 판정한다.
            if (!cellA.Block.GetInputType().HasFlag(InputType.Swap) || !cellB.Block.GetInputType().HasFlag(InputType.Swap))
            {
                Log("[ThreeMatchBoard] 스왑 불가 블럭이 포함되어 스왑을 취소합니다.");
                return;
            }

            // 0. 특수 효과가 스왑을 소비하는지 먼저 확인한다. (무지개 폭탄 등: 매치 성립과 무관하게 발동)
            //    물리 스왑 전에 호출하므로 각 블럭은 자신의 현재 좌표를 기준으로 대상 좌표를 받는다.
            _damageBuffer.Clear();
            if (cellA.Block.FireSwapped(this, first, second, _damageBuffer)
                || (cellB.Block != null && cellB.Block.FireSwapped(this, second, first, _damageBuffer)))
            {
                // 스왑이 소비되어 블럭이 파괴되었으므로 낙하/보충 단계로 진입한다.
                State = BoardState.Falling;
                return;
            }

            // 1. 물리적 스왑
            cellA.Block.SetState(BlockState.Moving);
            cellB.Block.SetState(BlockState.Moving);
            SwapBlocks(first, second);

            // 2. 매칭 여부 확인
            if (FindMatches().Count > 0)
            {
                // 매칭 성공 -> 매칭 페이즈 진입
                State = BoardState.Matching;
            }
            else
            {
                // 매칭 실패 -> 원상복구
                Log("[ThreeMatchBoard] 매칭 실패. 원상복구.");
                SwapBlocks(first, second);

                // 복구 후 상태 초기화
                cellA.Block.SetState(BlockState.Idle);
                cellB.Block.SetState(BlockState.Idle);
            }
        }

        /// <summary>
        /// 보드의 논리 상태를 순차적으로 업데이트합니다.
        /// 모바일 환경의 가변 프레임(Variable FPS)에 대응하여 최대한 부드러운 연출과 입력을 처리합니다.
        /// </summary>
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

                case BoardState.Matching:
                    if (ProcessMatching())
                    {
                        State = BoardState.Falling;
                    }
                    else
                    {
                        State = BoardState.Waiting;
                        _currentOrderIndex = 0; 

                        foreach (var cell in Cells.Values)
                        {
                            cell.Block?.SetState(BlockState.Idle);
                        }

                        CheckAndShuffleIfNoMoves();
                    }
                    break;

                case BoardState.Falling:
                    ProcessFallingAndFilling();
                    State = BoardState.Matching;
                    break;

                case BoardState.Filling:
                    State = BoardState.Matching;
                    break;
            }
        }

        /// <summary>
        /// 고정 프레임 간격(Fixed FPS)으로 논리 프레임을 전진시킵니다.
        /// 리플레이 재현 시 기준점이 되는 시간축 역할을 하며, 결정론적 동작을 보장하기 위해 반드시 여기서만 프레임이 증가해야 합니다.
        /// </summary>
        public void FixedUpdate()
        {
            _frameCount++;
            bool isWaiting = (State == BoardState.Waiting);
            Objective?.UpdateTimer(isWaiting);
        }

        private bool HasEmptyCell()
        {
            foreach (var c in Cells.Values)
            {
                if ((c.CellType == CellType.Normal || c.CellType == CellType.Generator) && c.Block == null)
                {
                    return true;
                }
            }
            return false;
        }

        private bool ProcessMatching()
        {
            var matches = FindMatches();
            if (matches == null || matches.Count == 0)
            {
                return false;
            }

            Objective?.OnMatchEvent();

            // 이번 매치 해소 스텝의 피격 칸 집합 초기화 ("한 스텝 = 칸당 최대 1대")
            _damageBuffer.Clear();

            // 이번 매칭 시퀀스의 터지는 연출을 하나의 그룹으로 묶음
            uint burstOrder = _currentOrderIndex++;
            // 이번 스텝에 실제 파괴(빈 칸 생성)가 있었는지. 하나도 없으면 낙하 패스를 건너뛰어 무한 재매치를 막는다.
            bool anyDestroyed = false;
            foreach (var pos in matches)
            {
                var cell = GetCell(pos);
                if (cell?.Block == null)
                {
                    continue;
                }

                // 이번 스텝에 이미 피격된 칸(예: 같은 매치의 폭탄 여파)은 매치 데미지를 중복 적용하지 않는다.
                if (!_damageBuffer.Add(pos))
                {
                    continue;
                }

                Block block = cell.Block;

                // "한 번의 매치 = 한 대(HP 1 감소)". 면역이거나 HP가 남으면 이번 매치에선 파괴되지 않는다(다중 HP 매치 블럭 대비).
                if (!block.TakeDamage(DamageSource.Match))
                {
                    continue;
                }

                block.SetState(BlockState.Matched);
                Objective.OnBlockDestroyed(block.GetBlockId());
                cell.Block = null;

                AddView(new BoardViewAction
                {
                    type = ViewType.Destroy,
                    frame = (uint)_frameCount,
                    position = pos
                }, burstOrder);

                // 파괴된 블럭의 OnDestroyed 효과 발화 (폭탄 등). 스텝 집합을 전파해 연쇄 데미지도 중복 차단.
                block.FireDestroyed(this, pos, _damageBuffer);
                anyDestroyed = true;
            }

            // 매치 칸에 직교 인접한 칸에 인접 매치 데미지 적용 (과자 등 인접 파괴형 장애물)
            bool neighborDestroyed = DamageNeighborsOfMatches(matches);

            // 파괴가 전혀 없었으면(예: 모든 매치 블럭이 HP만 깎임) 낙하 단계로 진입하지 않는다.
            return anyDestroyed || neighborDestroyed;
        }

        /// <summary>
        /// 이번 매치에 직교(상하좌우) 인접한 칸의 블럭에 NeighborMatch 데미지를 가합니다.
        /// 해당 소스에 면역인 블럭(일반 블럭 등)은 영향받지 않습니다.
        /// 보드를 (x, y) 순서로 스캔하여 결정론적 순서로 처리합니다.
        /// </summary>
        /// <param name="matches">이번 시퀀스에 매치된 좌표 집합</param>
        /// <returns>인접 데미지로 블럭이 하나라도 파괴되었으면 true</returns>
        private bool DamageNeighborsOfMatches(HashSet<GridPos> matches)
        {
            if (matches == null || matches.Count == 0)
            {
                return false;
            }

            bool anyDestroyed = false;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    GridPos pos = new GridPos(x, y);
                    var cell = GetCell(pos);
                    if (cell?.Block == null)
                    {
                        continue;
                    }

                    // 매치 칸 자신은 이미 파괴 처리되었으므로 제외
                    if (matches.Contains(pos))
                    {
                        continue;
                    }

                    // 직교 인접 칸 중 하나라도 이번 매치에 포함되면 인접 매치 데미지 적용
                    if (matches.Contains(new GridPos(x + 1, y))
                        || matches.Contains(new GridPos(x - 1, y))
                        || matches.Contains(new GridPos(x, y + 1))
                        || matches.Contains(new GridPos(x, y - 1)))
                    {
                        if (BlockDamage.Damage(this, pos, DamageSource.NeighborMatch, _damageBuffer))
                        {
                            anyDestroyed = true;
                        }
                    }
                }
            }
            return anyDestroyed;
        }
        private HashSet<GridPos> FindMatches()
        {
            _matchBuffer.Clear();
            HashSet<GridPos> matches = _matchBuffer;

            // 가로 탐색
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width - 2; x++)
                {
                    string id = GetBlockIdAt(new GridPos(x, y));
                    if (id != null && id == GetBlockIdAt(new GridPos(x + 1, y)) && id == GetBlockIdAt(new GridPos(x + 2, y)))
                    {
                        matches.Add(new GridPos(x, y));
                        matches.Add(new GridPos(x + 1, y));
                        matches.Add(new GridPos(x + 2, y));

                        int nx = x + 3;
                        while (nx < Width && GetBlockIdAt(new GridPos(nx, y)) == id)
                        {
                            matches.Add(new GridPos(nx, y));
                            nx++;
                        }
                        x = nx - 1;
                    }
                }
            }

            // 세로 탐색
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height - 2; y++)
                {
                    string id = GetBlockIdAt(new GridPos(x, y));
                    if (id != null && id == GetBlockIdAt(new GridPos(x, y + 1)) && id == GetBlockIdAt(new GridPos(x, y + 2)))
                    {
                        matches.Add(new GridPos(x, y));
                        matches.Add(new GridPos(x, y + 1));
                        matches.Add(new GridPos(x, y + 2));

                        int ny = y + 3;
                        while (ny < Height && GetBlockIdAt(new GridPos(x, ny)) == id)
                        {
                            matches.Add(new GridPos(x, ny));
                            ny++;
                        }
                        y = ny - 1;
                    }
                }
            }
            return matches;
        }

        private string GetBlockIdAt(GridPos pos)
        {
            var cell = GetCell(pos);
            bool isMatchable = cell?.Block != null && (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator);
            return isMatchable ? cell.Block.GetBlockId() : null;
        }

        private bool ProcessFallingAndFilling()
        {
            bool anyChanged = false;
            // 낙하 연출 그룹 (터지는 연출과 분리된 다음 오더)
            uint fallOrder = _currentOrderIndex++;

            for (int x = 0; x < Width; x++)
            {
                // 1단계: 각 열에서 블럭들을 아래로 모으기 (Write Index 방식)
                int writeY = 0;
                for (int y = 0; y < Height; y++)
                {
                    var cell = GetCell(new GridPos(x, y));
                    if (cell == null) continue;

                    // 장애물 셀 처리 (장애물이 있으면 그 위로만 쌓이게 하려면 추가 로직이 필요하나, 일단 기본 흐름 구현)
                    if (cell.CellType == CellType.Close || cell.CellType == CellType.Lock)
                    {
                        writeY = y + 1;
                        continue;
                    }

                    if (cell.Block != null)
                    {
                        if (writeY != y)
                        {
                            // 블럭 이동 발생
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

                // 2단계: 남은 위쪽 빈칸들에 새 블럭 보충
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
                                
                                // 화면 밖에서 줄지어 내려오도록 설정
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
            // 프레임 우선, 프레임이 같다면 orderIndex가 작은 순서대로 정렬하여 반환
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

        /// <summary>
        /// 화면 연출용 액션을 추가합니다. 추가될 때마다 orderIndex가 자동으로 증가하여 순차적 연출을 보장합니다.
        /// </summary>
        public void AddView(BoardViewAction view, uint? customOrder = null)
        {
            // 연출 프레임/순서는 큐 소유자(보드)가 스탬프한다. (Model의 연출 데이터 책임 분리)
            view.frame = (uint)_frameCount;
            view.orderIndex = customOrder ?? _currentOrderIndex++;
            _views.Add(view);
        }
        


        public PuzzleCell GetCell(GridPos pos) => Cells.TryGetValue(pos, out var c) ? c : null;
        private bool IsAdjacent(GridPos a, GridPos b) => (Math.Abs(a.X - b.X) == 1 && a.Y == b.Y) || (Math.Abs(a.Y - b.Y) == 1 && a.X == b.X);

        // 스왑 동작을 동일한 orderIndex로 묶음
        private void SwapBlocks(GridPos a, GridPos b)
        {
            var ca = GetCell(a);
            var cb = GetCell(b);
            if (ca == null || cb == null) return;

            var t = ca.Block;
            ca.Block = cb.Block;
            cb.Block = t;

            uint order = _currentOrderIndex++; // 스왑용 단일 오더 발급
            AddView(new BoardViewAction { type = ViewType.Move, frame = (uint)_frameCount, position = a, targetPosition = b }, order);
            AddView(new BoardViewAction { type = ViewType.Move, frame = (uint)_frameCount, position = b, targetPosition = a }, order);
        }

        private void SwapBlocksDataOnly(GridPos a, GridPos b)
        {
            var ca = GetCell(a);
            var cb = GetCell(b);
            if (ca == null || cb == null) return;

            var t = ca.Block;
            ca.Block = cb.Block;
            cb.Block = t;
        }

        private bool HasPossibleMoves()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    GridPos current = new GridPos(x, y);
                    var cell = GetCell(current);
                    if (cell?.Block == null || (cell.CellType != CellType.Normal && cell.CellType != CellType.Generator)) continue;

                    // 오른쪽 스왑 체크
                    if (x < Width - 1)
                    {
                        GridPos right = new GridPos(x + 1, y);
                        var rCell = GetCell(right);
                        if (rCell?.Block != null && (rCell.CellType == CellType.Normal || rCell.CellType == CellType.Generator))
                        {
                            SwapBlocksDataOnly(current, right);
                            bool hasMatch = FindMatches().Count > 0;
                            SwapBlocksDataOnly(current, right);
                            if (hasMatch) return true;
                        }
                    }

                    // 위쪽 스왑 체크
                    if (y < Height - 1)
                    {
                        GridPos up = new GridPos(x, y + 1);
                        var uCell = GetCell(up);
                        if (uCell?.Block != null && (uCell.CellType == CellType.Normal || uCell.CellType == CellType.Generator))
                        {
                            SwapBlocksDataOnly(current, up);
                            bool hasMatch = FindMatches().Count > 0;
                            SwapBlocksDataOnly(current, up);
                            if (hasMatch) return true;
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

                // 당장 매칭은 없지만 다음 턴에 매칭 가능한 스왑은 있는 상태
                if (FindMatches().Count == 0 && HasPossibleMoves())
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
