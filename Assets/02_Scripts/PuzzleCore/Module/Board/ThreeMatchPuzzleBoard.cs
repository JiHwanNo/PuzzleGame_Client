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
        /// <summary> 좌표 입력과 종료 입력을 수신 순서대로 보관합니다. null은 입력 종료입니다. </summary>
        private readonly Queue<GridPos?> _pendingInputs = new Queue<GridPos?>();
        /// <summary> 좌표 입력과 종료 입력에 공통으로 부여하는 기록 순서입니다. </summary>
        private ulong _inputSequence;
        /// <summary> 현재 매칭이 유저의 성공한 교환에서 직접 시작되었는지 여부입니다. </summary>
        private bool _pendingPlayerMatch;

        /// <summary> 현재 보드에 적용된 게임 사양서 </summary>
        internal GameSpec gameSpec;

        /// <summary> 연출의 시각적 순서를 정하는 인덱스 (프레임과 무관하게 증가) </summary>
        private uint _currentOrderIndex = 0;

        /// <summary> FindMatches() 재사용 HashSet (매 호출마다 할당 방지) </summary>
        private HashSet<GridPos> _matchBuffer = new HashSet<GridPos>();

        /// <summary>
        /// 보드 내부 로직을 수행하며 로그를 전달합니다.
        /// </summary>
        /// <param name="message">전달할 로그 메시지</param>
        private void Log(string message)
        {
            if (OnLog != null)
            {
                OnLog.Invoke(message);
            }
        }

        /// <summary>
        /// 전달된 사양서를 바탕으로 보드를 초기화합니다.
        /// </summary>
        /// <param name="spec">보드 초기화에 사용할 게임 사양</param>
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
            _pendingInputs.Clear();
            _inputSequence = 0;
            _pendingPlayerMatch = false;
            _currentOrderIndex = 0;
            gameSpec = spec;
            Random = new PuzzleRandom(spec.randomSeed);
            Objective = new ObjectiveManager(gameSpec.rule.objectives, gameSpec.rule.timeLimit, spec.mode);

            if (gameSpec != null && gameSpec.stageData != null)
            {
                Width = gameSpec.stageData.stage_width;
                Height = gameSpec.stageData.stage_height;
                foreach (CellData cellData in gameSpec.stageData.cells)
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

            // 초기 빈칸은 첫 고정 틱에서 낙하·생성을 처리한 뒤 매칭을 검사합니다.
            // 생성 불가능한 빈칸이 남아도 Falling을 반복하지 않습니다.
            State = HasEmptyCell() ? BoardState.Falling : BoardState.Matching;
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

            if (GetBlockIdAt(input) == null)
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
            _recordedInputs.Add(new InputRecord(_frameCount, input, _inputSequence++));

            // 개별 블럭 조작 로직 (스테이트 기반)
            PuzzleCell targetCell = GetCell(input);
            if (targetCell == null || targetCell.Block == null)
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
                    PuzzleCell prevCell = GetCell(prev);
                    if (prevCell != null && prevCell.Block != null)
                    {
                        prevCell.Block.SetState(BlockState.Idle);
                    }

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

        /// <summary> 고정 틱에서 입력 종료와 매칭 여부를 판정합니다. </summary>
        private bool ProcessInputEnd()
        {
            _recordedInputEnds.Add(new InputEndRecord(_frameCount, _inputSequence++));

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

        /// <summary> 선택을 해제한 뒤 두 셀의 교환 가능 여부와 매칭을 판정합니다. </summary>
        /// <param name="first">첫 번째 셀 좌표</param>
        /// <param name="second">두 번째 셀 좌표</param>
        private void ProcessSwapInput(GridPos first, GridPos second)
        {
            // 권한이나 셀 상태 때문에 조기 반환해도 이전 선택 표시를 남기지 않습니다.
            if (_selectedPos.HasValue)
            {
                PuzzleCell selectedCell = GetCell(_selectedPos.Value);
                if (selectedCell != null && selectedCell.Block != null)
                {
                    selectedCell.Block.SetState(BlockState.Idle);
                }
                _selectedPos = null;
            }
            PuzzleCell cellA = GetCell(first);
            PuzzleCell cellB = GetCell(second);
            if ((cellA == null || cellA.Block == null) || (cellB == null || cellB.Block == null))
            {
                Log($"[ThreeMatchBoard] 스왑 대상 셀에 블럭이 없습니다. first=({first.X},{first.Y}), second=({second.X},{second.Y})");
                return;
            }

            // 조작 중 셀 속성이 바뀌었어도 잠금/막힘 셀은 교환하지 않습니다.
            if (GetBlockIdAt(first) == null || GetBlockIdAt(second) == null)
            {
                return;
            }

            // 스왑 가능 여부는 블럭 데이터(inputType)로 판정한다.
            if (!cellA.Block.GetInputType().HasFlag(InputType.Swap) || !cellB.Block.GetInputType().HasFlag(InputType.Swap))
            {
                Log("[ThreeMatchBoard] 스왑 불가 블럭이 포함되어 스왑을 취소합니다.");
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
                _pendingPlayerMatch = true;
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
        /// 셀과 보드의 논리 상태를 순차적으로 업데이트합니다.
        /// 고정 틱에서만 호출하여 렌더링 프레임 수와 관계없이 동일한 판정을 수행합니다.
        /// </summary>
        private void UpdateState()
        {
            foreach (PuzzleCell cell in Cells.Values)
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

                case BoardState.Matching:
                    if (ProcessMatching())
                    {
                        State = BoardState.Falling;
                    }
                    else
                    {
                        State = BoardState.Waiting;
                        _currentOrderIndex = 0; 

                        foreach (PuzzleCell cell in Cells.Values)
                        {
                            if (cell.Block != null)
                            {
                                cell.Block.SetState(BlockState.Idle);
                            }
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
        /// 입력 판정, 상태 전환, 타이머 갱신을 한 논리 틱에서 처리합니다.
        /// 리플레이 재현 시 기준점이 되는 시간축 역할을 하며, 결정론적 동작을 보장하기 위해 반드시 여기서만 프레임이 증가해야 합니다.
        /// </summary>
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

        /// <summary> 일반 셀이나 생성기 셀에 빈칸이 있는지 확인합니다. </summary>
        private bool HasEmptyCell()
        {
            foreach (PuzzleCell c in Cells.Values)
            {
                if ((c.CellType == CellType.Normal || c.CellType == CellType.Generator) && c.Block == null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary> 매칭된 블럭을 파괴하고 점수와 기믹 및 연출 액션을 갱신합니다. </summary>
        private bool ProcessMatching()
        {
            bool isPlayerMatch = _pendingPlayerMatch;
            _pendingPlayerMatch = false;
            HashSet<GridPos> matches = FindMatches();
            if (matches == null || matches.Count == 0)
            {
                return false;
            }

            if (Objective != null && (isPlayerMatch || gameSpec.UseLegacyAutomaticCombos))
            {
                Objective.OnMatchEvent(isPlayerMatch);
            }

            // 이번 매칭 시퀀스의 터지는 연출을 하나의 그룹으로 묶음
            uint burstOrder = _currentOrderIndex++;
            foreach (GridPos pos in matches)
            {
                PuzzleCell cell = GetCell(pos);
                if (cell != null && cell.Block != null)
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
            return true;
        }
        /// <summary> 가로 또는 세로로 연속된 매칭 블럭의 좌표를 수집합니다. </summary>
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

        /// <summary> 조작 가능한 셀에 있는 블럭의 식별자를 반환합니다. </summary>
        /// <param name="pos">대상 격자 좌표</param>
        private string GetBlockIdAt(GridPos pos)
        {
            PuzzleCell cell = GetCell(pos);
            bool isMatchable = (cell != null && cell.Block != null) && (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator);
            return isMatchable ? cell.Block.GetBlockId() : null;
        }

        /// <summary> 블럭을 아래로 이동하고 각 생성기의 공급 구간에 빈칸을 채웁니다. </summary>
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
                    PuzzleCell cell = GetCell(new GridPos(x, y));
                    if (cell == null)
                    {
                        // 누락된 셀은 낙하 경계로 취급하여 그 위 블럭이 기존 블럭을 덮어쓰지 않게 합니다.
                        writeY = y + 1;
                        continue;
                    }

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
                            PuzzleCell targetCell = GetCell(new GridPos(x, writeY));
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

                // 아래에서 위로 생성기를 만나면 해당 생성기까지의 구간만 채웁니다.
                // 막힘/잠금/누락 셀과 이전 생성기는 다음 생성기의 공급 구간을 분리합니다.
                int fillStartY = 0;
                for (int y = 0; y < Height; y++)
                {
                    PuzzleCell cell = GetCell(new GridPos(x, y));
                    if (cell == null || cell.CellType == CellType.Close || cell.CellType == CellType.Lock)
                    {
                        fillStartY = y + 1;
                        continue;
                    }

                    if (cell.CellType == CellType.Generator)
                    {
                        if (FillFromGenerator(x, fillStartY, y, cell, fallOrder))
                        {
                            anyChanged = true;
                        }
                        fillStartY = y + 1;
                    }
                }
            }

            if (!anyChanged)
            {
                _currentOrderIndex--;
            }
            return anyChanged;
        }

        /// <summary> 생성기의 설정으로 바로 아래 공급 구간의 빈 셀을 채웁니다. </summary>
        /// <param name="x">처리할 열 좌표</param>
        /// <param name="startY">공급 구간의 아래쪽 경계</param>
        /// <param name="generatorY">생성기 셀의 높이</param>
        /// <param name="generator">블럭 목록을 제공하는 생성기</param>
        /// <param name="fallOrder">이번 낙하 연출의 공통 순서</param>
        /// <returns>블럭을 하나 이상 생성했으면 참</returns>
        private bool FillFromGenerator(int x, int startY, int generatorY, PuzzleCell generator, uint fallOrder)
        {
            bool changed = false;
            int spawnSequence = 0;
            for (int y = startY; y <= generatorY; y++)
            {
                PuzzleCell cell = GetCell(new GridPos(x, y));
                if (cell == null || cell.Block != null ||
                    (cell.CellType != CellType.Normal && cell.CellType != CellType.Generator))
                {
                    continue;
                }

                cell.Block = generator.GenerateBlock(gameSpec, Random, _blockFactory);
                if (cell.Block == null)
                {
                    continue;
                }

                cell.Block.SetState(BlockState.Falling);
                AddView(new BoardViewAction
                {
                    type = ViewType.CreateAndFall,
                    position = new GridPos(x, generatorY + 1 + spawnSequence),
                    targetPosition = new GridPos(x, y),
                    blockData = cell.Block
                }, fallOrder);
                spawnSequence++;
                changed = true;
            }
            return changed;
        }

        /// <summary> 일시정지 요청을 받습니다. 현재 구현에서는 보드 상태를 변경하지 않습니다. </summary>
        /// <param name="pause">일시정지 요청 여부</param>
        public void Pause(bool pause)
        {
        }
        /// <summary> 기록한 좌표 입력 목록의 복사본을 반환합니다. </summary>
        public List<InputRecord> GetRecordedInputs() => new List<InputRecord>(_recordedInputs);
        /// <summary> 기록한 입력 종료 목록의 복사본을 반환합니다. </summary>
        public List<InputEndRecord> GetRecordedInputEnds() => new List<InputEndRecord>(_recordedInputEnds);

        /// <summary> 연출 액션을 프레임과 순서로 정렬하고 목록의 소유권을 호출자에게 넘깁니다. </summary>
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
            List<BoardViewAction> res = _views;
            _views = new List<BoardViewAction>();
            return res;
        }

        /// <summary>
        /// 화면 연출용 액션을 추가합니다. 추가될 때마다 orderIndex가 자동으로 증가하여 순차적 연출을 보장합니다.
        /// </summary>
        /// <param name="view">등록할 연출 액션</param>
        /// <param name="customOrder">직접 지정할 연출 순서이며 생략하면 내부 순서를 사용</param>
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
            if (Cells.TryGetValue(pos, out PuzzleCell cell))
            {
                return cell;
            }
            return null;
        }
        /// <summary> 두 좌표가 가로 또는 세로로 인접하는지 확인합니다. </summary>
        /// <param name="a">첫 번째 좌표</param>
        /// <param name="b">두 번째 좌표</param>
        private bool IsAdjacent(GridPos a, GridPos b) => (Math.Abs(a.X - b.X) == 1 && a.Y == b.Y) || (Math.Abs(a.Y - b.Y) == 1 && a.X == b.X);

        // 스왑 동작을 동일한 orderIndex로 묶음
        /// <summary> 두 셀의 블럭을 교환하고 이동 연출을 등록합니다. </summary>
        /// <param name="a">첫 번째 격자 좌표</param>
        /// <param name="b">두 번째 격자 좌표</param>
        private void SwapBlocks(GridPos a, GridPos b)
        {
            PuzzleCell ca = GetCell(a);
            PuzzleCell cb = GetCell(b);
            if (ca == null || cb == null)
            {
                return;
            }

            Block t = ca.Block;
            ca.Block = cb.Block;
            cb.Block = t;

            uint order = _currentOrderIndex++; // 스왑용 단일 오더 발급
            AddView(new BoardViewAction { type = ViewType.Move, frame = (uint)_frameCount, position = a, targetPosition = b }, order);
            AddView(new BoardViewAction { type = ViewType.Move, frame = (uint)_frameCount, position = b, targetPosition = a }, order);
        }

        /// <summary> 이동 연출 없이 두 셀의 블럭 데이터만 교환합니다. </summary>
        /// <param name="a">첫 번째 격자 좌표</param>
        /// <param name="b">두 번째 격자 좌표</param>
        private void SwapBlocksDataOnly(GridPos a, GridPos b)
        {
            PuzzleCell ca = GetCell(a);
            PuzzleCell cb = GetCell(b);
            if (ca == null || cb == null)
            {
                return;
            }

            Block t = ca.Block;
            ca.Block = cb.Block;
            cb.Block = t;
        }

        /// <summary> 현재 입력 규칙으로 매칭 가능한 조작이 있는지 확인합니다. </summary>
        private bool HasPossibleMoves()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    GridPos current = new GridPos(x, y);
                    PuzzleCell cell = GetCell(current);
                    if (cell == null || cell.Block == null || (cell.CellType != CellType.Normal && cell.CellType != CellType.Generator))
                    {
                        continue;
                    }

                    if (!cell.Block.GetInputType().HasFlag(InputType.Swap))
                    {
                        continue;
                    }

                    // 오른쪽 스왑 체크
                    if (x < Width - 1)
                    {
                        GridPos right = new GridPos(x + 1, y);
                        PuzzleCell rCell = GetCell(right);
                        if ((rCell != null && rCell.Block != null) && (rCell.CellType == CellType.Normal || rCell.CellType == CellType.Generator) &&
                            rCell.Block.GetInputType().HasFlag(InputType.Swap))
                        {
                            SwapBlocksDataOnly(current, right);
                            bool hasMatch = FindMatches().Count > 0;
                            SwapBlocksDataOnly(current, right);
                            if (hasMatch)
                            {
                                return true;
                            }
                        }
                    }

                    // 위쪽 스왑 체크
                    if (y < Height - 1)
                    {
                        GridPos up = new GridPos(x, y + 1);
                        PuzzleCell uCell = GetCell(up);
                        if ((uCell != null && uCell.Block != null) && (uCell.CellType == CellType.Normal || uCell.CellType == CellType.Generator) &&
                            uCell.Block.GetInputType().HasFlag(InputType.Swap))
                        {
                            SwapBlocksDataOnly(current, up);
                            bool hasMatch = FindMatches().Count > 0;
                            SwapBlocksDataOnly(current, up);
                            if (hasMatch)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary> 가능한 조작이 없으면 재배치를 시도하고 실패 시 기존 배치를 복원합니다. </summary>
        private void CheckAndShuffleIfNoMoves()
        {
            if (HasPossibleMoves())
            {
                return;
            }

            List<Block> blocks = new List<Block>();
            List<GridPos> positions = new List<GridPos>();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    PuzzleCell cell = GetCell(new GridPos(x, y));
                    if ((cell != null && cell.Block != null) && (cell.CellType == CellType.Normal || cell.CellType == CellType.Generator))
                    {
                        blocks.Add(cell.Block);
                        positions.Add(new GridPos(x, y));
                    }
                }
            }

            if (blocks.Count == 0)
            {
                return;
            }

            // 실패 시 원래 블럭 참조와 배치를 그대로 복원하여 뷰와 일치시킵니다.
            List<Block> originalBlocks = new List<Block>(blocks);
            int maxAttempts = 100;
            bool success = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                for (int i = blocks.Count - 1; i > 0; i--)
                {
                    int j = Random.Next(0, i + 1);
                    Block temp = blocks[i];
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
                foreach (GridPos pos in positions)
                {
                    PuzzleCell cell = GetCell(pos);
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
