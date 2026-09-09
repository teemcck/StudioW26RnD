using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Owns the worm boss's tile cache, world-space path sampling, melee-cone queries, and destination scoring.
/// This plain C# module has explicit scene dependencies and does not own combat state or start coroutines.
/// </summary>
internal sealed class BossArenaNavigation
{
    // Dependencies are fixed for the encounter; the boss Transform supplies live position fallbacks.
    private readonly Transform transform;
    private readonly Tilemap baseTilemap;
    private readonly Tilemap decorationTilemap;
    private readonly Collider2D _mainCollider;
    private readonly SpriteRenderer spriteRenderer;
    private readonly float baseArenaEdgePadding;

    // Ordered cells preserve sampling order; the set accelerates membership checks during BFS.
    private readonly List<Vector3Int> _walkableCells = new();

    private readonly HashSet<Vector3Int> _walkableCellSet = new();

    // Bounds are measured from occupied Base tiles, then narrowed by boss extents when scoring a move.
    private float _arenaMinX;

    private float _arenaMaxX;

    private float _arenaMinY;

    private float _arenaMaxY;

    private bool _arenaClampReady;

    private Vector2 _pivotHalfExtentsWorld;

    /// <summary>
    /// Captures arena references and builds the same extent, boundary, and walkability caches used at boss initialization.
    /// </summary>
    public BossArenaNavigation(Transform bossTransform, Tilemap baseTilemap, Tilemap decorationTilemap,
        Collider2D mainCollider, SpriteRenderer spriteRenderer, float baseArenaEdgePadding)
    {
        transform = bossTransform;
        this.baseTilemap = baseTilemap;
        this.decorationTilemap = decorationTilemap;
        _mainCollider = mainCollider;
        this.spriteRenderer = spriteRenderer;
        this.baseArenaEdgePadding = baseArenaEdgePadding;
        CacheBossPivotHalfExtentsWorld();
        CacheBaseArenaWorldBounds();
        CacheWalkableCells();
    }

    /// <summary>
    /// Caches occupied arena cells for membership checks, pathfinding, and attack targeting.
    /// </summary>
    private void CacheWalkableCells()
    {
        _walkableCells.Clear();
        _walkableCellSet.Clear();
        Tilemap source = baseTilemap ? baseTilemap : decorationTilemap;
        if (!source) return;
        BoundsInt bounds = source.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!IsWalkableCell(cell)) continue;
                _walkableCells.Add(cell);
                _walkableCellSet.Add(cell);
            }
        }
    }

    /// <summary>
    /// Treats occupied Base tiles as walkable; Decoration is used only when Base is absent.
    /// </summary>
    private bool IsWalkableCell(Vector3Int cell)
    {
        if (baseTilemap)
            return baseTilemap.HasTile(cell);
        if (decorationTilemap)
            return decorationTilemap.HasTile(cell);
        return false;
    }

    /// <summary>
    /// Combines collider and sprite extents with edge padding to keep sampled destinations inside the arena.
    /// </summary>
    private void CacheBossPivotHalfExtentsWorld()
    {
        _pivotHalfExtentsWorld = Vector2.zero;

        if (_mainCollider is BoxCollider2D box)
        {
            Vector2 he = Vector2.Scale(box.size * 0.5f, transform.lossyScale);
            _pivotHalfExtentsWorld = new Vector2(Mathf.Abs(he.x), Mathf.Abs(he.y));
        }
        else if (_mainCollider)
        {
            _pivotHalfExtentsWorld = _mainCollider.bounds.extents;
        }

        if (spriteRenderer && spriteRenderer.sprite)
        {
            Bounds ls = spriteRenderer.sprite.bounds;
            Vector3 sc = transform.lossyScale;
            Vector2 fromSprite = new Vector2(
                Mathf.Abs(ls.extents.x * sc.x),
                Mathf.Abs(ls.extents.y * sc.y));
            _pivotHalfExtentsWorld.x = Mathf.Max(_pivotHalfExtentsWorld.x, fromSprite.x);
            _pivotHalfExtentsWorld.y = Mathf.Max(_pivotHalfExtentsWorld.y, fromSprite.y);
        }

        float pad = Mathf.Max(0f, baseArenaEdgePadding);
        _pivotHalfExtentsWorld.x += pad;
        _pivotHalfExtentsWorld.y += pad;
    }

    /// <summary>
    /// Measures occupied Base tile edges in world space; empty tilemaps leave boundary filtering disabled.
    /// </summary>
    private void CacheBaseArenaWorldBounds()
    {
        _arenaClampReady = false;
        if (!baseTilemap)
            return;

        BoundsInt cb = baseTilemap.cellBounds;
        Vector2 halfCell = new Vector2(
            Mathf.Abs(baseTilemap.cellSize.x * baseTilemap.transform.lossyScale.x) * 0.5f,
            Mathf.Abs(baseTilemap.cellSize.y * baseTilemap.transform.lossyScale.y) * 0.5f);

        bool any = false;
        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
        for (int x = cb.xMin; x < cb.xMax; x++)
        {
            for (int y = cb.yMin; y < cb.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!baseTilemap.HasTile(cell))
                    continue;

                Vector2 center = baseTilemap.GetCellCenterWorld(cell);
                if (!any)
                {
                    minX = center.x - halfCell.x;
                    maxX = center.x + halfCell.x;
                    minY = center.y - halfCell.y;
                    maxY = center.y + halfCell.y;
                    any = true;
                }
                else
                {
                    minX = Mathf.Min(minX, center.x - halfCell.x);
                    maxX = Mathf.Max(maxX, center.x + halfCell.x);
                    minY = Mathf.Min(minY, center.y - halfCell.y);
                    maxY = Mathf.Max(maxY, center.y + halfCell.y);
                }
            }
        }

        if (!any)
            return;

        _arenaMinX = minX;
        _arenaMaxX = maxX;
        _arenaMinY = minY;
        _arenaMaxY = maxY;
        _arenaClampReady = true;
    }

    /// <summary>
    /// The same preferred tilemap drives all world and cell conversions.
    /// </summary>
    private Tilemap PrimaryTilemap => baseTilemap ? baseTilemap : decorationTilemap;

    /// <summary>
    /// Converts a tile cell to world space, falling back to the boss position without a tilemap.
    /// </summary>
    public Vector2 CellCenterWorld(Vector3Int cell)
    {
        return PrimaryTilemap ? (Vector2)PrimaryTilemap.GetCellCenterWorld(cell) : (Vector2)transform.position;
    }

    /// <summary>
    /// Converts world coordinates through the arena tilemap, or returns the origin cell without one.
    /// </summary>
    public Vector3Int WorldToCell(Vector2 world)
    {
        return PrimaryTilemap ? PrimaryTilemap.WorldToCell(world) : Vector3Int.zero;
    }

    /// <summary>
    /// Returns the tile cell beneath the current boss pivot.
    /// </summary>
    public Vector3Int GetCurrentCell() => WorldToCell(transform.position);

    /// <summary>
    /// Keeps an occupied target cell or finds the nearest cached cell by world distance.
    /// </summary>
    public Vector3Int NearestWalkableCell(Vector2 world)
    {
        if (_walkableCells.Count == 0) return GetCurrentCell();
        Vector3Int direct = WorldToCell(world);
        if (_walkableCellSet.Contains(direct)) return direct;

        float best = float.PositiveInfinity;
        Vector3Int bestCell = _walkableCells[0];
        foreach (Vector3Int c in _walkableCells)
        {
            float d = ((Vector2)CellCenterWorld(c) - world).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestCell = c;
            }
        }
        return bestCell;
    }

    /// <summary>
    /// Finds a four-neighbor route using breadth-first search. Clears the output first and returns false if disconnected.
    /// </summary>
    public bool TryGetWalkablePath(Vector3Int from, Vector3Int to, List<Vector3Int> outPath)
    {
        outPath.Clear();
        if (_walkableCellSet.Count == 0) return false;
        if (!_walkableCellSet.Contains(from)) from = NearestWalkableCell(CellCenterWorld(from));
        if (!_walkableCellSet.Contains(to)) to = NearestWalkableCell(CellCenterWorld(to));
        if (from == to)
        {
            outPath.Add(from);
            return true;
        }

        Dictionary<Vector3Int, Vector3Int> prev = new Dictionary<Vector3Int, Vector3Int>();
        Queue<Vector3Int> q = new Queue<Vector3Int>();
        q.Enqueue(from);
        prev[from] = from;

        // Recording a predecessor also marks a cell visited, preventing loops and duplicate queue entries.
        void EnqueueNeighbor(Vector3Int n, Vector3Int parent)
        {
            if (!_walkableCellSet.Contains(n) || prev.ContainsKey(n)) return;
            prev[n] = parent;
            q.Enqueue(n);
        }

        while (q.Count > 0)
        {
            Vector3Int c = q.Dequeue();
            if (c == to)
            {
                Vector3Int w = to;
                while (w != from)
                {
                    outPath.Add(w);
                    w = prev[w];
                }
                outPath.Add(from);
                outPath.Reverse();
                return true;
            }

            EnqueueNeighbor(new Vector3Int(c.x + 1, c.y, 0), c);
            EnqueueNeighbor(new Vector3Int(c.x - 1, c.y, 0), c);
            EnqueueNeighbor(new Vector3Int(c.x, c.y + 1, 0), c);
            EnqueueNeighbor(new Vector3Int(c.x, c.y - 1, 0), c);
        }

        return false;
    }

    /// <summary>
    /// Measures distance between cell centers so travel timing respects the arena grid transform.
    /// </summary>
    public float MeasurePathWorldLength(List<Vector3Int> path)
    {
        if (path == null || path.Count < 2) return 0f;
        float s = 0f;
        for (int i = 0; i < path.Count - 1; i++)
            s += Vector2.Distance(CellCenterWorld(path[i]), CellCenterWorld(path[i + 1]));
        return s;
    }

    /// <summary>
    /// Samples distance along a cell path, clamping beyond its ends and tolerating empty paths.
    /// </summary>
    public Vector2 PointOnWalkablePathAtDistance(List<Vector3Int> path, float distAlong)
    {
        if (path == null || path.Count == 0) return (Vector2)transform.position;
        distAlong = Mathf.Max(0f, distAlong);
        if (path.Count == 1) return CellCenterWorld(path[0]);
        float acc = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2 a = CellCenterWorld(path[i]);
            Vector2 b = CellCenterWorld(path[i + 1]);
            float seg = Vector2.Distance(a, b);
            if (acc + seg >= distAlong - 0.0001f)
            {
                float t = seg > 1e-5f ? (distAlong - acc) / seg : 0f;
                return Vector2.Lerp(a, b, Mathf.Clamp01(t));
            }
            acc += seg;
        }
        return CellCenterWorld(path[path.Count - 1]);
    }

    /// <summary>
    /// Samples nearby path positions to orient rubble along movement, with a stable rightward fallback.
    /// </summary>
    public Vector2 TangentOnWalkablePathAtDistance(List<Vector3Int> path, float distAlong, float delta)
    {
        if (path == null || path.Count < 2) return Vector2.right;
        delta = Mathf.Max(0.02f, delta);
        Vector2 p0 = PointOnWalkablePathAtDistance(path, Mathf.Max(0f, distAlong - delta));
        Vector2 p1 = PointOnWalkablePathAtDistance(path, distAlong + delta);
        Vector2 d = p1 - p0;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    /// <summary>
    /// Selects cached cell centers inside a melee cone; an empty cone falls back to the nearest forward cell.
    /// </summary>
    public List<Vector3Int> GetArcCells(Vector2 center, Vector2 forward, float range, float halfArcDegrees)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        float cosThreshold = Mathf.Cos(halfArcDegrees * Mathf.Deg2Rad);
        foreach (Vector3Int cell in _walkableCells)
        {
            Vector2 world = CellCenterWorld(cell);
            Vector2 d = world - center;
            float mag = d.magnitude;
            if (mag <= 0.001f || mag > range) continue;
            Vector2 dn = d / mag;
            if (Vector2.Dot(forward, dn) >= cosThreshold)
                cells.Add(cell);
        }
        if (cells.Count == 0)
        {
            cells.Add(NearestWalkableCell(center + forward * Mathf.Max(0.5f, range * 0.5f)));
        }
        return cells;
    }

    /// <summary>
    /// Samples destinations inside the arena, preferring distance for retreat or proximity to the supplied melee ideal.
    /// If no sample passes the filters, preserves the original random-cell fallback.
    /// </summary>
    public Vector3Int ChooseRepositionTarget(Vector2 playerPos, bool wantDistance,
        float minRepositionDistance, float maxRepositionDistance, float repositionIdealPlayerDistance, float meleeRange)
    {
        if (_walkableCells.Count == 0) return GetCurrentCell();


        int bestScore = int.MinValue;
        Vector3Int best = _walkableCells[Random.Range(0, _walkableCells.Count)];

        int samples = Mathf.Min(32, _walkableCells.Count);
        float edgeKeep = 0.58f;
        for (int i = 0; i < samples; i++)
        {
            Vector3Int candidate = _walkableCells[Random.Range(0, _walkableCells.Count)];
            Vector2 candidateWorld = CellCenterWorld(candidate);
            if (_arenaClampReady)
            {
                float ax0 = _arenaMinX + _pivotHalfExtentsWorld.x + edgeKeep;
                float ax1 = _arenaMaxX - _pivotHalfExtentsWorld.x - edgeKeep;
                float ay0 = _arenaMinY + _pivotHalfExtentsWorld.y + edgeKeep;
                float ay1 = _arenaMaxY - _pivotHalfExtentsWorld.y - edgeKeep;
                if (candidateWorld.x < ax0 || candidateWorld.x > ax1 || candidateWorld.y < ay0 || candidateWorld.y > ay1)
                    continue;
            }
            float distPlayer = (candidateWorld - playerPos).magnitude;
            float distSelf = ((Vector2)transform.position - candidateWorld).magnitude;
            if (distSelf < minRepositionDistance) continue;
            if (distSelf > maxRepositionDistance) continue;

            int score = 0;
            if (wantDistance)
            {
                score = Mathf.RoundToInt(distPlayer * 10f);
            }
            else
            {
                float ideal = Mathf.Clamp(repositionIdealPlayerDistance, 0.8f, Mathf.Max(1f, meleeRange * 0.95f));
                score = Mathf.RoundToInt(1000f - Mathf.Abs(distPlayer - ideal) * 40f);
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }
}
