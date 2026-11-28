using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>배치 좌표를 숲 공터의 발밑 위치로 바꾸고, 모델에서 일어난 이동·사격·피격을 전장 위에서 재생한다.</summary>
internal sealed partial class LobbyDefenseView
{
    private const float ReleaseTime = 0.12f;
    private const float ImpactTime = 0.48f;
    private const float ImpactEnd = 0.78f;
    private const float CellWidth = FieldWidth / (float)LobbyDefenseBoard.Width;
    private readonly RectTransform[] _rows = new RectTransform[LobbyDefenseBoard.Height];
    private readonly List<RaiderActor> _raiderActors = new List<RaiderActor>();
    private readonly List<Shot> _shots = new List<Shot>();
    private RectTransform _effects;
    private RectTransform _stageHeader;
    private Image _rim;
    private RectMask2D _fieldMask;
    private ScrollRect _fieldScroll;
    private Texture2D _placementForest;
    private Texture2D _meadow;
    private Texture2D _raiders;
    private int _shotCount;

    /// <summary>실제 적 한 기와 전장에 재사용하는 캐릭터, 체력 바를 연결한다.</summary>
    private sealed class RaiderActor
    {
        public LobbyDefenseBoard.Monster Monster;
        public RawImage Body;
        public RectTransform Root;
        public RectTransform Bar;
        public RectTransform Fill;
        public bool Seen;
        public bool FacesLeft;
    }

    /// <summary>이번 논리 초에 실제로 발사된 투사체와 목표를 보관한다.</summary>
    private sealed class Shot
    {
        public RawImage Image;
        public int From;
        public LobbyDefenseBoard.Monster Target;
        public bool Arrow;
    }

    /// <summary>캔버스 중심 기준의 <paramref name="bounds"/>에 스테이지 제목과 배치 전장을 맞춘다.</summary>
    internal void SetPlacementBounds(Rect bounds)
    {
        _placementBounds = bounds;
        if (_placing)
        {
            FitTab();
            Refresh();
        }
    }

    /// <summary>배치 시 열 개 행을 남은 높이에 맞추고, 화면 너비를 넘는 열은 가로로 스크롤한다.</summary>
    private void FitBattlefield()
    {
        const float headerSpace = 118f;
        float height = _placing ? FieldHeight : 510f;
        float visibleWidth = FieldWidth;
        float scale = 1f;
        Vector2 center = new Vector2(0f, 90f);
        Vector2 headerCenter = new Vector2(0f, 410f);
        float headerScale = 1f;
        if (_placing && _placementBounds.width > 0f && _placementBounds.height > 0f)
        {
            float tabScale = _mainTab.localScale.x;
            float titleScale = Mathf.Min(_placementBounds.width / FieldWidth, _placementBounds.height / (FieldHeight + headerSpace));
            float titleHeight = headerSpace * titleScale;
            // 제목은 화면에 고정하고 전장의 두 축에 같은 배율을 적용해 셀을 정사각형으로 유지한다.
            float gridScale = (_placementBounds.height - titleHeight) / FieldHeight;
            visibleWidth = Mathf.Min(FieldWidth, _placementBounds.width / gridScale);
            scale = gridScale / tabScale;
            center = (_placementBounds.center - Vector2.up * (titleHeight * .5f)) / tabScale;
            headerScale = titleScale / tabScale;
            headerCenter = new Vector2(_placementBounds.center.x, _placementBounds.yMax - titleHeight * .5f) / tabScale;
        }
        RectTransform viewport = _fieldScroll.viewport;
        viewport.sizeDelta = new Vector2(visibleWidth, height);
        viewport.localScale = Vector3.one * scale;
        viewport.anchoredPosition = center;
        rectTransform.sizeDelta = new Vector2(FieldWidth, height);
        // 카드 선택과 전투 갱신 중에는 현재 열을 유지하고, 화면이 넓어질 때만 끝 위치를 당긴다.
        float halfWidth = FieldWidth * .5f;
        float boardX = Mathf.Clamp(rectTransform.anchoredPosition.x, visibleWidth - halfWidth, halfWidth);
        rectTransform.anchoredPosition = new Vector2(boardX, 0f);
        _actorsRoot.sizeDelta = rectTransform.sizeDelta;
        _fieldMask.enabled = _placing;
        _fieldScroll.enabled = _placing;
        _rim.rectTransform.sizeDelta = viewport.sizeDelta + Vector2.one * 10f;
        _rim.rectTransform.localScale = viewport.localScale;
        _rim.rectTransform.anchoredPosition = center;
        _rim.enabled = _placing;
        _forest.texture = _placing ? _placementForest : _meadow;
        _stageHeader.localScale = Vector3.one * headerScale;
        _stageHeader.anchoredPosition = headerCenter;
    }

    /// <summary>전장이 <paramref name="position"/>으로 스크롤되면 이동 전의 셀 미리보기를 지운다.</summary>
    private void OnFieldScrolled(Vector2 position)
    {
        if (_placing && _selected >= 0)
        {
            _selected = -1;
            Refresh();
        }
    }

    /// <summary><paramref name="cell"/> 바닥에서 <paramref name="size"/>를 기준으로 캐릭터 몸통 중심을 구한다.</summary>
    private Vector2 ActorCenter(int cell, float size) => CellCenter(cell, size) + Vector2.up * (_placing ? 0f : size * 0.43f);

    /// <summary><paramref name="monster"/>의 직전 칸과 현재 칸 사이에서 보이는 몸통 위치를 구한다.</summary>
    private Vector2 RaiderPosition(LobbyDefenseBoard.Monster monster)
    {
        Vector2 end = ActorCenter(monster.Cell, CellWidth);
        if (monster.PreviousCell == monster.Cell)
        {
            return end;
        }
        Vector2 start = ActorCenter(monster.PreviousCell, CellWidth);
        return Vector2.Lerp(start, end, Mathf.Clamp01(_elapsed / 0.85f));
    }

    /// <summary><paramref name="size"/> 크기의 셀에 생존·처치된 적과 현재 교전의 투사체만 연결한다.</summary>
    private void RefreshCombatActors(float size)
    {
        foreach (RaiderActor actor in _raiderActors)
        {
            actor.Seen = false;
        }
        foreach (LobbyDefenseBoard.Monster monster in _session.Monsters)
        {
            if (!monster.Ghost)
            {
                BindRaider(monster, size);
            }
        }
        foreach (LobbyDefenseBoard.Monster defeated in _session.Defeated)
        {
            BindRaider(defeated, size);
        }
        foreach (RaiderActor actor in _raiderActors)
        {
            if (!actor.Seen)
            {
                actor.Monster = null;
                actor.Root.gameObject.SetActive(false);
            }
        }
        _shotCount = 0;
        for (int cell = 0; cell < LobbyDefenseBoard.Width * LobbyDefenseBoard.Height; cell++)
        {
            LobbyCard card = _session.CardAt(cell);
            if (card == null || card.Unit.Role != LobbyDefenseBoard.Piece.Archer)
            {
                continue;
            }
            LobbyDefenseBoard.UnitContact hit = _session.ContactAt(card.Cell);
            if (hit.Target == null || hit.Second != _session.Seconds)
            {
                continue;
            }
            if (_shotCount == _shots.Count)
            {
                RawImage image = MakeIcon(_effects, size);
                image.name = "Projectile";
                image.texture = _raiders;
                _shots.Add(new Shot { Image = image });
            }
            Shot shot = _shots[_shotCount++];
            shot.From = card.Cell;
            shot.Target = hit.Target;
            shot.Arrow = card.Unit == LobbyUnit.Rabbit;
        }
        for (int i = _shotCount; i < _shots.Count; i++)
        {
            _shots[i].Image.enabled = false;
        }
    }

    /// <summary><paramref name="monster"/>의 기존 배우를 찾아 연결하고 <paramref name="size"/>와 배치 모드에 맞춰 그린다.</summary>
    private void BindRaider(LobbyDefenseBoard.Monster monster, float size)
    {
        RaiderActor actor = null;
        foreach (RaiderActor slot in _raiderActors)
        {
            if (slot.Monster == monster)
            {
                actor = slot;
                break;
            }
            if (slot.Monster == null)
            {
                actor = slot;
            }
        }
        if (actor == null)
        {
            actor = new RaiderActor();
            actor.Root = Element("Raider", _actorsRoot, 0, 0, size, size);
            actor.Body = MakeIcon(actor.Root, size);
            actor.Body.texture = _raiders;
            actor.Bar = Panel("Health", actor.Root, 0, 0, 30, 4, new Color32(64, 34, 33, 255)).rectTransform;
            actor.Fill = Panel("Remaining", actor.Bar, 0, 0, 1, 4, new Color32(255, 113, 84, 255)).rectTransform;
            actor.Fill.anchorMin = actor.Fill.anchorMax = new Vector2(0f, 0.5f);
            actor.Fill.pivot = new Vector2(0f, 0.5f);
            _raiderActors.Add(actor);
        }
        if (actor.Monster != monster)
        {
            actor.FacesLeft = false;
        }
        actor.Monster = monster;
        actor.Seen = true;
        int dx = monster.FacingCell % LobbyDefenseBoard.Width - monster.PreviousCell % LobbyDefenseBoard.Width;
        if (dx != 0)
        {
            actor.FacesLeft = dx < 0;
        }
        actor.Root.SetParent(_rows[monster.Cell / LobbyDefenseBoard.Width], false);
        float span = size * (_placing ? 0.96f : 1.64f);
        actor.Body.rectTransform.sizeDelta = Vector2.one * span;
        actor.Body.uvRect = AtlasRegion((int)monster.Kind, true);
        actor.Bar.anchoredPosition = Vector2.down * span * 0.50f;
    }

    /// <summary><paramref name="index"/>번째 적 또는 효과의 행 <paramref name="upper"/>를 아틀라스 UV로 반환한다.</summary>
    private static Rect AtlasRegion(int index, bool upper) => new Rect(index / 3f, upper ? 0.5f : 0f, 1f / 3f, 0.5f);

    /// <summary>각 적의 걸음과 실제 피격 시점, 사수의 발사와 착탄을 같은 논리 초 안에서 보간한다.</summary>
    private void AnimateCombat()
    {
        foreach (RaiderActor actor in _raiderActors)
        {
            if (actor.Monster != null)
            {
                AnimateRaider(actor);
            }
        }
        for (int i = 0; i < _shotCount; i++)
        {
            AnimateShot(_shots[i]);
        }
    }

    /// <summary><paramref name="actor"/>가 이동·공격·마비·처치 상태에 맞게 걸음, 반동, 체력을 표시한다.</summary>
    private void AnimateRaider(RaiderActor actor)
    {
        LobbyDefenseBoard.Monster monster = actor.Monster;
        bool dead = monster.Health <= 0;
        bool visible = !dead || _elapsed < ImpactEnd;
        actor.Root.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }
        bool hit = monster.HitSecond > 0 && monster.HitSecond == _session.Seconds;
        float impact = Mathf.InverseLerp(ImpactTime, ImpactEnd, _elapsed);
        float recoil = hit ? Mathf.Sin(impact * Mathf.PI) : 0f;
        float fade = dead ? 1f - impact : 1f;
        float walk = _elapsed < 0.85f && monster.Cell != monster.PreviousCell ? Mathf.Sin(_elapsed / 0.85f * 4f * Mathf.PI) : 0f;
        float strike = monster.StrikeSecond == _session.Seconds ? Mathf.Sin(Mathf.Clamp01(_elapsed / 0.45f) * Mathf.PI) : 0f;
        float facing = actor.FacesLeft ? -1f : 1f;
        Vector2 position = RaiderPosition(monster);
        position.x += facing * (strike * 6f - recoil * 4f);
        position.y += Mathf.Abs(walk) * 3.2f;
        actor.Root.anchoredPosition = position;
        RectTransform body = actor.Body.rectTransform;
        body.localScale = new Vector3(facing * (1f - Mathf.Abs(walk) * 0.045f), 1f + Mathf.Abs(walk) * 0.06f, 1f);
        body.localEulerAngles = new Vector3(0f, 0f, facing * (walk * 5f - strike * 11f + (1f - fade) * 40f));
        Color baseColor = monster.Stun > 0 ? new Color32(194, 163, 255, 255) : Color.white;
        Color color = Color.Lerp(baseColor, new Color32(255, 128, 91, 255), recoil);
        color.a = fade;
        actor.Body.color = color;
        int health = hit && _elapsed < ImpactTime ? monster.HealthBeforeHit : monster.Health;
        actor.Bar.gameObject.SetActive(health > 0 && health < monster.MaxHealth);
        actor.Fill.sizeDelta = new Vector2(30f * Mathf.Clamp01((float)health / monster.MaxHealth), 4f);
    }

    /// <summary><paramref name="shot"/>의 화살 또는 마법탄을 사수에서 움직이는 실제 표적으로 보내고 착탄을 표시한다.</summary>
    private void AnimateShot(Shot shot)
    {
        RawImage image = shot.Image;
        image.enabled = _elapsed >= ReleaseTime && _elapsed < ImpactEnd && _raiders != null;
        if (!image.enabled)
        {
            return;
        }
        Vector2 from = ActorCenter(shot.From, CellWidth);
        Vector2 to = RaiderPosition(shot.Target);
        float size = _placing ? 24f : 38f;
        RectTransform rect = image.rectTransform;
        if (_elapsed < ImpactTime)
        {
            float t = Mathf.InverseLerp(ReleaseTime, ImpactTime, _elapsed);
            Vector2 aim = to - from;
            image.uvRect = AtlasRegion(shot.Arrow ? 0 : 1, false);
            image.color = Color.white;
            rect.anchoredPosition = Vector2.Lerp(from, to, 0.2f + 0.8f * t);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            rect.sizeDelta = Vector2.one * size;
            return;
        }
        float f = Mathf.InverseLerp(ImpactTime, ImpactEnd, _elapsed);
        image.uvRect = AtlasRegion(2, false);
        image.color = new Color(1f, 1f, 1f, 1f - f);
        rect.anchoredPosition = to;
        rect.localEulerAngles = new Vector3(0f, 0f, 24f * f);
        rect.sizeDelta = Vector2.one * size * (0.65f + 0.95f * f);
    }

    /// <summary><paramref name="mesh"/>에 배치한 캐릭터 발밑의 흙과 입·출구의 통행 표식을 그린다.</summary>
    private void DrawBattleGround(VertexHelper mesh)
    {
        for (int cell = 0; cell < LobbyDefenseBoard.Width * LobbyDefenseBoard.Height; cell++)
        {
            if (_session.HasUnit(cell))
            {
                GroundDisc(mesh, cell, 0.62f, 0.29f, Color.white, GroundUV(cell, true));
            }
        }
        Rect white = new Rect(0.01f, 0.02f, 0f, 0f);
        GroundDisc(mesh, LobbyDefenseBoard.Entrance, 0.47f, 0.17f, new Color32(151, 217, 162, 215), white);
        GroundDisc(mesh, LobbyDefenseBoard.Exit, 0.47f, 0.17f, new Color32(205, 177, 241, 215), white);
        DrawTraps(mesh);
    }

    /// <summary><paramref name="mesh"/>에 실제로 설치된 트랩을 충전 상태에 따라 보라색 보석으로 표시한다.</summary>
    private void DrawTraps(VertexHelper mesh)
    {
        Rect white = new Rect(0.01f, 0.02f, 0f, 0f);
        for (int cell = 0; cell < LobbyDefenseBoard.Width * LobbyDefenseBoard.Height; cell++)
        {
            if (_session.PieceAt(cell) != LobbyDefenseBoard.Piece.Trap)
            {
                continue;
            }
            Color gem = _session.IsCharged(cell) ? new Color32(189, 112, 247, 255) : new Color32(115, 94, 144, 255);
            GroundDisc(mesh, cell, 0.44f, 0.36f, gem, white, 4);
            GroundDisc(mesh, cell, 0.20f, 0.19f, Honey, white, 4);
        }
    }

    /// <summary><paramref name="mesh"/>의 <paramref name="cell"/>에 셀 너비 기준 반경과 <paramref name="uv"/> 텍스처를 가진 다각형을 그린다.</summary>
    private void GroundDisc(VertexHelper mesh, int cell, float radiusX, float radiusY, Color color, Rect uv, int sides = 20)
    {
        Vector2 center = CellCenter(cell, CellWidth);
        int first = mesh.currentVertCount;
        mesh.AddVert(center, color, uv.center);
        for (int i = 0; i < sides; i++)
        {
            float angle = 2f * Mathf.PI * i / sides;
            Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 offset = new Vector2(radiusX * axis.x, radiusY * axis.y) * CellWidth;
            mesh.AddVert(center + offset, color, uv.center + Vector2.Scale(uv.size, axis) * 0.5f);
            mesh.AddTriangle(first, first + 1 + (i + 1) % sides, first + 1 + i);
        }
    }
}
