using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>로비 캐릭터의 역할과 프리팹을 연결하고 한 플레이 세션 동안 같은 캐릭터 정의를 공유한다.</summary>
internal sealed class LobbyUnit
{
    private const string CatalogAddress = "LobbyUnitCatalog";
    internal readonly string Name;
    internal readonly LobbyDefenseBoard.Piece Role;
    internal readonly string PrefabPath;
    internal string Key
    {
        get
        {
            if (PrefabPath != null)
            {
                return PrefabPath;
            }
            return Role == LobbyDefenseBoard.Piece.Archer ? "Rabbit" : "Trap";
        }
    }

    internal static readonly LobbyUnit Rabbit = new LobbyUnit("토끼 사수", LobbyDefenseBoard.Piece.Archer, null);
    internal static readonly LobbyUnit Turtle = new LobbyUnit("새싹거북", LobbyDefenseBoard.Piece.Wall, "Prefab/LobbyTurtle");
    internal static readonly LobbyUnit Trap = new LobbyUnit("마비 트랩", LobbyDefenseBoard.Piece.Trap, null);
    private static readonly IReadOnlyList<LobbyUnit> _basicCatalog = Array.AsReadOnly(new LobbyUnit[] { Rabbit, Turtle, Trap });

    internal static LobbyUnit Ghost { get; private set; }
    internal static IReadOnlyList<LobbyUnit> Catalog { get; private set; } = _basicCatalog;

    /// <summary>JSON에 기록된 순서대로 신규 유닛 행을 읽는다.</summary>
    [Serializable]
    private sealed class CatalogFile
    {
        public UnitRow[] units = null;
    }

    /// <summary>프리팹 파일 이름인 식별자와 화면 표시 이름, 전투 역할을 보관한다.</summary>
    [Serializable]
    private sealed class UnitRow
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string role = string.Empty;
    }

    /// <summary><paramref name="name"/>과 <paramref name="role"/> 및 <paramref name="prefabPath"/>를 한 캐릭터로 등록한다.</summary>
    private LobbyUnit(string name, LobbyDefenseBoard.Piece role, string prefabPath)
    {
        Name = name;
        Role = role;
        PrefabPath = prefabPath;
    }

    /// <summary>새 플레이를 시작할 때 데이터로 추가한 정의만 비우고 기본 세 유닛을 준비한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCatalog()
    {
        Catalog = _basicCatalog;
        Ghost = null;
    }

    /// <summary>처음 성공한 카탈로그를 씬 전환 뒤에도 공유하며, 읽기 실패 후에는 다음 로비 진입에서 다시 시도한다.</summary>
    internal static bool InitializeCatalog()
    {
        if (Ghost != null)
        {
            return true;
        }
        TextAsset asset = AssetManager.Instance.LoadAsset<TextAsset>(CatalogAddress);
        if (asset == null)
        {
            Debug.LogError($"[LobbyUnit] {CatalogAddress} 데이터를 불러올 수 없습니다.");
            return false;
        }
        try
        {
            CatalogFile data = JsonUtility.FromJson<CatalogFile>(asset.text);
            string error = RegisterCatalog(data);
            if (error == null)
            {
                return true;
            }
            Debug.LogError($"[LobbyUnit] {CatalogAddress} 데이터 오류: {error}");
        }
        catch (ArgumentException exception)
        {
            Debug.LogError($"[LobbyUnit] {CatalogAddress} JSON을 읽을 수 없습니다. {exception.Message}");
        }
        return false;
    }

    /// <summary><paramref name="data"/>의 모든 행이 유효하면 목록과 유령을 함께 등록하고, 실패하면 그 이유를 반환한다.</summary>
    private static string RegisterCatalog(CatalogFile data)
    {
        if (data == null || data.units == null || data.units.Length == 0)
        {
            return "units 목록이 비어 있습니다.";
        }
        List<LobbyUnit> units = new List<LobbyUnit>(_basicCatalog);
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LobbyUnit ghost = null;
        bool hasArcher = false;
        bool hasWall = false;
        for (int i = 0; i < data.units.Length; i++)
        {
            UnitRow row = data.units[i];
            if (row == null || string.IsNullOrWhiteSpace(row.id) || string.IsNullOrWhiteSpace(row.name))
            {
                return $"{i + 1}번째 유닛에 id 또는 name이 없습니다.";
            }
            if (row.id != row.id.Trim() || row.id.Contains("/") || row.id.Contains("\\") || row.id == "." || row.id == "..")
            {
                return $"{i + 1}번째 id '{row.id}'는 프리팹 파일 이름이어야 합니다.";
            }
            if (!ids.Add(row.id))
            {
                return $"id '{row.id}'가 중복됐습니다.";
            }
            string path = "Prefab/LobbyRoster/" + row.id;
            switch (row.role)
            {
                case "Archer":
                    units.Add(new LobbyUnit(row.name, LobbyDefenseBoard.Piece.Archer, path));
                    hasArcher = true;
                    break;
                case "Wall":
                    units.Add(new LobbyUnit(row.name, LobbyDefenseBoard.Piece.Wall, path));
                    hasWall = true;
                    break;
                case "Ghost":
                    if (ghost != null)
                    {
                        return "Ghost 역할은 한 종만 등록할 수 있습니다.";
                    }
                    ghost = new LobbyUnit(row.name, LobbyDefenseBoard.Piece.Empty, path);
                    break;
                default:
                    return $"'{row.id}'의 role '{row.role}'을 인식할 수 없습니다.";
            }
        }
        if (!hasArcher || !hasWall || ghost == null)
        {
            return "Archer, Wall, Ghost 역할이 모두 필요합니다.";
        }
        Catalog = units.AsReadOnly();
        Ghost = ghost;
        return null;
    }

}

/// <summary>인벤토리의 카드 한 장에 뽑기 결과와 전장 배치 상태를 보관한다.</summary>
[Serializable]
internal sealed class LobbyCard
{
    // 과거 저장본의 종류 번호도 읽고, 이후에는 카탈로그 순서와 무관한 키를 보관한다.
    public int Kind;
    public string UnitKey;
    public int Attack;
    public int Defense;
    public int Health;
    public int Cell = -1;
    public int RemainingHealth;
    public int ReadyAt;

    /// <summary>저장된 식별자로 카탈로그 번호를 복원하고 예전 번호형 저장본에 식별자를 채운다.</summary>
    internal bool ResolveUnit()
    {
        if (string.IsNullOrEmpty(UnitKey))
        {
            if (Kind < 0 || Kind >= LobbyUnit.Catalog.Count)
            {
                return false;
            }
            UnitKey = Unit.Key;
            return true;
        }
        for (int index = 0; index < LobbyUnit.Catalog.Count; index++)
        {
            if (LobbyUnit.Catalog[index].Key == UnitKey)
            {
                Kind = index;
                return true;
            }
        }
        return false;
    }

    internal LobbyUnit Unit => LobbyUnit.Catalog[Kind];
    internal string Stats => $"공 {Attack} · 방 {Defense} · 체 {Health}";
    private static readonly string[] GradeNames = { "1성", "2성", "3성", "금별 3성" };
    private int GradeIndex => Math.Max(0, Math.Min(3, (Attack + Defense + Health) / 10));
    internal int StarCount => Math.Min(3, GradeIndex + 1);
    internal bool HasGoldStars => GradeIndex == 3;
    internal string Grade => GradeNames[GradeIndex];
}
