using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class WormBossController : EnemyBase
{
    private enum BossState
    {
        Idle,
        Repositioning,
        Telegraphing,
        Attacking,
        Recovering,
        Underground,
        Dead
    }

    private enum AttackType
    {
        LaserCross,
        Swipe,
        Dig,
        BurrowTrail
    }

    private enum LaserPattern
    {
        Plus,
        DiagonalX,
        Combined
    }

    [System.Serializable]
    private struct AttackNumbers
    {
        public float telegraphTime;
        public float activeTime;
        public float cooldownTime;
        public float damage;
    }

    [Header("Arena References")]
    [SerializeField] private Tilemap baseTilemap;
    [SerializeField] private Tilemap decorationTilemap;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Boss Movement")]
    [SerializeField] private float repositionSpeed = 2.4f;
    [SerializeField] private float preferredDistance = 3.2f;
    [SerializeField] private float repositionDuration = 1.2f;
    [SerializeField] private bool enablePassiveContactDamage = false;
    [SerializeField] private float contactDamage = 4f;
    [SerializeField] private float contactTickInterval = 0.65f;
    [SerializeField] private float perTargetContactGrace = 1.1f;

    [Header("Laser Attack")]
    [SerializeField] private AttackNumbers laser = new AttackNumbers
    {
        telegraphTime = 1.1f,
        activeTime = 0.26f,
        cooldownTime = 0.65f,
        damage = 24f
    };
    [SerializeField] private float laserTileRadius = 0.42f;
    [SerializeField] private int laserMaxRangeTiles = 8;

    [Header("Swipe Attack")]
    [SerializeField] private AttackNumbers swipe = new AttackNumbers
    {
        telegraphTime = 0.85f,
        activeTime = 0.18f,
        cooldownTime = 0.55f,
        damage = 18f
    };
    [SerializeField] private float swipeRange = 2.2f;
    [SerializeField] private float swipeHalfArcDegrees = 38f;
    [SerializeField] private int swipeChainAtLowHp = 2;

    [Header("Dig Attack")]
    [SerializeField] private AttackNumbers dig = new AttackNumbers
    {
        telegraphTime = 0.95f,
        activeTime = 0.2f,
        cooldownTime = 0.8f,
        damage = 30f
    };
    [SerializeField] private float digStrikeRadius = 0.65f;
    [SerializeField] private float digTravelTime = 0.7f;
    [SerializeField] private float digUndergroundAlpha = 0.15f;

    [Header("Extra Attack - Burrow Trail")]
    [SerializeField] private bool enableBurrowTrail = true;
    [SerializeField] private AttackNumbers burrowTrail = new AttackNumbers
    {
        telegraphTime = 0.7f,
        activeTime = 0.2f,
        cooldownTime = 0.7f,
        damage = 14f
    };
    [SerializeField] private int burrowTrailSteps = 5;
    [SerializeField] private float burrowTrailSpacing = 0.9f;
    [SerializeField] private float burrowTrailRadius = 0.45f;

    [Header("Phase Thresholds")]
    [SerializeField] private float phaseTwoThreshold = 0.7f;
    [SerializeField] private float phaseThreeThreshold = 0.35f;
    [SerializeField] private float phaseTwoAttackSpeedMultiplier = 1.1f;
    [SerializeField] private float phaseThreeAttackSpeedMultiplier = 1.25f;

    [Header("Telegraph Colors")]
    [SerializeField] private Color laserTelegraphColor = new Color(1f, 0.2f, 0.2f, 0.55f);
    [SerializeField] private Color laserDamageColor = new Color(1f, 0.35f, 0.1f, 0.85f);
    [SerializeField] private Color swipeTelegraphColor = new Color(1f, 0.65f, 0.2f, 0.5f);
    [SerializeField] private Color swipeDamageColor = new Color(1f, 0.8f, 0.2f, 0.85f);
    [SerializeField] private Color digTelegraphColor = new Color(0.45f, 0.95f, 1f, 0.58f);
    [SerializeField] private Color digDamageColor = new Color(0.55f, 1f, 1f, 0.92f);
    [SerializeField] private Color burrowTrailTelegraphColor = new Color(1f, 0.78f, 0.28f, 0.5f);
    [SerializeField] private Color burrowTrailDamageColor = new Color(1f, 0.92f, 0.35f, 0.9f);
    [SerializeField] private Color telegraphOutlineColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private float telegraphCellScale = 0.92f;
    [SerializeField] private float telegraphOutlineThickness = 0.075f;
    [SerializeField] private bool enableTelegraphParticles = false;
    [SerializeField] private float particleScaleMultiplier = 0.62f;
    [SerializeField] private int telegraphParticleCellCap = 9;

    [Header("UI")]
    [SerializeField] private BossHealthBarUI healthBarUI;
    [SerializeField] private string bossDisplayName = "Worm Core";

    private readonly List<Vector3Int> _walkableCells = new();
    private readonly HashSet<Vector3Int> _walkableCellSet = new();
    private readonly List<GameObject> _activeTelegraphs = new();
    private readonly Dictionary<int, float> _contactCooldownByTarget = new();

    private BossState _state = BossState.Idle;
    private AttackType _lastAttack = AttackType.LaserCross;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _mainCollider;
    private Coroutine _behaviorRoutine;
    private bool _invulnerable;
    private float _nextContactDamageTime;
    private float _cachedBaseAlpha = 1f;
    private int _lastObservedPhase = 1;

    private static Sprite s_whiteSprite;
    private static Material s_telegraphMaterial;

    private int CurrentPhase
    {
        get
        {
            if (HealthNormalized <= phaseThreeThreshold) return 3;
            if (HealthNormalized <= phaseTwoThreshold) return 2;
            return 1;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _mainCollider = GetComponent<Collider2D>();
        if (_spriteRenderer) _cachedBaseAlpha = _spriteRenderer.color.a;
        ResolveSceneReferences();
        CacheWalkableCells();
        EnsurePlayerDamageReceiver();
    }

    private void OnEnable()
    {
        _behaviorRoutine = StartCoroutine(BehaviorLoop());
    }

    private void OnDisable()
    {
        if (_behaviorRoutine != null)
            StopCoroutine(_behaviorRoutine);
        ClearTelegraphs();
    }

    private void Update()
    {
        if (IsDead || _state == BossState.Dead || !Player)
            return;

        if (Time.time >= _nextContactDamageTime)
        {
            _nextContactDamageTime = Time.time + contactTickInterval;
            if (enablePassiveContactDamage)
                TryContactDamage();
        }

        if (healthBarUI)
            healthBarUI.SetHealth(HealthNormalized, CurrentHealth, MaxHealth, CurrentPhase);

        if (CurrentPhase != _lastObservedPhase)
        {
            _lastObservedPhase = CurrentPhase;
            OnPhaseChanged(_lastObservedPhase);
        }
    }

    private IEnumerator BehaviorLoop()
    {
        yield return null;
        BindHealthBar();

        while (!IsDead)
        {
            if (!Player)
            {
                yield return null;
                continue;
            }

            _state = BossState.Repositioning;
            yield return RepositionFor(repositionDuration);

            AttackType nextAttack = PickNextAttack();
            yield return ExecuteAttack(nextAttack);
        }
    }

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
                var cell = new Vector3Int(x, y, 0);
                if (!IsWalkableCell(cell)) continue;
                _walkableCells.Add(cell);
                _walkableCellSet.Add(cell);
            }
        }
    }

    private void ResolveSceneReferences()
    {
        if (!baseTilemap)
        {
            GameObject baseGo = GameObject.Find("Base");
            if (baseGo) baseTilemap = baseGo.GetComponent<Tilemap>();
        }

        if (!decorationTilemap)
        {
            GameObject decoGo = GameObject.Find("Decoration");
            if (decoGo) decorationTilemap = decoGo.GetComponent<Tilemap>();
        }

        if (!healthBarUI)
        {
            healthBarUI = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
            if (!healthBarUI)
            {
                var go = new GameObject("BossHealthBarUI");
                healthBarUI = go.AddComponent<BossHealthBarUI>();
            }
        }
    }

    private bool IsWalkableCell(Vector3Int cell)
    {
        bool inBase = baseTilemap && baseTilemap.HasTile(cell);
        bool inDeco = decorationTilemap && decorationTilemap.HasTile(cell);
        return inBase || inDeco;
    }

    private IEnumerator RepositionFor(float duration)
    {
        float endTime = Time.time + duration;
        float moveScale = CurrentPhase == 3 ? 1.25f : (CurrentPhase == 2 ? 1.1f : 1f);

        while (Time.time < endTime && !IsDead && Player)
        {
            Vector2 toPlayer = (Player.position - transform.position);
            float distance = toPlayer.magnitude;
            Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;

            if (distance > preferredDistance + 0.5f)
                Rb.AddForce(direction * repositionSpeed * moveScale, ForceMode2D.Force);
            else if (distance < preferredDistance - 0.45f)
                Rb.AddForce(-direction * repositionSpeed * 0.8f * moveScale, ForceMode2D.Force);

            yield return null;
        }
    }

    private AttackType PickNextAttack()
    {
        List<AttackType> candidates = BuildPhaseAttackPool(CurrentPhase);

        AttackType result = _lastAttack;
        int safety = 0;
        while (result == _lastAttack && safety < 8)
        {
            result = candidates[Random.Range(0, candidates.Count)];
            safety++;
        }

        _lastAttack = result;
        return result;
    }

    private List<AttackType> BuildPhaseAttackPool(int phase)
    {
        var pool = new List<AttackType>();
        if (phase <= 1)
        {
            pool.Add(AttackType.Swipe);
            pool.Add(AttackType.LaserCross);
            pool.Add(AttackType.Dig);
            return pool;
        }

        if (phase == 2)
        {
            pool.Add(AttackType.Swipe);
            pool.Add(AttackType.LaserCross);
            pool.Add(AttackType.Dig);
            if (enableBurrowTrail) pool.Add(AttackType.BurrowTrail);
            return pool;
        }

        pool.Add(AttackType.Swipe);
        pool.Add(AttackType.LaserCross);
        pool.Add(AttackType.Dig);
        if (enableBurrowTrail) pool.Add(AttackType.BurrowTrail);
        return pool;
    }

    private IEnumerator ExecuteAttack(AttackType attack)
    {
        switch (attack)
        {
            case AttackType.LaserCross:
                yield return DoLaserCrossAttack();
                break;
            case AttackType.Swipe:
                yield return DoSwipeAttack();
                break;
            case AttackType.Dig:
                yield return DoDigAttack();
                break;
            case AttackType.BurrowTrail:
                yield return DoBurrowTrailAttack();
                break;
        }
    }

    private IEnumerator DoLaserCrossAttack()
    {
        _state = BossState.Telegraphing;
        var cells = GetLaserCellsForCurrentPhase();
        SpawnLaserCrossTelegraph(cells, laserTelegraphColor, laser.telegraphTime, true, 8.8f, 0.28f, 0.82f, 0.65f, 1f);
        yield return WaitWithPhaseSpeed(laser.telegraphTime);

        _state = BossState.Attacking;
        SpawnLaserCrossTelegraph(cells, laserDamageColor, laser.activeTime, false, 0f, 1f, 1f, 1f, 1f);
        SpawnLaserBeamVfx(cells, laserDamageColor);
        DealDamageOnCells(cells, laserTileRadius, laser.damage);
        yield return WaitWithPhaseSpeed(laser.activeTime);

        _state = BossState.Recovering;
        ClearTelegraphs();
        yield return WaitWithPhaseSpeed(laser.cooldownTime);
    }

    private IEnumerator DoSwipeAttack()
    {
        int swipes = CurrentPhase >= 3 ? Mathf.Max(2, swipeChainAtLowHp) : 1;
        Vector2 toPlayer = Player ? ((Vector2)(Player.position - transform.position)).normalized : Vector2.right;
        if (toPlayer.sqrMagnitude <= 0.0001f) toPlayer = Vector2.right;

        for (int i = 0; i < swipes; i++)
        {
            _state = BossState.Telegraphing;
            var cells = GetArcCells(transform.position, toPlayer, swipeRange, swipeHalfArcDegrees);
            SpawnSwipeTelegraph(cells, swipeTelegraphColor, swipe.telegraphTime, true, 6.2f, 0.24f, 0.74f, 0.58f, 1f);
            yield return WaitWithPhaseSpeed(swipe.telegraphTime);

            _state = BossState.Attacking;
            SpawnSwipeTelegraph(cells, swipeDamageColor, swipe.activeTime, false, 0f, 1f, 1f, 1f, 1f);
            SpawnSwipeVfx(toPlayer, swipeDamageColor);
            DealDamageOnCells(cells, 0.45f, swipe.damage);
            yield return WaitWithPhaseSpeed(swipe.activeTime);

            ClearTelegraphs();
            toPlayer = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 35f : -35f) * toPlayer;
        }

        _state = BossState.Recovering;
        yield return WaitWithPhaseSpeed(swipe.cooldownTime);
    }

    private IEnumerator DoDigAttack()
    {
        _state = BossState.Underground;
        SetUndergroundVisuals(true);

        Vector3Int targetCell = GetNearestValidCellToPlayer();
        Vector3 targetWorld = CellCenterWorld(targetCell);
        float startTime = Time.time;
        Vector3 startPos = transform.position;

        while (Time.time < startTime + digTravelTime)
        {
            float t = Mathf.InverseLerp(startTime, startTime + digTravelTime, Time.time);
            transform.position = Vector3.Lerp(startPos, targetWorld, t);
            yield return null;
        }
        transform.position = targetWorld;

        _state = BossState.Telegraphing;
        List<Vector3Int> strikeCells = GetCellsInRadius(targetWorld, digStrikeRadius + 0.45f);
        SpawnTelegraphCells(strikeCells, digTelegraphColor, 1.02f, dig.telegraphTime, true, 4.1f, 0.22f, 0.68f, 0.55f, 1f);
        yield return WaitWithPhaseSpeed(dig.telegraphTime);

        _state = BossState.Attacking;
        SetUndergroundVisuals(false);
        SpawnTelegraphCells(strikeCells, digDamageColor, 1.08f, dig.activeTime, false, 0f, 1f, 1f, 1f, 1f);
        SpawnDigVfx(strikeCells, digDamageColor);
        DealDamageOnCells(strikeCells, digStrikeRadius, dig.damage);
        yield return WaitWithPhaseSpeed(dig.activeTime);

        ClearTelegraphs();
        _state = BossState.Recovering;
        yield return WaitWithPhaseSpeed(dig.cooldownTime);
    }

    private IEnumerator DoBurrowTrailAttack()
    {
        if (!Player)
            yield break;

        Vector2 baseDir = ((Vector2)(Player.position - transform.position)).normalized;
        if (baseDir.sqrMagnitude <= 0.0001f) baseDir = Vector2.right;

        var cells = new List<Vector3Int>();
        for (int i = 1; i <= burrowTrailSteps; i++)
        {
            Vector2 pos = (Vector2)transform.position + baseDir * (burrowTrailSpacing * i);
            cells.Add(GetNearestWalkableCell(pos));
        }

        _state = BossState.Telegraphing;
        SpawnTelegraphCells(cells, burrowTrailTelegraphColor, 1.25f, burrowTrail.telegraphTime, true, 7.4f, 0.2f, 0.7f, 0.55f, 1f);
        yield return WaitWithPhaseSpeed(burrowTrail.telegraphTime);

        _state = BossState.Attacking;
        SpawnTelegraphCells(cells, burrowTrailDamageColor, 1.6f, burrowTrail.activeTime, false, 0f, 1f, 1f, 1f, 1f);
        DealDamageOnCells(cells, burrowTrailRadius, burrowTrail.damage);
        yield return WaitWithPhaseSpeed(burrowTrail.activeTime);

        ClearTelegraphs();
        _state = BossState.Recovering;
        yield return WaitWithPhaseSpeed(burrowTrail.cooldownTime);
    }

    private void DealDamageOnCells(List<Vector3Int> cells, float radius, float damage)
    {
        if (cells == null || cells.Count == 0) return;

        var dealt = new HashSet<IDamageable>();
        foreach (var cell in cells)
        {
            Vector2 center = CellCenterWorld(cell);
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, playerLayerMask);
            foreach (var hit in hits)
            {
                if (!hit) continue;
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || dealt.Contains(damageable)) continue;

                Vector2 knockbackDir = ((Vector2)hit.bounds.center - center).normalized;
                if (knockbackDir.sqrMagnitude <= 0.0001f) knockbackDir = Vector2.up;
                damageable.TakeHit(damage, knockbackDir, 1.25f);
                dealt.Add(damageable);
            }
        }
    }

    private void TryContactDamage()
    {
        if (!Player || _mainCollider == null) return;
        Collider2D[] overlaps = new Collider2D[8];
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = playerLayerMask,
            useTriggers = true
        };

        int count = _mainCollider.Overlap(filter, overlaps);
        for (int i = 0; i < count; i++)
        {
            var hit = overlaps[i];
            if (!hit) continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;
            Component damageComponent = damageable as Component;
            if (damageComponent == null) continue;
            int id = damageComponent.GetInstanceID();
            if (_contactCooldownByTarget.TryGetValue(id, out float nextAllowed) && Time.time < nextAllowed)
                continue;

            Vector2 dir = ((Vector2)hit.bounds.center - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude <= 0.0001f) dir = Vector2.up;
            damageable.TakeHit(contactDamage, dir, 1.1f);
            _contactCooldownByTarget[id] = Time.time + perTargetContactGrace;
            break;
        }
    }

    private Vector3Int GetCurrentCell()
    {
        Tilemap source = baseTilemap ? baseTilemap : decorationTilemap;
        if (!source) return Vector3Int.zero;
        return source.WorldToCell(transform.position);
    }

    private Vector3Int GetNearestValidCellToPlayer()
    {
        if (!Player) return GetCurrentCell();
        return GetNearestWalkableCell(Player.position);
    }

    private Vector3Int GetNearestWalkableCell(Vector2 worldPoint)
    {
        if (_walkableCells.Count == 0) return GetCurrentCell();

        Tilemap source = baseTilemap ? baseTilemap : decorationTilemap;
        Vector3Int direct = source.WorldToCell(worldPoint);
        if (_walkableCellSet.Contains(direct)) return direct;

        float best = float.PositiveInfinity;
        Vector3Int bestCell = _walkableCells[0];
        for (int i = 0; i < _walkableCells.Count; i++)
        {
            Vector2 c = CellCenterWorld(_walkableCells[i]);
            float d = (c - worldPoint).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestCell = _walkableCells[i];
            }
        }
        return bestCell;
    }

    private List<Vector3Int> GetCrossCells(Vector3Int origin, int maxRange)
    {
        var result = new List<Vector3Int> { origin };
        var dirs = new[]
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down
        };

        foreach (Vector3Int dir in dirs)
        {
            for (int i = 1; i <= maxRange; i++)
            {
                Vector3Int cell = origin + dir * i;
                if (!_walkableCellSet.Contains(cell))
                    break;
                result.Add(cell);
            }
        }
        return result;
    }

    private List<Vector3Int> GetDiagonalCrossCells(Vector3Int origin, int maxRange)
    {
        var result = new List<Vector3Int> { origin };
        var dirs = new[]
        {
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(-1, -1, 0)
        };

        foreach (Vector3Int dir in dirs)
        {
            for (int i = 1; i <= maxRange; i++)
            {
                Vector3Int cell = origin + dir * i;
                if (!_walkableCellSet.Contains(cell))
                    break;
                result.Add(cell);
            }
        }

        return result;
    }

    private List<Vector3Int> GetLaserCellsForCurrentPhase()
    {
        Vector3Int origin = GetCurrentCell();
        if (CurrentPhase <= 1)
            return GetCrossCells(origin, laserMaxRangeTiles);

        LaserPattern pattern = CurrentPhase == 2
            ? (Random.value < 0.5f ? LaserPattern.Plus : LaserPattern.DiagonalX)
            : (Random.value < 0.35f ? LaserPattern.Combined : (Random.value < 0.5f ? LaserPattern.Plus : LaserPattern.DiagonalX));

        List<Vector3Int> plus = GetCrossCells(origin, laserMaxRangeTiles);
        if (pattern == LaserPattern.Plus) return plus;

        List<Vector3Int> diag = GetDiagonalCrossCells(origin, laserMaxRangeTiles);
        if (pattern == LaserPattern.DiagonalX) return diag;

        var combined = new HashSet<Vector3Int>(plus);
        combined.UnionWith(diag);
        return new List<Vector3Int>(combined);
    }

    private List<Vector3Int> GetArcCells(Vector2 center, Vector2 forward, float range, float halfArcDegrees)
    {
        var cells = new List<Vector3Int>();
        float cosThreshold = Mathf.Cos(halfArcDegrees * Mathf.Deg2Rad);
        foreach (var cell in _walkableCells)
        {
            Vector2 world = CellCenterWorld(cell);
            Vector2 dir = world - center;
            float mag = dir.magnitude;
            if (mag <= 0.001f || mag > range) continue;

            Vector2 dirN = dir / mag;
            if (Vector2.Dot(forward, dirN) >= cosThreshold)
                cells.Add(cell);
        }
        return cells;
    }

    private List<Vector3Int> GetCellsInRadius(Vector2 center, float radius)
    {
        var cells = new List<Vector3Int>();
        float radiusSq = radius * radius;
        for (int i = 0; i < _walkableCells.Count; i++)
        {
            Vector3Int cell = _walkableCells[i];
            float d = (((Vector2)CellCenterWorld(cell)) - center).sqrMagnitude;
            if (d <= radiusSq)
                cells.Add(cell);
        }

        if (cells.Count == 0)
            cells.Add(GetNearestWalkableCell(center));
        return cells;
    }

    private Vector2 CellCenterWorld(Vector3Int cell)
    {
        Tilemap source = baseTilemap ? baseTilemap : decorationTilemap;
        if (!source) return transform.position;
        return source.GetCellCenterWorld(cell);
    }

    private void SpawnTelegraphCells(List<Vector3Int> cells, Color color, float scale, float lifetime, bool pulse,
        float pulseSpeed, float fillAlphaMin, float fillAlphaMax, float outlineAlphaMin, float outlineAlphaMax)
    {
        ClearTelegraphs();
        if (cells == null || cells.Count == 0) return;

        BuildTelegraphRegion(cells, color, scale, lifetime, pulse, pulseSpeed, fillAlphaMin, fillAlphaMax, outlineAlphaMin, outlineAlphaMax);
    }

    private void BuildTelegraphRegion(List<Vector3Int> cells, Color color, float scale, float lifetime, bool pulse,
        float pulseSpeed, float fillAlphaMin, float fillAlphaMax, float outlineAlphaMin, float outlineAlphaMax)
    {
        if (!TryGetCellBasis(out Vector2 a, out Vector2 b))
            return;

        var cellSet = new HashSet<Vector3Int>(cells);
        Mesh fillMesh = BuildFillMesh(cells, a, b, scale);
        Mesh outlineMesh = BuildOutlineMesh(cellSet, a, b, scale, telegraphOutlineThickness);

        var root = new GameObject("BossTelegraphRegion");
        root.transform.position = new Vector3(0f, 0f, transform.position.z + 0.05f);
        _activeTelegraphs.Add(root);

        if (fillMesh != null)
        {
            var fillGo = new GameObject("FillMesh");
            fillGo.transform.SetParent(root.transform, false);
            var mf = fillGo.AddComponent<MeshFilter>();
            mf.sharedMesh = fillMesh;
            var mr = fillGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateTelegraphMaterial(color);
            mr.sortingOrder = 76;
            _activeTelegraphs.Add(fillGo);
        }

        if (outlineMesh != null)
        {
            var outlineGo = new GameObject("OutlineMesh");
            outlineGo.transform.SetParent(root.transform, false);
            var mf = outlineGo.AddComponent<MeshFilter>();
            mf.sharedMesh = outlineMesh;
            var mr = outlineGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateTelegraphMaterial(telegraphOutlineColor);
            mr.sortingOrder = 77;
            _activeTelegraphs.Add(outlineGo);
        }

        if (pulse)
            StartCoroutine(PulseTelegraph(root, color, telegraphOutlineColor, lifetime, pulseSpeed, fillAlphaMin, fillAlphaMax, outlineAlphaMin, outlineAlphaMax));
    }

    private void SpawnLaserCrossTelegraph(List<Vector3Int> cells, Color color, float lifetime, bool pulse,
        float pulseSpeed, float fillAlphaMin, float fillAlphaMax, float outlineAlphaMin, float outlineAlphaMax)
    {
        SpawnTelegraphCells(cells, color, telegraphCellScale, lifetime, pulse, pulseSpeed, fillAlphaMin, fillAlphaMax, outlineAlphaMin, outlineAlphaMax);
    }

    private void SpawnSwipeTelegraph(List<Vector3Int> cells, Color color, float lifetime, bool pulse,
        float pulseSpeed, float fillAlphaMin, float fillAlphaMax, float outlineAlphaMin, float outlineAlphaMax)
    {
        SpawnTelegraphCells(cells, color, telegraphCellScale, lifetime, pulse, pulseSpeed, fillAlphaMin, fillAlphaMax, outlineAlphaMin, outlineAlphaMax);
    }

    private IEnumerator PulseTelegraph(GameObject telegraphRoot, Color fillBase, Color outlineBase, float duration, float pulseSpeed,
        float fillAlphaMin, float fillAlphaMax, float outlineAlphaMin, float outlineAlphaMax)
    {
        if (!telegraphRoot || duration <= 0.001f)
            yield break;

        var renderers = telegraphRoot.GetComponentsInChildren<MeshRenderer>();
        float end = Time.time + duration;
        while (Time.time < end && telegraphRoot)
        {
            float t = Mathf.InverseLerp(end - duration, end, Time.time);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * Mathf.Max(0.25f, pulseSpeed));
            float ramp = Mathf.Lerp(0.75f, 1f, t);

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r || !r.sharedMaterial) continue;

                if (r.gameObject.name.Contains("Outline"))
                {
                    Color c = outlineBase;
                    c.a *= Mathf.Lerp(outlineAlphaMin, outlineAlphaMax, pulse) * ramp;
                    r.sharedMaterial.color = c;
                }
                else
                {
                    Color c = fillBase;
                    c.a *= Mathf.Lerp(fillAlphaMin, fillAlphaMax, pulse) * ramp;
                    r.sharedMaterial.color = c;
                }
            }
            yield return null;
        }
    }

    private bool TryGetCellBasis(out Vector2 basisRight, out Vector2 basisUp)
    {
        basisRight = Vector2.right * 0.5f;
        basisUp = Vector2.up * 0.5f;
        if (_walkableCells.Count == 0) return false;

        Vector3Int sample = _walkableCells[0];
        Vector2 center = CellCenterWorld(sample);
        basisRight = CellCenterWorld(sample + Vector3Int.right) - center;
        basisUp = CellCenterWorld(sample + Vector3Int.up) - center;

        if (basisRight.sqrMagnitude < 0.0001f) basisRight = new Vector2(0.5f, 0f);
        if (basisUp.sqrMagnitude < 0.0001f) basisUp = new Vector2(0f, 0.5f);
        return true;
    }

    private Mesh BuildFillMesh(List<Vector3Int> cells, Vector2 basisRight, Vector2 basisUp, float scale)
    {
        if (cells == null || cells.Count == 0) return null;

        var vertices = new List<Vector3>(cells.Count * 4);
        var triangles = new List<int>(cells.Count * 6);
        Vector2 r = basisRight * 0.5f * scale;
        Vector2 u = basisUp * 0.5f * scale;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2 c = CellCenterWorld(cells[i]);
            Vector3 v0 = c - r - u;
            Vector3 v1 = c + r - u;
            Vector3 v2 = c + r + u;
            Vector3 v3 = c - r + u;

            int idx = vertices.Count;
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            triangles.Add(idx + 0);
            triangles.Add(idx + 1);
            triangles.Add(idx + 2);
            triangles.Add(idx + 0);
            triangles.Add(idx + 2);
            triangles.Add(idx + 3);
        }

        var mesh = new Mesh { name = "TelegraphFillMesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh BuildOutlineMesh(HashSet<Vector3Int> cells, Vector2 basisRight, Vector2 basisUp, float scale, float thickness)
    {
        if (cells == null || cells.Count == 0) return null;

        var vertices = new List<Vector3>(cells.Count * 16);
        var triangles = new List<int>(cells.Count * 24);
        Vector2 r = basisRight * 0.5f * scale;
        Vector2 u = basisUp * 0.5f * scale;

        foreach (Vector3Int cell in cells)
        {
            Vector2 c = CellCenterWorld(cell);
            Vector2 v0 = c - r - u;
            Vector2 v1 = c + r - u;
            Vector2 v2 = c + r + u;
            Vector2 v3 = c - r + u;

            if (!cells.Contains(cell + Vector3Int.left)) AddEdgeQuad(v0, v3, thickness, vertices, triangles);
            if (!cells.Contains(cell + Vector3Int.right)) AddEdgeQuad(v1, v2, thickness, vertices, triangles);
            if (!cells.Contains(cell + Vector3Int.down)) AddEdgeQuad(v0, v1, thickness, vertices, triangles);
            if (!cells.Contains(cell + Vector3Int.up)) AddEdgeQuad(v3, v2, thickness, vertices, triangles);
        }

        var mesh = new Mesh { name = "TelegraphOutlineMesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddEdgeQuad(Vector2 p0, Vector2 p1, float thickness, List<Vector3> vertices, List<int> triangles)
    {
        Vector2 edge = p1 - p0;
        if (edge.sqrMagnitude < 0.00001f) return;

        Vector2 n = new Vector2(-edge.y, edge.x).normalized * (thickness * 0.5f);
        Vector3 q0 = p0 - n;
        Vector3 q1 = p0 + n;
        Vector3 q2 = p1 + n;
        Vector3 q3 = p1 - n;

        int idx = vertices.Count;
        vertices.Add(q0);
        vertices.Add(q1);
        vertices.Add(q2);
        vertices.Add(q3);

        triangles.Add(idx + 0);
        triangles.Add(idx + 1);
        triangles.Add(idx + 2);
        triangles.Add(idx + 0);
        triangles.Add(idx + 2);
        triangles.Add(idx + 3);
    }

    private Material CreateTelegraphMaterial(Color color)
    {
        if (!s_telegraphMaterial)
        {
            Shader shader = Shader.Find("Sprites/Default");
            s_telegraphMaterial = new Material(shader);
        }

        var m = new Material(s_telegraphMaterial) { color = color };
        return m;
    }

    private void SpawnLaserBeamVfx(List<Vector3Int> cells, Color color)
    {
        if (cells == null || cells.Count == 0) return;

        Vector3Int center = cells[0];
        int minX = center.x;
        int maxX = center.x;
        int minY = center.y;
        int maxY = center.y;
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int c = cells[i];
            if (c.y == center.y)
            {
                minX = Mathf.Min(minX, c.x);
                maxX = Mathf.Max(maxX, c.x);
            }

            if (c.x == center.x)
            {
                minY = Mathf.Min(minY, c.y);
                maxY = Mathf.Max(maxY, c.y);
            }
        }

        SpawnAttackStrip(CellCenterWorld(new Vector3Int(minX, center.y, 0)),
            CellCenterWorld(new Vector3Int(maxX, center.y, 0)),
            0.52f, new Color(color.r, color.g, color.b, 0.9f), 0.22f, "LaserBeamH");
        SpawnAttackStrip(CellCenterWorld(new Vector3Int(center.x, minY, 0)),
            CellCenterWorld(new Vector3Int(center.x, maxY, 0)),
            0.52f, new Color(color.r, color.g, color.b, 0.9f), 0.22f, "LaserBeamV");

    }

    private void SpawnSwipeVfx(Vector2 direction, Color color)
    {
        var go = new GameObject("SwipeVFX");
        go.transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z + 0.08f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        SpawnAttackStrip(transform.position, (Vector2)transform.position + direction.normalized * (swipeRange * 1.05f),
            0.44f, new Color(color.r, color.g, color.b, 0.72f), 0.18f, "SwipeSlash");
        Destroy(go, 0.01f);
    }

    private void SpawnDigVfx(List<Vector3Int> cells, Color color)
    {
        if (cells == null || cells.Count == 0) return;

        int count = Mathf.Min(cells.Count, 6);
        for (int i = 0; i < count; i++)
        {
            Vector2 center = CellCenterWorld(cells[i]);
            SpawnAttackStrip(center + new Vector2(-0.22f, 0f), center + new Vector2(0.22f, 0f),
                0.36f, new Color(color.r, color.g, color.b, 0.62f), 0.2f, "DigBurst");
        }
    }

    private void SpawnAttackStrip(Vector2 start, Vector2 end, float width, Color color, float lifetime, string name)
    {
        Vector2 delta = end - start;
        float length = Mathf.Max(0.1f, delta.magnitude);
        Vector2 center = (start + end) * 0.5f;

        var go = new GameObject(name);
        go.transform.position = new Vector3(center.x, center.y, transform.position.z + 0.09f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(length, width, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        ApplySpriteSorting(sr, 22);
        sr.color = color;

        StartCoroutine(FadeAndDestroyStrip(sr, lifetime));
    }

    private IEnumerator FadeAndDestroyStrip(SpriteRenderer sr, float lifetime)
    {
        if (!sr || lifetime <= 0.001f)
            yield break;

        Color baseColor = sr.color;
        float end = Time.time + lifetime;
        while (Time.time < end && sr)
        {
            float t = Mathf.InverseLerp(end - lifetime, end, Time.time);
            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, t);
            sr.color = c;
            yield return null;
        }

        if (sr)
            Destroy(sr.gameObject);
    }

    private void ApplySpriteSorting(SpriteRenderer renderer, int extraOrder)
    {
        if (!renderer) return;
        if (_spriteRenderer)
        {
            renderer.sortingLayerID = _spriteRenderer.sortingLayerID;
            renderer.sortingOrder = _spriteRenderer.sortingOrder + extraOrder;
        }
        else
        {
            renderer.sortingOrder = extraOrder;
        }
    }

    private void ClearTelegraphs()
    {
        for (int i = 0; i < _activeTelegraphs.Count; i++)
        {
            if (_activeTelegraphs[i]) Destroy(_activeTelegraphs[i]);
        }
        _activeTelegraphs.Clear();
    }

    private void SetUndergroundVisuals(bool underground)
    {
        _invulnerable = underground;
        if (_mainCollider) _mainCollider.enabled = !underground;

        if (_spriteRenderer)
        {
            Color c = _spriteRenderer.color;
            c.a = underground ? digUndergroundAlpha : _cachedBaseAlpha;
            _spriteRenderer.color = c;
        }
    }

    private float PhaseSpeedMultiplier()
    {
        if (CurrentPhase == 3) return phaseThreeAttackSpeedMultiplier;
        if (CurrentPhase == 2) return phaseTwoAttackSpeedMultiplier;
        return 1f;
    }

    private IEnumerator WaitWithPhaseSpeed(float seconds)
    {
        float scaled = seconds / Mathf.Max(0.01f, PhaseSpeedMultiplier());
        yield return new WaitForSeconds(scaled);
    }

    public override void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce, DamageContext context = default)
    {
        if (_invulnerable) return;
        base.TakeHit(damage, knockbackDirection, knockbackForce, context);
    }

    protected override void Die(DamageContext context = default)
    {
        _state = BossState.Dead;
        if (healthBarUI)
            healthBarUI.HideBar();
        ClearTelegraphs();
        base.Die(context);
    }

    private void BindHealthBar()
    {
        if (!healthBarUI) return;
        healthBarUI.Bind(this, bossDisplayName, phaseTwoThreshold, phaseThreeThreshold);
        healthBarUI.SetHealth(HealthNormalized, CurrentHealth, MaxHealth, CurrentPhase);
        healthBarUI.NotifyPhaseChange(CurrentPhase);
        _lastObservedPhase = CurrentPhase;
    }

    private void OnPhaseChanged(int phase)
    {
        if (healthBarUI)
            healthBarUI.NotifyPhaseChange(phase);

        Color phaseColor = phase == 2
            ? new Color(1f, 0.62f, 0.2f, 0.95f)
            : new Color(1f, 0.9f, 0.28f, 0.95f);

        List<Vector3Int> ringCells = GetCellsInRadius(transform.position, 1.3f + phase * 0.3f);
        SpawnTelegraphCells(ringCells, phaseColor, 1.15f, 0.24f, false, 0f, 1f, 1f, 1f, 1f);
        SpawnAttackStrip((Vector2)transform.position + new Vector2(-0.9f, 0f),
            (Vector2)transform.position + new Vector2(0.9f, 0f), 0.5f, phaseColor, 0.28f, "PhaseShiftVFX");
    }

    private void EnsurePlayerDamageReceiver()
    {
        if (!Player) return;
        var damageable = Player.GetComponent<IDamageable>();
        if (damageable != null) return;
        if (!Player.GetComponent<PlayerHealth>())
            Player.gameObject.AddComponent<PlayerHealth>();
    }

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite) return s_whiteSprite;
        Texture2D texture = Texture2D.whiteTexture;
        s_whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, swipeRange);
    }
}
