using System;
using System.Collections.Generic;

/// <summary>로비 방어전의 배치, 최단 경로, 초 단위 전투와 고용 비용을 관리한다.</summary>
internal sealed class LobbyDefenseBoard
{
    public const int Width = 100;
    public const int Height = 10;
    public const int Entrance = Width * 4;
    public const int Exit = Entrance + Width - 1;
    public const int HireCost = 15;
    public const int Range = 2;
    private const int Count = Width * Height;
    public const int InvalidPlacement = -2;
    private const int StunSeconds = 2;
    private const int RechargeSeconds = 6;
    /// <summary>빈 땅, 공격 유닛, 길막 유닛, 통과 가능한 트랩을 구분한다.</summary>
    internal enum Piece { Empty, Archer, Wall, Trap }
    private readonly LobbyUnit[] _lineup = new LobbyUnit[Count];
    private readonly int[] _strength = new int[Count];
    private readonly int[] _readyAt = new int[Count];
    /// <summary>사격 또는 방어 유닛 피격이 일어난 논리 초, 상대 방향과 처치 여부를 보관한다.</summary>
    internal struct UnitContact
    {
        public int Second;
        public int ColumnDelta;
        public int RowDelta;
        public bool FinishingBlow;
    }

    private readonly UnitContact[] _contacts = new UnitContact[Count];
    private int _seconds;
    private int _regularCount;
    private int _archers;
    private readonly List<int> _units = new List<int>();
    private readonly List<Monster> _monsters = new List<Monster>();
    private readonly int[] _distance = new int[Count];
    private readonly int[] _preview = new int[Count];
    private readonly int[] _queue = new int[Count];
    private int _spawned;
    private int _waveTime;
    private int _rest;

    /// <summary>한 적의 종류, 위치, 체력과 남은 마비 시간을 보관한다.</summary>
    internal sealed class Monster
    {
        public int Cell = Entrance;
        public int Health = 12;
        public bool Ghost;
        public int PreviousCell = Entrance;
        public int FacingCell = Entrance + 1;
        public int SpawnSecond;
        public int StrikeSecond = -1;
        public int Stun;
    }

    public IReadOnlyList<Monster> Monsters => _monsters;
    public int Gold { get; private set; }
    public int Wave { get; private set; } = 1;
    public bool Started { get; private set; }
    public int NextSpawn => 3 - _waveTime % 3;
    public int Rest => _rest;
    public bool SpawnsFinished => _spawned == 6;
    public int RouteLength => _distance[Entrance];
    public bool PreviewSiege { get; private set; }
    public int Seconds => _seconds;
    internal int RosterVersion { get; private set; }
    internal int GhostExitSecond { get; private set; } = -1;
    internal int GhostExitFrom { get; private set; }

    /// <summary><paramref name="cell"/>의 유닛이 마지막으로 사격하거나 피격된 방향과 처치 여부를 반환한다.</summary>
    public UnitContact ContactAt(int cell) => _contacts[cell];

    /// <summary><paramref name="unit"/>에서 <paramref name="opponent"/> 방향의 접촉과 처치 여부 <paramref name="finishingBlow"/>를 기록한다.</summary>
    private void RecordContact(int unit, int opponent, bool finishingBlow)
    {
        _contacts[unit] = new UnitContact
        {
            Second = _seconds,
            ColumnDelta = opponent % Width - unit % Width,
            RowDelta = opponent / Width - unit / Width,
            FinishingBlow = finishingBlow
        };
    }

    /// <summary>빈 보드의 출구까지 거리를 초기화한다.</summary>
    public LobbyDefenseBoard()
    {
        FindDistances(_distance, -1);
    }

    /// <summary>첫 방문에 무료 사수를 입구 옆에 배치하고 첫 웨이브의 선두 몬스터를 즉시 소환한다.</summary>
    public void StartFirstWave()
    {
        if (!Started && Hire(Entrance + 3))
        {
            SpawnMonster();
        }
    }

    /// <summary>입구에 일반 몬스터 한 마리를 추가하고 웨이브 소환 수와 생존 수를 갱신한다.</summary>
    private void SpawnMonster()
    {
        _monsters.Add(new Monster());
        _regularCount++;
        _spawned++;
    }

    /// <summary><paramref name="cell"/>의 유닛 점유 상태를 반환한다.</summary>
    public bool HasUnit(int cell) => _lineup[cell] != null;

    /// <summary><paramref name="cell"/>의 배치 종류를 반환한다.</summary>
    public Piece PieceAt(int cell)
    {
        LobbyUnit unit = _lineup[cell];
        if (unit == null)
        {
            return Piece.Empty;
        }
        return unit.Role;
    }

    /// <summary><paramref name="cell"/>에 배치된 캐릭터를 반환한다.</summary>
    internal LobbyUnit UnitAt(int cell) => _lineup[cell];

    /// <summary>현재 전장에서 <paramref name="unit"/>와 같은 캐릭터의 배치 수를 센다.</summary>
    internal int CountOf(LobbyUnit unit)
    {
        int count = 0;
        foreach (int cell in _units)
        {
            if (_lineup[cell] == unit)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary><paramref name="cell"/>의 트랩이 다시 작동할 수 있는지 반환한다.</summary>
    public bool IsCharged(int cell) => _seconds >= _readyAt[cell];

    /// <summary><paramref name="piece"/>의 고용 비용을 구하며 공격 유닛을 모두 잃으면 무료 한 기를 지원한다.</summary>
    public int CostOf(Piece piece) => piece == Piece.Archer && _archers == 0 ? 0 : HireCost;

    /// <summary><paramref name="a"/>와 <paramref name="b"/> 사이의 상하좌우 거리를 구한다.</summary>
    public static int Separation(int a, int b) => Math.Abs(a % Width - b % Width) + Math.Abs(a / Width - b / Width);

    /// <summary><paramref name="cell"/>에 <paramref name="piece"/>를 놓은 뒤의 경로 길이, 봉쇄(-1), 배치 불가(-2)를 구분한다.</summary>
    public int Preview(int cell, Piece piece = Piece.Archer)
    {
        if (cell < 0 || cell >= Count || cell == Entrance || cell == Exit || HasUnit(cell))
        {
            return InvalidPlacement;
        }
        int blocked = piece == Piece.Trap ? -1 : cell;
        FindDistances(_preview, blocked);
        PreviewSiege = _preview[Entrance] < 0;
        foreach (Monster monster in _monsters)
        {
            if (monster.Cell == cell || StepTowardExit(monster.Cell) == cell)
            {
                return InvalidPlacement;
            }
            if (_preview[monster.Cell] < 0)
            {
                PreviewSiege = true;
            }
        }
        return _preview[Entrance];
    }

    /// <summary><paramref name="cell"/>에 <paramref name="piece"/>를 배치할 수 있으면 비용을 지불하고 경로를 갱신한다.</summary>
    public bool Hire(int cell, Piece piece = Piece.Archer) => Hire(cell, LobbyUnit.DefaultFor(piece));

    /// <summary><paramref name="cell"/>에 <paramref name="unit"/>을 고용하고 해당 역할의 전투 규칙을 적용한다.</summary>
    internal bool Hire(int cell, LobbyUnit unit)
    {
        if (unit == null || unit.Role == Piece.Empty)
        {
            return false;
        }
        Piece piece = unit.Role;
        int cost = CostOf(piece);
        if (Gold < cost || Preview(cell, piece) == InvalidPlacement)
        {
            return false;
        }
        Gold -= cost;
        _lineup[cell] = unit;
        RosterVersion++;
        _contacts[cell] = default;
        _strength[cell] = piece == Piece.Wall ? 36 : 12;
        _readyAt[cell] = _seconds;
        if (piece == Piece.Archer)
        {
            _archers++;
        }
        Started = true;
        _units.Add(cell);
        Array.Copy(_preview, _distance, Count);
        return true;
    }

    /// <summary>정비 중 <paramref name="source"/>의 유닛을 <paramref name="cell"/>로 옮길 경로를 확인한다.</summary>
    public int PreviewMove(int source, int cell)
    {
        if (_rest == 0 || source < 0 || source >= Count || !HasUnit(source) || source == cell)
        {
            return InvalidPlacement;
        }
        LobbyUnit unit = _lineup[source];
        _lineup[source] = null;
        int length = Preview(cell, unit.Role);
        _lineup[source] = unit;
        return length;
    }

    /// <summary>정비 중 <paramref name="source"/>의 유닛을 <paramref name="cell"/>로 무료 이동한다.</summary>
    public bool MoveUnit(int source, int cell)
    {
        if (PreviewMove(source, cell) == InvalidPlacement)
        {
            return false;
        }
        _lineup[cell] = _lineup[source];
        _lineup[source] = null;
        _contacts[cell] = _contacts[source];
        _contacts[source] = default;
        _strength[cell] = _strength[source];
        _readyAt[cell] = _readyAt[source];
        _units[_units.IndexOf(source)] = cell;
        Array.Copy(_preview, _distance, Count);
        return true;
    }

    /// <summary><paramref name="cell"/>에서 <paramref name="direction"/> 순서의 이웃을 반환한다.</summary>
    private static int Neighbor(int cell, int direction)
    {
        switch (direction)
        {
            case 0: return cell % Width < Width - 1 ? cell + 1 : -1;
            case 1: return cell >= Width ? cell - Width : -1;
            case 2: return cell < Count - Width ? cell + Width : -1;
            default: return cell % Width > 0 ? cell - 1 : -1;
        }
    }

    /// <summary><paramref name="blocked"/>를 막은 출구 기준 거리표를 <paramref name="distances"/>에 채운다.</summary>
    private void FindDistances(int[] distances, int blocked)
    {
        Array.Fill(distances, -1);
        distances[Exit] = 0;
        _queue[0] = Exit;
        int tail = 1;
        for (int head = 0; head < tail; head++)
        {
            int cell = _queue[head];
            for (int direction = 0; direction < 4; direction++)
            {
                int next = Neighbor(cell, direction);
                if (next >= 0 && next != blocked && (_lineup[next] == null || _lineup[next].Role == Piece.Trap) && distances[next] < 0)
                {
                    distances[next] = distances[cell] + 1;
                    _queue[tail++] = next;
                }
            }
        }
    }

    /// <summary><paramref name="cell"/>에서 <paramref name="preview"/>에 해당하는 최단 경로의 다음 칸을 구한다.</summary>
    public int Next(int cell, bool preview)
    {
        int[] distances = preview ? _preview : _distance;
        for (int direction = 0; direction < 4; direction++)
        {
            int next = Neighbor(cell, direction);
            if (next >= 0 && distances[cell] > 0 && distances[next] == distances[cell] - 1)
            {
                return next;
            }
        }
        return -1;
    }

    /// <summary>로비 전투를 한 초 진행해 이동, 소환, 공격, 정비를 순서대로 처리한다.</summary>
    public void Tick()
    {
        if (!Started)
        {
            return;
        }
        _seconds++;
        AdvanceMonsters();
        if (_seconds % 5 == 0)
        {
            _monsters.Add(new Monster { Ghost = true, SpawnSecond = _seconds });
        }
        if (_rest > 0)
        {
            _rest--;
            if (_rest == 0)
            {
                Wave++;
                _spawned = 0;
                _waveTime = 0;
                SpawnMonster();
            }
            return;
        }
        _waveTime++;
        if (!SpawnsFinished && _waveTime % 3 == 0)
        {
            SpawnMonster();
        }
        foreach (int unit in _units)
        {
            if (_lineup[unit].Role == Piece.Archer)
            {
                Attack(unit);
            }
        }
        if (SpawnsFinished && _regularCount == 0)
        {
            _rest = 8;
        }
    }

    /// <summary><paramref name="cell"/>의 우회로를 우선하며 고립된 적은 출구 방향의 방벽으로 접근한다.</summary>
    private int StepTowardExit(int cell)
    {
        if (_distance[cell] >= 0)
        {
            return Next(cell, false);
        }
        if (cell % Width < Width - 1)
        {
            return cell + 1;
        }
        return cell < Exit ? cell + Width : cell - Width;
    }

    /// <summary>소환 순서로 이동하고 막힌 유닛의 체력과 트랩 재충전 시각을 공유한다.</summary>
    private void AdvanceMonsters()
    {
        for (int i = 0; i < _monsters.Count; i++)
        {
            Monster monster = _monsters[i];
            if (monster.Stun > 0)
            {
                monster.Stun--;
                continue;
            }
            int next = StepTowardExit(monster.Cell);
            Piece piece = PieceAt(next);
            monster.PreviousCell = monster.Cell;
            monster.FacingCell = next;
            if (piece == Piece.Archer || piece == Piece.Wall)
            {
                monster.StrikeSecond = _seconds;
                _strength[next] -= piece == Piece.Wall ? 2 : 4;
                if (piece == Piece.Wall)
                {
                    RecordContact(next, monster.Cell, false);
                }
                if (_strength[next] <= 0)
                {
                    if (piece == Piece.Archer)
                    {
                        _archers--;
                    }
                    _lineup[next] = null;
                    RosterVersion++;
                    _units.Remove(next);
                    FindDistances(_distance, -1);
                }
                continue;
            }
            monster.Cell = next;
            if (!monster.Ghost && piece == Piece.Trap && IsCharged(next))
            {
                monster.Stun = StunSeconds;
                _readyAt[next] = _seconds + RechargeSeconds;
            }
            if (next == Exit)
            {
                if (monster.Ghost)
                {
                    GhostExitSecond = _seconds;
                    GhostExitFrom = monster.PreviousCell;
                }
                else
                {
                    _regularCount--;
                }
                _monsters.RemoveAt(i);
                i--;
            }
        }
    }

    /// <summary><paramref name="unit"/>의 사거리에서 출구에 가까운 적을 공격하고 처치 보상을 지급한다.</summary>
    private void Attack(int unit)
    {
        Monster target = null;
        int bestDistance = int.MaxValue;
        foreach (Monster monster in _monsters)
        {
            if (monster.Ghost)
            {
                continue;
            }
            int distance = _distance[monster.Cell];
            if (distance < 0)
            {
                distance = int.MaxValue;
            }
            if (Separation(unit, monster.Cell) <= Range && (target == null || distance < bestDistance))
            {
                target = monster;
                bestDistance = distance;
            }
        }
        if (target == null)
        {
            return;
        }
        target.Health -= 4;
        bool defeated = target.Health <= 0;
        RecordContact(unit, target.Cell, defeated);
        if (defeated)
        {
            _monsters.Remove(target);
            _regularCount--;
            Gold += 5;
        }
    }
}
