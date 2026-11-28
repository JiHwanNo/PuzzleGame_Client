using System;
using System.Collections.Generic;
using Puzzle.Core;

/// <summary>로비 방어전의 배치, 보유 카드, 최단 경로와 스테이지 전투를 관리한다.</summary>
internal sealed class LobbyDefenseBoard
{
    public const int Width = 20;
    public const int Height = 10;
    public const int Entrance = Width * 4;
    public const int Exit = Entrance + Width - 1;
    public const int DrawCost = 15;
    public const int Range = 2;
    private const int Count = Width * Height;
    public const int InvalidPlacement = -2;
    private const int StunSeconds = 2;
    private const int RechargeSeconds = 6;
    /// <summary>빈 땅, 공격 유닛, 길막 유닛, 통과 가능한 트랩을 구분한다.</summary>
    internal enum Piece { Empty, Archer, Wall, Trap }
    /// <summary>스테이지 편성에서 체력과 봉쇄 공격력이 다른 세 종류의 적을 구분한다.</summary>
    internal enum EnemyKind { Scout, Soldier, Guard }

    /// <summary>한 스테이지에 출현할 적의 순서와 소환 간격을 지정한다.</summary>
    [Serializable]
    internal sealed class StagePlan
    {
        public int Interval;
        public EnemyKind[] Enemies;

        /// <summary><paramref name="interval"/>초마다 <paramref name="enemies"/>의 다음 적을 소환하는 편성을 만든다.</summary>
        public StagePlan(int interval, params EnemyKind[] enemies)
        {
            Interval = interval;
            Enemies = enemies;
        }
    }

    /// <summary>첫 세 스테이지의 기본 편성을 만든다. 이후 세 스테이지 단위로 적 체력이 증가한다.</summary>
    internal static StagePlan[] DefaultStages()
    {
        return new StagePlan[]
        {
            new StagePlan(3, EnemyKind.Scout, EnemyKind.Scout, EnemyKind.Scout, EnemyKind.Scout, EnemyKind.Scout, EnemyKind.Scout),
            new StagePlan(3, EnemyKind.Scout, EnemyKind.Soldier, EnemyKind.Scout, EnemyKind.Soldier, EnemyKind.Scout, EnemyKind.Soldier),
            new StagePlan(2, EnemyKind.Soldier, EnemyKind.Scout, EnemyKind.Guard, EnemyKind.Soldier, EnemyKind.Scout, EnemyKind.Guard)
        };
    }

    /// <summary><paramref name="plans"/>에 소환 가능한 편성과 양수 간격이 있는지 확인한다.</summary>
    internal static bool ValidStages(StagePlan[] plans)
    {
        if (plans == null || plans.Length == 0)
        {
            return false;
        }
        foreach (StagePlan plan in plans)
        {
            if (plan == null || plan.Interval <= 0 || plan.Enemies == null || plan.Enemies.Length == 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>카드, 골드, 방어 배치와 진행 중인 적을 한 번에 저장한다.</summary>
    [Serializable]
    internal sealed class Progress
    {
        private const int LegacyWidth = 100;
        public int Version = 2;
        public List<LobbyCard> Cards = new List<LobbyCard>();
        public List<Monster> Monsters = new List<Monster>();
        public int Gold;
        public int Stage = 1;
        public int Spawned;
        public int Escaped;
        public int StageTime;
        public int Seconds;
        public bool Started;

        /// <summary>저장 좌표를 검증하고 100열 저장본을 처음 열 때만 현재 전장으로 옮긴다.</summary>
        internal bool TryRestoreLayout()
        {
            if (Version == 2)
            {
                return ValidLayout(Width);
            }
            if (Version != 1 || !ValidLayout(LegacyWidth))
            {
                return false;
            }
            bool[] blocked = new bool[Count];
            foreach (LobbyCard card in Cards)
            {
                if (card.Cell < 0)
                {
                    continue;
                }
                int column = card.Cell % LegacyWidth;
                int cell = card.Cell / LegacyWidth * Width + column;
                if (column >= Width || cell == Exit)
                {
                    card.Cell = -1;
                    card.RemainingHealth = 0;
                    card.ReadyAt = 0;
                    continue;
                }
                card.Cell = cell;
                blocked[cell] = card.Unit.Role != Piece.Trap;
            }
            foreach (Monster monster in Monsters)
            {
                int column = Math.Min(monster.Cell % LegacyWidth, Width - 1);
                int target = monster.Cell / LegacyWidth * Width + column;
                int current = target;
                if (blocked[target] || target == Exit)
                {
                    // 새 출구나 방벽과 겹친 적은 가장 가까운 통행 칸에서 진행을 이어간다.
                    current = Entrance;
                    for (int cell = 0; cell < Count; cell++)
                    {
                        if (cell != Exit && !blocked[cell] && Separation(cell, target) < Separation(current, target))
                        {
                            current = cell;
                        }
                    }
                }
                monster.Cell = current;
                monster.PreviousCell = current;
                monster.FacingCell = current;
            }
            Version = 2;
            return true;
        }

        /// <summary>해당 저장본의 <paramref name="width"/>를 기준으로 카드와 적의 좌표 및 진행 수치를 검사한다.</summary>
        private bool ValidLayout(int width)
        {
            int entrance = 4 * width;
            int exit = entrance + width - 1;
            if (Cards == null || Cards.Count == 0 || Monsters == null || Stage < 1 || Gold < 0 ||
                StageTime < 0 || Seconds < 0 || Escaped < 0 || Spawned < Escaped)
            {
                return false;
            }
            HashSet<int> occupied = new HashSet<int>();
            foreach (LobbyCard card in Cards)
            {
                if (card == null || !card.ResolveUnit() ||
                    card.Attack < 0 || card.Attack > 10 || card.Defense < 0 || card.Defense > 10 || card.Health < 0 || card.Health > 10 ||
                    card.Cell < -1 || card.Cell >= width * Height || card.Cell == entrance || card.Cell == exit)
                {
                    return false;
                }
                if (card.Cell >= 0 && (!occupied.Add(card.Cell) || card.RemainingHealth <= 0))
                {
                    return false;
                }
            }
            int regular = 0;
            foreach (Monster monster in Monsters)
            {
                if (monster == null || monster.Cell < 0 || monster.Cell >= width * Height || monster.Cell == exit)
                {
                    return false;
                }
                if (!monster.Ghost)
                {
                    if (monster.Health <= 0 || monster.Health > monster.MaxHealth)
                    {
                        return false;
                    }
                    regular++;
                }
            }
            return regular <= Spawned - Escaped;
        }
    }

    private readonly StagePlan[] _stages;
    private readonly PuzzleRandom _random;
    internal readonly Progress Save;
    private readonly LobbyCard[] _lineup = new LobbyCard[Count];
    /// <summary>사격 또는 방어 유닛 피격이 일어난 논리 초, 상대 방향과 처치 여부를 보관한다.</summary>
    internal struct UnitContact
    {
        public int Second;
        public int ColumnDelta;
        public int RowDelta;
        public bool FinishingBlow;
        public Monster Target;
    }

    private readonly UnitContact[] _contacts = new UnitContact[Count];
    // 처치된 적도 해당 논리 초의 피격 연출이 끝날 때까지 뷰에 전달한다.
    private readonly List<Monster> _defeated = new List<Monster>();
    internal IReadOnlyList<Monster> Defeated => _defeated;
    private int _regularCount;
    private readonly int[] _distance = new int[Count];
    private readonly int[] _preview = new int[Count];
    private readonly int[] _queue = new int[Count];

    /// <summary>한 적의 종류와 전투 상태, 움직임을 보관한다.</summary>
    [Serializable]
    internal sealed class Monster
    {
        public int Cell = Entrance;
        public int Health;
        public int MaxHealth;
        public EnemyKind Kind;
        public bool Ghost;
        public int PreviousCell = Entrance;
        public int FacingCell = Entrance + 1;
        public int SpawnSecond;
        public int StrikeSecond = -1;
        public int Stun;
        [NonSerialized] public int HitSecond;
        [NonSerialized] public int HealthBeforeHit;
    }

    private StagePlan CurrentPlan => _stages[(Stage - 1) % _stages.Length];
    public IReadOnlyList<Monster> Monsters => Save.Monsters;
    internal IReadOnlyList<LobbyCard> Cards => Save.Cards;
    public int Gold => Save.Gold;
    public int Stage => Save.Stage;
    public bool Started => Save.Started;
    public int NextSpawn => CurrentPlan.Interval - Save.StageTime % CurrentPlan.Interval;
    public bool SpawnsFinished => Save.Spawned >= CurrentPlan.Enemies.Length;
    public bool Finished => Started && SpawnsFinished && _regularCount == 0;
    public bool Cleared => Finished && Save.Escaped == 0;
    public int RouteLength => _distance[Entrance];
    public bool PreviewSiege { get; private set; }
    public int Seconds => Save.Seconds;
    internal int RosterVersion { get; private set; }
    internal int GhostExitSecond { get; private set; } = -1;
    internal int GhostExitFrom { get; private set; }

    /// <summary><paramref name="cell"/>의 유닛이 마지막으로 사격하거나 피격된 방향과 처치 여부를 반환한다.</summary>
    public UnitContact ContactAt(int cell) => _contacts[cell];

    /// <summary><paramref name="unit"/>에서 <paramref name="opponent"/> 방향의 접촉과 처치 여부 <paramref name="finishingBlow"/>를 기록한다.</summary>
    private void RecordContact(int unit, Monster opponent, bool finishingBlow)
    {
        _contacts[unit] = new UnitContact
        {
            Second = Seconds,
            ColumnDelta = opponent.Cell % Width - unit % Width,
            RowDelta = opponent.Cell / Width - unit / Width,
            FinishingBlow = finishingBlow,
            Target = opponent
        };
    }

    /// <summary><paramref name="stages"/> 편성과 <paramref name="seed"/>로 보드를 열고 <paramref name="saved"/>가 있으면 진행을 복구한다.</summary>
    internal LobbyDefenseBoard(StagePlan[] stages, int seed, Progress saved)
    {
        _stages = stages;
        _random = new PuzzleRandom(seed);
        if (saved == null)
        {
            Save = new Progress();
            AddCard(0);
            AddCard(1);
            AddCard(2);
        }
        else
        {
            Save = saved;
            foreach (LobbyCard card in Save.Cards)
            {
                if (card.Cell >= 0)
                {
                    // 이전 저장본의 남은 체력을 살리되 현재 카드의 최대 체력을 넘지 않게 한다.
                    card.RemainingHealth = Math.Min(card.RemainingHealth, card.TotalHealth);
                    _lineup[card.Cell] = card;
                }
            }
            foreach (Monster monster in Save.Monsters)
            {
                if (!monster.Ghost)
                {
                    _regularCount++;
                }
            }
        }
        FindDistances(_distance, -1);
    }

    /// <summary>골드가 충분하면 무작위 캐릭터 카드 한 장을 뽑아 인벤토리에 넣는다.</summary>
    internal LobbyCard Draw()
    {
        if (Gold < DrawCost)
        {
            return null;
        }
        Save.Gold -= DrawCost;
        return AddCard(_random.Next(0, LobbyUnit.Catalog.Count));
    }

    /// <summary><paramref name="kind"/> 캐릭터의 세 능력치를 각각 뽑아 새 카드에 보관한다.</summary>
    private LobbyCard AddCard(int kind)
    {
        LobbyCard card = new LobbyCard
        {
            Kind = kind,
            UnitKey = LobbyUnit.Catalog[kind].Key,
            Attack = _random.Next(0, 11),
            Defense = _random.Next(0, 11),
            Health = _random.Next(0, 11)
        };
        Save.Cards.Add(card);
        RosterVersion++;
        return card;
    }

    /// <summary>현재 스테이지의 편성에서 다음 적을 입구에 소환한다.</summary>
    private void SpawnMonster()
    {
        EnemyKind kind = CurrentPlan.Enemies[Save.Spawned];
        int cycle = (Stage - 1) / _stages.Length;
        int health = 12 + (int)kind * 6 + cycle * 4;
        Save.Monsters.Add(new Monster { Kind = kind, Health = health, MaxHealth = health });
        _regularCount++;
        Save.Spawned++;
    }

    /// <summary>클리어한 스테이지를 넘기거나 돌파당한 스테이지에 다시 도전한다.</summary>
    internal bool StartNextAttempt()
    {
        if (!Finished)
        {
            return false;
        }
        if (Cleared)
        {
            Save.Stage++;
        }
        Save.Escaped = 0;
        Save.StageTime = 0;
        Save.Spawned = 0;
        SpawnMonster();
        return true;
    }

    /// <summary><paramref name="cell"/>의 유닛 점유 상태를 반환한다.</summary>
    public bool HasUnit(int cell) => _lineup[cell] != null;

    /// <summary><paramref name="cell"/>에 놓인 보유 카드 원본을 반환한다.</summary>
    internal LobbyCard CardAt(int cell) => _lineup[cell];

    /// <summary><paramref name="cell"/>에 배치된 역할을 반환한다.</summary>
    public Piece PieceAt(int cell)
    {
        if (!HasUnit(cell))
        {
            return Piece.Empty;
        }
        return _lineup[cell].Unit.Role;
    }

    /// <summary><paramref name="cell"/>에 배치된 캐릭터를 반환한다.</summary>
    internal LobbyUnit UnitAt(int cell)
    {
        if (!HasUnit(cell))
        {
            return null;
        }
        return _lineup[cell].Unit;
    }

    /// <summary><paramref name="cell"/>의 트랩이 다시 작동할 수 있는지 반환한다.</summary>
    public bool IsCharged(int cell) => Seconds >= _lineup[cell].ReadyAt;

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
        foreach (Monster monster in Save.Monsters)
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

    /// <summary>인벤토리의 미배치 <paramref name="card"/>를 <paramref name="cell"/>에 놓고 첫 배치 때 방어를 시작한다.</summary>
    internal bool PlaceCard(int cell, LobbyCard card)
    {
        if (card == null || card.Cell >= 0 || !Save.Cards.Contains(card) || Preview(cell, card.Unit.Role) == InvalidPlacement)
        {
            return false;
        }
        _lineup[cell] = card;
        card.Cell = cell;
        card.RemainingHealth = card.TotalHealth;
        card.ReadyAt = Seconds;
        _contacts[cell] = default;
        RosterVersion++;
        Array.Copy(_preview, _distance, Count);
        if (!Started)
        {
            Save.Started = true;
            SpawnMonster();
        }
        return true;
    }

    /// <summary>정비 중 <paramref name="source"/>의 유닛을 <paramref name="cell"/>로 옮길 경로를 확인한다.</summary>
    public int PreviewMove(int source, int cell)
    {
        if (!Finished || source < 0 || source >= Count || !HasUnit(source) || source == cell)
        {
            return InvalidPlacement;
        }
        LobbyCard card = _lineup[source];
        _lineup[source] = null;
        int length = Preview(cell, card.Unit.Role);
        _lineup[source] = card;
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
        _lineup[cell].Cell = cell;
        RosterVersion++;
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
                if (next >= 0 && next != blocked && (_lineup[next] == null || _lineup[next].Unit.Role == Piece.Trap) && distances[next] < 0)
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
        _defeated.Clear();
        Save.Seconds++;
        AdvanceMonsters();
        if (Seconds % 5 == 0)
        {
            Save.Monsters.Add(new Monster { Ghost = true, SpawnSecond = Seconds });
        }
        if (Finished)
        {
            return;
        }
        Save.StageTime++;
        if (!SpawnsFinished && Save.StageTime % CurrentPlan.Interval == 0)
        {
            SpawnMonster();
        }
        foreach (LobbyCard card in Save.Cards)
        {
            if (card.Cell >= 0 && card.Unit.Role == Piece.Archer)
            {
                Attack(card.Cell);
            }
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
        for (int i = 0; i < Save.Monsters.Count; i++)
        {
            Monster monster = Save.Monsters[i];
            monster.PreviousCell = monster.Cell;
            if (monster.Stun > 0)
            {
                monster.Stun--;
                continue;
            }
            int next = StepTowardExit(monster.Cell);
            Piece piece = PieceAt(next);
            monster.FacingCell = next;
            if (piece == Piece.Archer || piece == Piece.Wall)
            {
                monster.StrikeSecond = Seconds;
                LobbyCard blocker = _lineup[next];
                int impact = piece == Piece.Wall ? 2 : 4;
                blocker.RemainingHealth -= Math.Max(1, impact + (int)monster.Kind - blocker.TotalDefense);
                if (piece == Piece.Wall)
                {
                    RecordContact(next, monster, false);
                    if (!monster.Ghost && Damage(monster, blocker.TotalAttack))
                    {
                        i--;
                    }
                }
                if (blocker.RemainingHealth <= 0)
                {
                    blocker.Cell = -1;
                    _lineup[next] = null;
                    RosterVersion++;
                    FindDistances(_distance, -1);
                }
                continue;
            }
            monster.Cell = next;
            if (!monster.Ghost && piece == Piece.Trap && IsCharged(next))
            {
                LobbyCard trap = _lineup[next];
                // 기절 연장과 재충전 단축은 각각 그 카드의 방어·체력 뽑기 보너스에 따른다.
                monster.Stun = StunSeconds + trap.Defense / 5;
                trap.ReadyAt = Seconds + RechargeSeconds - trap.Health / 5;
                if (Damage(monster, trap.TotalAttack))
                {
                    i--;
                    continue;
                }
            }
            if (next == Exit)
            {
                if (monster.Ghost)
                {
                    GhostExitSecond = Seconds;
                    GhostExitFrom = monster.PreviousCell;
                }
                else
                {
                    _regularCount--;
                    Save.Escaped++;
                }
                Save.Monsters.RemoveAt(i);
                i--;
            }
        }
    }

    /// <summary><paramref name="unit"/>의 사거리에서 출구에 가까운 적을 공격하고 처치 보상을 지급한다.</summary>
    private void Attack(int unit)
    {
        LobbyCard attacker = _lineup[unit];
        Monster target = null;
        int bestDistance = int.MaxValue;
        foreach (Monster monster in Save.Monsters)
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
            if (Separation(unit, monster.Cell) <= attacker.Unit.AttackRange && (target == null || distance < bestDistance))
            {
                target = monster;
                bestDistance = distance;
            }
        }
        if (target == null)
        {
            return;
        }
        bool defeated = Damage(target, attacker.TotalAttack);
        RecordContact(unit, target, defeated);
    }

    /// <summary><paramref name="target"/>에 <paramref name="damage"/>를 주고 처치 시 한 번만 보상을 지급한다.</summary>
    private bool Damage(Monster target, int damage)
    {
        if (damage > 0 && target.HitSecond != Seconds)
        {
            target.HealthBeforeHit = target.Health;
            target.HitSecond = Seconds;
        }
        target.Health -= damage;
        if (target.Health > 0)
        {
            return false;
        }
        Save.Monsters.Remove(target);
        _defeated.Add(target);
        _regularCount--;
        Save.Gold += 5;
        return true;
    }
}
