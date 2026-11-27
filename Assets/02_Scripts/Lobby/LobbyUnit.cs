using System;
using System.Collections.Generic;

/// <summary>고용할 로비 캐릭터의 표시 이름과 전투 역할, 애니메이션 프리팹을 연결한다.</summary>
internal sealed class LobbyUnit
{
    internal readonly string Name;
    internal readonly LobbyDefenseBoard.Piece Role;
    internal readonly string PrefabPath;

    internal static readonly LobbyUnit Rabbit = Archer("토끼 사수", null);
    internal static readonly LobbyUnit Turtle = new LobbyUnit("새싹거북", LobbyDefenseBoard.Piece.Wall, "Prefab/LobbyTurtle");
    internal static readonly LobbyUnit Trap = new LobbyUnit("마비 트랩", LobbyDefenseBoard.Piece.Trap, null);
    internal static readonly LobbyUnit Ghost = new LobbyUnit("등불유령", LobbyDefenseBoard.Piece.Empty, "Prefab/LobbyRoster/GhostLantern");

    internal static readonly IReadOnlyList<LobbyUnit> Catalog = Array.AsReadOnly(new LobbyUnit[]
    {
        Rabbit, Turtle, Trap,
        Archer("불꽃여우", "FoxSpark"), Archer("서리고양이", "CatFrost"), Archer("도토리다람쥐", "SquirrelAcorn"),
        Archer("방울펭귄", "PenguinBubble"), Archer("번개꿀벌", "BeeBolt"),
        Wall("밤송이고슴도치", "HedgeChestnut"), Wall("꿀단지곰", "BearHoney"), Wall("대나무판다", "PandaBamboo"),
        Wall("수련개구리", "FrogLily"), Wall("산호게", "CrabCoral"), Wall("딸기달팽이", "SnailBerry"),
        Wall("구름양", "SheepCloud"), Wall("통나무비버", "BeaverLog"), Wall("호박아르마딜로", "ArmadilloAmber"),
        Wall("초승달부엉이", "OwlMoon"), Wall("얼음바다코끼리", "WalrusIce"), Wall("알껍질병아리", "ChickEgg"),
        Wall("수박코뿔소", "RhinoMelon")
    });

    /// <summary><paramref name="name"/>과 <paramref name="role"/> 및 <paramref name="prefabPath"/>를 한 캐릭터로 등록한다.</summary>
    private LobbyUnit(string name, LobbyDefenseBoard.Piece role, string prefabPath)
    {
        Name = name;
        Role = role;
        PrefabPath = prefabPath;
    }

    /// <summary><paramref name="name"/> 사수를 <paramref name="id"/> 프리팹 또는 기본 사수 시트와 연결한다.</summary>
    private static LobbyUnit Archer(string name, string id)
    {
        string path = null;
        if (id != null)
        {
            path = "Prefab/LobbyRoster/" + id;
        }
        return new LobbyUnit(name, LobbyDefenseBoard.Piece.Archer, path);
    }

    /// <summary><paramref name="name"/> 방어 유닛을 <paramref name="id"/> 프리팹과 연결한다.</summary>
    private static LobbyUnit Wall(string name, string id) => new LobbyUnit(name, LobbyDefenseBoard.Piece.Wall, "Prefab/LobbyRoster/" + id);

    /// <summary><paramref name="role"/>만 지정한 고용 요청에 해당하는 기본 캐릭터를 반환한다.</summary>
    internal static LobbyUnit DefaultFor(LobbyDefenseBoard.Piece role)
    {
        switch (role)
        {
            case LobbyDefenseBoard.Piece.Archer: return Rabbit;
            case LobbyDefenseBoard.Piece.Wall: return Turtle;
            case LobbyDefenseBoard.Piece.Trap: return Trap;
            default: return null;
        }
    }
}
