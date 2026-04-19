using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class WormBossController : EnemyBase
{
    public enum BossAttackType
    {
        Melee,
        Shoot,
        Dig,
        Laser
    }

    private enum InternalState
    {
        Initializing,
        Idle,
        Attacking,
        Stunned,
        Repositioning,
        Dead
    }

    private const string StateIdle = "BossWorm_Idle";
    private const string StateShoot = "BossWorm_Shoot";
    private const string StateMelee = "BossWorm_Melee";
    private const string StateDigging = "BossWorm_Digging";
    private const string StateRising = "BossWorm_Rising";
    private const string StateLaserCharge = "BossWorm_LaserCharge";
    private const string StateLaserFire = "BossWorm_LaserFire";
    private const string StatePhaseChange = "BossWorm_PhaseChange";
    private const string StateDamage = "BossWorm_Damage";
    private const string StateDying = "BossWorm_Dying";

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Tilemap baseTilemap;
    [SerializeField] private Tilemap decorationTilemap;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private BossHealthBarUI healthBarUI;
    [SerializeField] private GameObject postBossTeleporter;
    [SerializeField] private Transform postBossTeleporterPan;
    [SerializeField] private BossAudioManager bossAudioManager;

    [Header("Telegraph")]
    [SerializeField] private Sprite indicatorBaseSprite;
    [SerializeField] private Sprite indicatorImminentSprite;
    [SerializeField] private Color indicatorBaseColor = new Color(1f, 1f, 1f, 0.82f);
    [SerializeField] private Color indicatorImminentColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private int indicatorSortingOrder = 42;
    [SerializeField] private string indicatorSortingLayerName = "";

    [Header("Phase & shield")]
    [SerializeField] private float phaseTwoHp = 0.64f;
    [SerializeField] private float phaseThreeHp = 0.31f;

    [SerializeField] private float[] maxShieldByPhase = { 50f, 80f, 120f };
    [SerializeField] private float shieldRegenIdleSeconds = 5f;
    [SerializeField] private float shieldRegenFillSeconds = 5f;
    [SerializeField] private Color shieldPingColor = new Color(0.42f, 0.78f, 1f, 1f);
    [SerializeField] private float shieldPingDuration = 0.18f;
    [SerializeField] private float damageStunDuration = 0.32f;

    [Header("Shield break stun")]
    [SerializeField] private float shieldBreakStunDuration = 2f;
    [SerializeField] private float shieldBreakKnockbackForce = 4.05f;
    [SerializeField] private float shieldBreakCameraShake = 0.24f;
    [SerializeField] private float shieldBreakWobbleDegrees = 4.6f;
    [SerializeField] private float shieldBreakChildWobbleX = 0.058f;
    [SerializeField] private float shieldBreakChildWobbleY = 0.042f;
    [SerializeField] private float shieldBreakChildWobbleDegrees = 4.5f;

    [Header("Phase transition")]
    [SerializeField] private CameraController phaseTransitionCamera;
    [SerializeField] private float phaseTransitionShieldFillPhase2 = 0.95f;
    [SerializeField] private float phaseTransitionShieldFillPhase3 = 1.35f;
    [SerializeField] private float phaseTransitionLaserHoldAfterShieldPhase2 = 0.4f;
    [SerializeField] private float phaseTransitionLaserHoldAfterShieldPhase3 = 0.65f;
    [SerializeField] private float phaseTransitionShakeEnterPhase2 = 0.38f;
    [SerializeField] private float phaseTransitionShakeEnterPhase3 = 0.62f;
    [SerializeField] private float phaseTransitionShakeResume = 0.14f;

    [Header("Cutscene")]
    [SerializeField] private bool waitForExternalCutscene;

    [Header("Death cutscene")]
    [SerializeField] private float deathPanPlayerToBossSeconds = 1.25f;
    [SerializeField] private float deathHealthBarFadeSeconds = 1f;
    [SerializeField] private float deathHealthDrainSeconds = 0.45f;
    [SerializeField] private float deathPanBossToTeleporterSeconds = 1.25f;
    [SerializeField] private float deathTeleporterFadeSeconds = 1f;
    [SerializeField] private float deathPanTeleporterToPlayerSeconds = 1.25f;
    [SerializeField] private BossEscapeSequenceManager escapeSequenceManager;
    [Header("Movement")]
    [SerializeField] private float undergroundSpeed = 3.2f;
    [SerializeField] private float rubbleTrailInterval = 0.08f;
    [SerializeField] private Color rubbleColor = new Color(0.48f, 0.32f, 0.18f, 0.82f);
    [SerializeField] private int rubbleSortingOrder = 50;
    [SerializeField] private float[] idleTimeAfterAttackByPhase = { 2.0f, 1.4f, 0.9f };
    [SerializeField] private float[] repositionChanceByPhase = { 0.25f, 0.45f, 0.7f };
    [SerializeField] private float minRepositionDistance = 2.2f;
    [SerializeField] private float maxRepositionDistance = 6f;
    [SerializeField] private float repositionIdealPlayerDistance = 1.85f;
    [SerializeField] private float phaseThreeChainChance = 0.35f;
    [SerializeField] private float phaseThreeChainCooldownMultiplier = 1.35f;
    [Range(0f, 1f)]
    [SerializeField] private float shieldRegenSeekThreshold = 0.2f;
    [SerializeField] private float repositionEmergeDamage = 10f;
    [SerializeField] private float repositionEmergeRadius = 0.85f;
    [SerializeField] private float baseArenaEdgePadding = 0.52f;
    [SerializeField] private bool spriteFacesRightByDefault = false;
    [SerializeField] private float minFlipInterval = 0.25f;
    [SerializeField] private float flipThreshold = 0.25f;

    [Header("Combat")]
    [SerializeField] private float meleeRange = 2.2f;
    [SerializeField] private float meleeArcDegrees = 70f;
    [SerializeField] private float meleeDamage = 18f;
    [SerializeField] private float meleeFramesBeforeWait = 0.42f;
    [SerializeField] private float meleeChargeWait = 0.33f;
    [SerializeField] private float meleeStrikeDuration = 0.12f;
    [SerializeField] private float meleeRecoveryDuration = 0.13f;
    [SerializeField] private float meleeImminentFraction = 0.3f;

    [SerializeField] private GameObject bossProjectilePrefab;
    [SerializeField] private float shootFramesBeforeWait = 0.42f;
    [SerializeField] private float shootChargeWait = 0.33f;
    [SerializeField] private float shootRecoveryDuration = 0.25f;
    [SerializeField] private float shootSpreadPhase2 = 18f;
    [SerializeField] private float shootSpreadPhase3 = 22f;
    [SerializeField] private float shootBurstIntervalPhase3 = 0.18f;
    [SerializeField] private int shootBulletsPhase2 = 3;
    [SerializeField] private int shootBulletsPhase3 = 3;

    [SerializeField] private float digTrackDuration = 0.9f;
    [SerializeField] private float digLockDuration = 0.35f;
    [SerializeField] private float digStrikeRadius = 1.05f;
    [SerializeField] private float digDamage = 24f;
    [SerializeField] private float digRiseDamageNormalizedTime = 0.22f;

    [SerializeField] private GameObject bossLaserBeamPrefab;
    [SerializeField] private float laserChargeDuration = 0.66f;
    [SerializeField] private float laserFireDuration = 0.72f;
    [SerializeField] private float laserRecoveryDuration = 0.35f;
    [SerializeField] private float laserDamage = 22f;
    [SerializeField] private float laserBeamLength = 7.5f;
    [SerializeField] private float laserMinBeamLength = 0.35f;
    [SerializeField] private int laserStretchSegmentCount = 18;
    [SerializeField] private float laserSegmentStripeWidth = 0.42f;
    [SerializeField] private float laserTipHitRadius = 0.48f;
    [SerializeField] private float laserChargeStripeWidth = 0.55f;
    [SerializeField] private float laserSegmentImminentLead = 0.35f;
    [SerializeField] private float laserRowFadeDuration = 0.14f;
    [SerializeField] private Color laserBeamColor = new Color(1f, 0.3f, 0.2f, 0.95f);
    [SerializeField] private Vector2 laserMouthOffset = new Vector2(0.4f, 0.05f);
    [SerializeField] private float laserBetweenSweepPause = 0.22f;

    [SerializeField] private Color riseRubbleColor = new Color(0.55f, 0.38f, 0.22f, 0.92f);

    private readonly List<Vector3Int> _walkableCells = new();
    private readonly HashSet<Vector3Int> _walkableCellSet = new();
    private readonly List<Vector3Int> _pathScratch = new();

    private float _arenaMinX;
    private float _arenaMaxX;
    private float _arenaMinY;
    private float _arenaMaxY;
    private bool _arenaClampReady;
    private Vector2 _pivotHalfExtentsWorld;

    private BossAudioManager _bossAudioCached;

    private Rigidbody2D _rb;
    private Collider2D _mainCollider;
    private float _baseAlpha = 1f;
    private Vector3 _baseScale = Vector3.one;
    private int _lastObservedPhase = 1;
    private int _sortingLayerId;

    private float _currentShield;
    private float _lastShieldDamageTime;
    private bool _shieldBroken;

    private Coroutine _behaviorRoutine;
    private Coroutine _attackRoutine;
    private Coroutine _damageRoutine;
    private readonly List<BossAttackIndicator> _activeIndicators = new();
    private BossAttackIndicator _trackedIndicator;
    private InternalState _state = InternalState.Initializing;
    private BossAttackType _currentAttack;
    private bool _attackActive;
    private BossAttackType _lastAttack = BossAttackType.Melee;
    private bool _digInvulnerable;
    private bool _immuneDuringDigMove;
    private bool _isStunned;
    private float _lastFlipTime = -999f;

    private Coroutine _shieldPingRoutine;
    private Color _shieldPingBaseColor = Color.white;
    private GameObject _activeBossLaserBeam;
    private bool _wormAnimatorHeldOnLaserFireFrame;
    private bool _wormAnimatorHeldOnPhaseChangeFrame;
    private readonly List<BossAttackIndicator> _laserStretchSegments = new();
    private bool[] _laserRowFadeStarted;

    private bool _phaseTransitionActive;
    private Coroutine _phaseTransitionRoutine;
    private bool _phaseChangePending;
    private int _queuedNewPhase = 1;
    private bool _introActive;

    private Coroutine _repositionRoutine;

    private Coroutine _shieldBreakFeedbackRoutine;
    private Vector3 _spriteVisualBaseLocal;
    private Quaternion _spriteVisualBaseLocalRot = Quaternion.identity;
    private bool _spriteVisualIsChild;
    private float _deathHealthBarDrainFromNormalized = 1f;

    public int CurrentPhase
    {
        get
        {
            if (HealthNormalized <= phaseThreeHp) return 3;
            if (HealthNormalized <= phaseTwoHp) return 2;
            return 1;
        }
    }

    public float CurrentShieldNormalized => GetMaxShieldForCurrentPhase() <= 0.0001f ? 0f : Mathf.Clamp01(_currentShield / GetMaxShieldForCurrentPhase());

    public bool WaitForExternalCutscene => waitForExternalCutscene;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        _mainCollider = GetComponent<Collider2D>();
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer)
        {
            _baseAlpha = spriteRenderer.color.a;
            _shieldPingBaseColor = spriteRenderer.color;
            _spriteVisualBaseLocal = spriteRenderer.transform.localPosition;
            _spriteVisualBaseLocalRot = spriteRenderer.transform.localRotation;
            _spriteVisualIsChild = spriteRenderer.transform != transform;
        }
        _baseScale = transform.localScale;
        if (!string.IsNullOrEmpty(indicatorSortingLayerName))
            _sortingLayerId = SortingLayer.NameToID(indicatorSortingLayerName);
        else if (spriteRenderer)
            _sortingLayerId = spriteRenderer.sortingLayerID;

        ResolveSceneReferences();
        CacheBossPivotHalfExtentsWorld();
        CacheBaseArenaWorldBounds();
        CacheWalkableCells();
        _currentShield = GetMaxShieldForCurrentPhase();
    }

    private BossAudioManager ResolveBossAudio()
    {
        if (bossAudioManager)
            return bossAudioManager;
        if (_bossAudioCached)
            return _bossAudioCached;
        _bossAudioCached = FindFirstObjectByType<BossAudioManager>();
        return _bossAudioCached;
    }

    private void OnEnable()
    {
        if (waitForExternalCutscene)
        {
            _state = InternalState.Initializing;
            _introActive = true;
            return;
        }

        _state = InternalState.Idle;
        PlayAnimState(StateIdle);
        _behaviorRoutine = StartCoroutine(BehaviorLoop());
    }

    private void OnDisable()
    {
        if (_introActive)
        {
            _introActive = false;
            CameraController cam = phaseTransitionCamera ? phaseTransitionCamera : FindFirstObjectByType<CameraController>();
            if (cam)
                cam.LockToPlayer();
        }
        if (_behaviorRoutine != null) StopCoroutine(_behaviorRoutine);
        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        if (_damageRoutine != null) StopCoroutine(_damageRoutine);
        if (_shieldPingRoutine != null) StopCoroutine(_shieldPingRoutine);
        if (_repositionRoutine != null) StopCoroutine(_repositionRoutine);
        _repositionRoutine = null;
        if (_shieldBreakFeedbackRoutine != null) StopCoroutine(_shieldBreakFeedbackRoutine);
        _shieldBreakFeedbackRoutine = null;
        DisableStunVisual();
        ResetSpriteVisualLocalIfChild();
        ReleaseWormLaserFireHoldPose();
        ReleasePhaseChangeHoldPose();
        if (_activeBossLaserBeam)
        {
            Destroy(_activeBossLaserBeam);
            _activeBossLaserBeam = null;
        }
        DestroyActiveIndicator();
        _attackActive = false;
        _digInvulnerable = false;
        _immuneDuringDigMove = false;
        if (_phaseTransitionRoutine != null)
        {
            StopCoroutine(_phaseTransitionRoutine);
            _phaseTransitionRoutine = null;
        }
        _phaseTransitionActive = false;
        _phaseChangePending = false;
    }

    private void Update()
    {
        if (IsDead || _state == InternalState.Dead) return;
        if (_introActive) return;

        float maxShield = GetMaxShieldForCurrentPhase();
        float fillDur = Mathf.Max(0.1f, shieldRegenFillSeconds);
        float regenPerSec = maxShield / fillDur;
        if (!_phaseTransitionActive && !_shieldBroken && maxShield > 0f && _currentShield < maxShield
            && Time.time - _lastShieldDamageTime >= shieldRegenIdleSeconds)
        {
            _currentShield = Mathf.Min(maxShield, _currentShield + regenPerSec * Time.deltaTime);
            UpdateShieldUI();
        }

        if (healthBarUI)
            healthBarUI.SetHealth(HealthNormalized);

        if (CurrentPhase != _lastObservedPhase)
        {
            if (!_phaseChangePending)
                _phaseChangePending = true;
            _queuedNewPhase = CurrentPhase;
        }

        if (_phaseChangePending && _phaseTransitionRoutine == null && CanStartPhaseTransitionNow())
        {
            if (_shieldBreakFeedbackRoutine != null)
            {
                StopCoroutine(_shieldBreakFeedbackRoutine);
                _shieldBreakFeedbackRoutine = null;
            }
            ResetSpriteVisualLocalIfChild();
            DisableStunVisual();
            _lastObservedPhase = _queuedNewPhase;
            _phaseTransitionRoutine = StartCoroutine(PhaseTransitionSequence(_queuedNewPhase));
        }
    }

    private bool CanStartPhaseTransitionNow()
    {
        if (_introActive) return false;
        if (_phaseTransitionActive) return false;
        return true;
    }

    public void SetIntroCutsceneActive(bool active)
    {
        _introActive = active;
    }

    public void StartCombatAfterCutscene()
    {
        if (_behaviorRoutine != null)
            return;
        _state = InternalState.Idle;
        PlayAnimState(StateIdle);
        _behaviorRoutine = StartCoroutine(BehaviorLoop());
    }

    public void BindHealthBarForFightStart() => BindHealthBar();

    public void Cutscene_PrepareBuriedFacingPlayer()
    {
        FaceDirection(AimDirectionToPlayer());
        SetUndergroundVisuals(true);
    }

    public void Cutscene_SetUnderground(bool underground) => SetUndergroundVisuals(underground);

    public void Cutscene_PlayAnimatorState(string stateName) => PlayAnimState(stateName);

    public float Cutscene_GetAnimatorClipLength(string stateName, float fallback) => GetAnimClipLength(stateName, fallback);

    private void ResolveSceneReferences()
    {
        if (!baseTilemap)
        {
            var go = GameObject.Find("Base");
            if (go) baseTilemap = go.GetComponent<Tilemap>();
        }
        if (!decorationTilemap)
        {
            var go = GameObject.Find("Decoration");
            if (go) decorationTilemap = go.GetComponent<Tilemap>();
        }
        if (!healthBarUI)
            healthBarUI = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
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

    private bool IsWalkableCell(Vector3Int cell)
    {
        if (baseTilemap)
            return baseTilemap.HasTile(cell);
        if (decorationTilemap)
            return decorationTilemap.HasTile(cell);
        return false;
    }

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
            var fromSprite = new Vector2(
                Mathf.Abs(ls.extents.x * sc.x),
                Mathf.Abs(ls.extents.y * sc.y));
            _pivotHalfExtentsWorld.x = Mathf.Max(_pivotHalfExtentsWorld.x, fromSprite.x);
            _pivotHalfExtentsWorld.y = Mathf.Max(_pivotHalfExtentsWorld.y, fromSprite.y);
        }

        float pad = Mathf.Max(0f, baseArenaEdgePadding);
        _pivotHalfExtentsWorld.x += pad;
        _pivotHalfExtentsWorld.y += pad;
    }

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
                var cell = new Vector3Int(x, y, 0);
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

    private Tilemap PrimaryTilemap => baseTilemap ? baseTilemap : decorationTilemap;

    private Vector2 CellCenterWorld(Vector3Int cell)
    {
        return PrimaryTilemap ? (Vector2)PrimaryTilemap.GetCellCenterWorld(cell) : (Vector2)transform.position;
    }

    private Vector3Int WorldToCell(Vector2 world)
    {
        return PrimaryTilemap ? PrimaryTilemap.WorldToCell(world) : Vector3Int.zero;
    }

    private Vector3Int GetCurrentCell() => WorldToCell(transform.position);

    private Vector3Int NearestWalkableCell(Vector2 world)
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

    private bool TryGetWalkablePath(Vector3Int from, Vector3Int to, List<Vector3Int> outPath)
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

        var prev = new Dictionary<Vector3Int, Vector3Int>();
        var q = new Queue<Vector3Int>();
        q.Enqueue(from);
        prev[from] = from;

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

    private float MeasurePathWorldLength(List<Vector3Int> path)
    {
        if (path == null || path.Count < 2) return 0f;
        float s = 0f;
        for (int i = 0; i < path.Count - 1; i++)
            s += Vector2.Distance(CellCenterWorld(path[i]), CellCenterWorld(path[i + 1]));
        return s;
    }

    private Vector2 PointOnWalkablePathAtDistance(List<Vector3Int> path, float distAlong)
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

    private Vector2 TangentOnWalkablePathAtDistance(List<Vector3Int> path, float distAlong, float delta)
    {
        if (path == null || path.Count < 2) return Vector2.right;
        delta = Mathf.Max(0.02f, delta);
        Vector2 p0 = PointOnWalkablePathAtDistance(path, Mathf.Max(0f, distAlong - delta));
        Vector2 p1 = PointOnWalkablePathAtDistance(path, distAlong + delta);
        Vector2 d = p1 - p0;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    private void SetBossWorldPosition(Vector2 world)
    {
        float z = transform.position.z;
        if (_rb)
            _rb.position = world;
        else
            transform.position = new Vector3(world.x, world.y, z);
    }

    private IEnumerator BehaviorLoop()
    {
        yield return null;
        BindHealthBar();

        while (!IsDead)
        {
            if (_phaseTransitionActive)
            {
                yield return null;
                continue;
            }

            if (!Player || _state == InternalState.Dead)
            {
                yield return null;
                continue;
            }

            if (_isStunned || _attackActive)
            {
            yield return null;
                continue;
            }

            if (ShouldReposition())
            {
                _repositionRoutine = StartCoroutine(RepositionRoutine());
                yield return _repositionRoutine;
                _repositionRoutine = null;
                if (_isStunned || IsDead)
                    continue;
            }

            BossAttackType next = PickNextAttack();
            StartAttack(next);

            while (_attackActive && !_isStunned && !IsDead)
                yield return null;

            bool chained = false;
            if (CurrentPhase >= 3 && !_isStunned && !IsDead
                && Random.value < phaseThreeChainChance)
            {
                float chainBreather = Mathf.Max(0.2f, GetIdleTimeForCurrentPhase() * 0.25f);
                float gapUntil = Time.time + chainBreather;
                while (Time.time < gapUntil && !_isStunned && !IsDead)
                    yield return null;

                BossAttackType chain = PickChainAttack(next);
                StartAttack(chain);
                while (_attackActive && !_isStunned && !IsDead)
                    yield return null;
                chained = true;
            }

            float idleTime = GetIdleTimeForCurrentPhase();
            if (chained) idleTime *= phaseThreeChainCooldownMultiplier;
            float waitUntil = Time.time + idleTime;
            PlayAnimState(StateIdle);
            while (Time.time < waitUntil && !_isStunned && !IsDead)
                yield return null;
        }
    }

    private bool ShouldReposition()
    {
        if (!Player) return false;
        float chance = GetArrayValue(repositionChanceByPhase, CurrentPhase - 1, 0.5f);
        return Random.value < chance;
    }

    private float GetIdleTimeForCurrentPhase()
    {
        return GetArrayValue(idleTimeAfterAttackByPhase, CurrentPhase - 1, 0.9f);
    }

    private float GetMaxShieldForCurrentPhase()
    {
        return GetArrayValue(maxShieldByPhase, CurrentPhase - 1, 58f);
    }

    private static float GetArrayValue(float[] arr, int index, float fallback)
    {
        if (arr == null || arr.Length == 0) return fallback;
        return arr[Mathf.Clamp(index, 0, arr.Length - 1)];
    }


    private BossAttackType PickNextAttack()
    {
        List<BossAttackType> pool = BuildPhaseAttackPool(CurrentPhase);
        BossAttackType pick = pool[Random.Range(0, pool.Count)];
        int safety = 0;
        while (pick == _lastAttack && safety < 6 && pool.Count > 1)
        {
            pick = pool[Random.Range(0, pool.Count)];
            safety++;
        }
        _lastAttack = pick;
        return pick;
    }

    private BossAttackType PickChainAttack(BossAttackType previous)
    {
        List<BossAttackType> pool = BuildPhaseAttackPool(CurrentPhase);
        pool.RemoveAll(a => a == previous);
        if (previous == BossAttackType.Laser || previous == BossAttackType.Dig)
            pool.RemoveAll(a => a == BossAttackType.Laser);
        if (pool.Count == 0) return BossAttackType.Melee;
        return pool[Random.Range(0, pool.Count)];
    }

    private List<BossAttackType> BuildPhaseAttackPool(int phase)
    {
        var pool = new List<BossAttackType>();
        pool.Add(BossAttackType.Melee);
        pool.Add(BossAttackType.Shoot);
        pool.Add(BossAttackType.Dig);
        if (phase >= 2) pool.Add(BossAttackType.Laser);
            return pool;
        }


    private void StartAttack(BossAttackType type)
    {
        _currentAttack = type;
        _attackActive = true;
        _state = InternalState.Attacking;

        IEnumerator routine = type switch
        {
            BossAttackType.Melee => MeleeRoutine(),
            BossAttackType.Shoot => ShootRoutine(),
            BossAttackType.Dig => DigRoutine(),
            BossAttackType.Laser => LaserRoutine(),
            _ => null
        };
        if (routine != null)
            _attackRoutine = StartCoroutine(routine);
    }

    private void EndAttack()
    {
        _immuneDuringDigMove = false;
        ReleaseWormLaserFireHoldPose();
        _attackActive = false;
        _attackRoutine = null;
        if (_state == InternalState.Attacking)
            _state = InternalState.Idle;
        PlayAnimState(StateIdle);
    }

    private void PriorityCancelOngoingMoves()
    {
        ReleaseWormLaserFireHoldPose();
        ReleasePhaseChangeHoldPose();
        DestroyLaserStretchSegments();
        if (_activeBossLaserBeam)
        {
            Destroy(_activeBossLaserBeam);
            _activeBossLaserBeam = null;
        }
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
        if (_repositionRoutine != null)
        {
            StopCoroutine(_repositionRoutine);
            _repositionRoutine = null;
        }
        if (_damageRoutine != null)
        {
            StopCoroutine(_damageRoutine);
            _damageRoutine = null;
        }
        DestroyActiveIndicator();
        _attackActive = false;
        _immuneDuringDigMove = false;
        if (_digInvulnerable)
            SetUndergroundVisuals(false);
        if (_rb)
            _rb.linearVelocity = Vector2.zero;
        if (_state == InternalState.Repositioning || _state == InternalState.Attacking)
            _state = InternalState.Idle;
    }

    private IEnumerator MeleeRoutine()
    {
        Vector2 aim = AimDirectionToPlayer();
        FaceDirection(aim);
        PlayAnimState(StateMelee);

        List<Vector3Int> coneCells = GetArcCells(transform.position, aim, meleeRange, meleeArcDegrees * 0.5f);
        float totalTelegraph = meleeFramesBeforeWait + meleeChargeWait;

        Vector2 center = (Vector2)transform.position + aim * (meleeRange * 0.5f);
        float width = 2f * meleeRange * Mathf.Tan(meleeArcDegrees * 0.5f * Mathf.Deg2Rad);
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        SpawnRectIndicator(center, new Vector2(meleeRange, width), angle, totalTelegraph, meleeImminentFraction);

        yield return new WaitForSeconds(meleeFramesBeforeWait);
        yield return new WaitForSeconds(meleeChargeWait);

        ResolveBossAudio()?.PlayMeleeSfx();
        DealDamageOnCells(coneCells, 0.55f, meleeDamage, 2.5f);
        DestroyActiveIndicator();

        yield return new WaitForSeconds(meleeStrikeDuration);
        yield return new WaitForSeconds(meleeRecoveryDuration);

        EndAttack();
    }


    private IEnumerator ShootRoutine()
    {
        Vector2 aim = AimDirectionToPlayer();
        FaceDirection(aim);
        PlayAnimState(StateShoot);

        float totalTelegraph = shootFramesBeforeWait + shootChargeWait;
        float sightLength = 5f;
        float sightWidth = 0.8f;
        Vector2 sightCenter = (Vector2)transform.position + aim * (sightLength * 0.5f);
        float sightAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        SpawnRectIndicator(sightCenter, new Vector2(sightLength, sightWidth), sightAngle,
            totalTelegraph, 0.32f);

        yield return new WaitForSeconds(shootFramesBeforeWait);
        yield return new WaitForSeconds(shootChargeWait);

        FireBossBullets(aim);
        DestroyActiveIndicator();

        if (CurrentPhase >= 3)
        {
            yield return new WaitForSeconds(shootBurstIntervalPhase3);
            FireBossBullets(AimDirectionToPlayer());
        }

        yield return new WaitForSeconds(shootRecoveryDuration);
        EndAttack();
    }

    private void FireBossBullets(Vector2 aim)
    {
        if (!bossProjectilePrefab) return;

        ResolveBossAudio()?.PlayRangedShootSfx();

        int bullets;
        float spread;
        if (CurrentPhase >= 3)
        {
            bullets = shootBulletsPhase3;
            spread = shootSpreadPhase3;
        }
        else if (CurrentPhase == 2)
        {
            bullets = shootBulletsPhase2;
            spread = shootSpreadPhase2;
        }
        else
        {
            bullets = 1;
            spread = 0f;
        }

        Vector3 firePos = transform.position;
        for (int i = 0; i < bullets; i++)
        {
            float offset = bullets == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (float)(bullets - 1));
            Vector2 dir = Rotate(aim, offset);
            GameObject go = Instantiate(bossProjectilePrefab, firePos, Quaternion.identity);
            SimpleProjectile proj = go.GetComponent<SimpleProjectile>();
            if (proj) proj.Fire(dir);
        }
    }


    private IEnumerator DigRoutine()
    {
        _immuneDuringDigMove = true;
        PlayAnimState(StateDigging);
        ResolveBossAudio()?.PlayDigSfx();
        float digClipLen = GetAnimClipLength(StateDigging, 0.62f);
        yield return new WaitForSeconds(digClipLen);

        SetUndergroundVisuals(true);

        Vector3Int followingCell = NearestWalkableCell(Player ? Player.position : (Vector2)transform.position);
        Vector3Int startCell = GetCurrentCell();
        float indicatorRadius = digStrikeRadius * 1.15f;
        SpawnCircleIndicator(CellCenterWorld(followingCell), indicatorRadius,
            digTrackDuration + digLockDuration,
            digLockDuration / Mathf.Max(0.01f, digTrackDuration + digLockDuration),
            tracked: true);

        float trackElapsed = 0f;
        float nextRubble = 0f;
        while (trackElapsed < digTrackDuration)
        {
            trackElapsed += Time.deltaTime;
            if (Player)
            {
                Vector3Int candidate = NearestWalkableCell(Player.position);
                if (candidate != followingCell)
                {
                    followingCell = candidate;
                    UpdateTrackedIndicatorCenter(CellCenterWorld(followingCell));
                }
            }

            float u = Mathf.Clamp01(trackElapsed / Mathf.Max(0.01f, digTrackDuration));
            Vector2 pos;
            Vector2 trailDir = Vector2.right;
            if (TryGetWalkablePath(startCell, followingCell, _pathScratch))
            {
                float len = MeasurePathWorldLength(_pathScratch);
                if (len < 0.001f)
                    pos = CellCenterWorld(_pathScratch[0]);
                else
                {
                    float distAlong = u * len;
                    pos = PointOnWalkablePathAtDistance(_pathScratch, distAlong);
                    trailDir = TangentOnWalkablePathAtDistance(_pathScratch, distAlong, 0.12f);
                }
            }
            else
            {
                Vector2 a = CellCenterWorld(startCell);
                Vector2 b = CellCenterWorld(followingCell);
                pos = Vector2.Lerp(a, b, u);
                trailDir = (b - a).sqrMagnitude > 0.0001f ? (b - a).normalized : Vector2.right;
            }

            SetBossWorldPosition(pos);

            if (trackElapsed >= nextRubble)
            {
                nextRubble += rubbleTrailInterval;
                SpawnRubbleBurst(transform.position, 2, 1.2f, rubbleColor, trailDir);
            }
            yield return null;
        }

        Vector3Int lockedPrimary = followingCell;
        ForceAllIndicatorsImminent();

        List<Vector3Int> strikeCells = new() { lockedPrimary };

        yield return new WaitForSeconds(digLockDuration);

        Vector2 risePos = CellCenterWorld(lockedPrimary);
        SetBossWorldPosition(risePos);

        SetUndergroundVisuals(false);
        PlayAnimState(StateRising);
        ResolveBossAudio()?.PlayRiseSfx();
        foreach (var cell in strikeCells)
            SpawnRubbleBurst(CellCenterWorld(cell), 5, 1.52f, riseRubbleColor, default);

        float riseClipLen = GetAnimClipLength(StateRising, 0.72f);
        float riseDmgDelay = riseClipLen * Mathf.Clamp01(digRiseDamageNormalizedTime);
        yield return new WaitForSeconds(riseDmgDelay);

        DealDamageOnCells(strikeCells, digStrikeRadius, digDamage, 4f);
        DestroyActiveIndicator();

        yield return new WaitForSeconds(riseClipLen - riseDmgDelay);
        EndAttack();
    }


    private IEnumerator LaserRoutine()
    {
        int sweepCount = CurrentPhase >= 3 ? 2 : 1;
        for (int sweepIndex = 0; sweepIndex < sweepCount; sweepIndex++)
        {
            Vector2 aim = AimDirectionToPlayer();
            FaceDirection(aim);
            float aimAngleDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            yield return ExecuteSingleLaserSweep(aimAngleDeg);
            if (sweepIndex < sweepCount - 1 && laserBetweenSweepPause > 0f)
                yield return new WaitForSeconds(laserBetweenSweepPause);
        }

        yield return new WaitForSeconds(laserRecoveryDuration);
        EndAttack();
    }

    private IEnumerator ExecuteSingleLaserSweep(float aimAngleDeg)
    {
        float castAngleDeg = aimAngleDeg;
        Vector2 castDir = AngleToVector(castAngleDeg);

        PlayAnimState(StateLaserCharge);

        Vector2 mouthStart = ComputeMouthWorldPosition();
        Vector2 telegraphCenter = mouthStart + castDir * (laserBeamLength * 0.5f);

        BossAttackIndicator chargeTelegraph = SpawnRectIndicator(telegraphCenter,
            new Vector2(laserBeamLength, laserChargeStripeWidth), castAngleDeg,
            Mathf.Max(0.05f, laserChargeDuration), 0.32f);

        yield return new WaitForSeconds(laserChargeDuration);

        if (chargeTelegraph)
        {
            Destroy(chargeTelegraph.gameObject);
            _activeIndicators.Remove(chargeTelegraph);
        }

        if (!bossLaserBeamPrefab)
        {
            PlayAnimState(StateIdle);
            yield break;
        }

        FreezeWormLaserFireHoldPose();
        ResolveBossAudio()?.PlayLaserShootSfx();

        GameObject beamGo = Instantiate(bossLaserBeamPrefab);
        _activeBossLaserBeam = beamGo;
        var beamFx = beamGo.GetComponent<BossLaserBeamInstance>();
        if (beamFx)
        {
            beamFx.ApplyVisualTint(laserBeamColor);
            if (spriteRenderer) beamFx.CopySortingFrom(spriteRenderer);
        }

        EnsureLaserStretchSegmentsCreated();
        int segN = Mathf.Max(4, laserStretchSegmentCount);
        if (_laserRowFadeStarted == null || _laserRowFadeStarted.Length != segN)
            _laserRowFadeStarted = new bool[segN];
        else
            System.Array.Clear(_laserRowFadeStarted, 0, segN);

        foreach (var seg in _laserStretchSegments)
        {
            if (seg) seg.SetVisualEnabled(false);
        }

        float segmentLenFixed = laserBeamLength / segN;
        var damagedThisSweep = new HashSet<IDamageable>();
        float elapsed = 0f;
        float beamZ = transform.position.z - 0.01f;

        while (elapsed < laserFireDuration)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, laserFireDuration));
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float beamLen = Mathf.Lerp(laserMinBeamLength, laserBeamLength, eased);
            Vector2 mouthNow = ComputeMouthWorldPosition();
            Vector2 tip = mouthNow + castDir * beamLen;
            float u = beamLen / Mathf.Max(0.001f, laserBeamLength);

            _laserStretchSegments.RemoveAll(static s => s == null);

            if (beamFx)
                beamFx.ApplyBeam(mouthNow, castAngleDeg, beamLen, beamZ);

            for (int i = 0; i < segN && i < _laserStretchSegments.Count; i++)
            {
                BossAttackIndicator seg = _laserStretchSegments[i];
                if (!seg) continue;

                float rowStartU = i / (float)segN;
                if (u < rowStartU)
                {
                    seg.SetVisualEnabled(false);
                    continue;
                }

                seg.SetVisualEnabled(true);
                Vector2 center = mouthNow + castDir * ((i + 0.5f) * segmentLenFixed);
                seg.UpdateRect(center, new Vector2(segmentLenFixed, laserSegmentStripeWidth), castAngleDeg);

                float rowEndU = (i + 1f) / segN;
                float imminentAt = rowEndU - (laserSegmentImminentLead / segN);
                if (u >= imminentAt && seg.CurrentPhase != BossAttackIndicator.Phase.Imminent)
                    seg.ForceImminent();

                if (u >= rowEndU && !_laserRowFadeStarted[i])
                {
                    _laserRowFadeStarted[i] = true;
                    seg.FadeOutAndDestroy(Mathf.Max(0.04f, laserRowFadeDuration));
                }
            }

            DealDamageLaserTip(tip, laserTipHitRadius, laserDamage, damagedThisSweep);

            _activeIndicators.RemoveAll(static r => r == null);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (beamFx)
        {
            Vector2 mouthFinal = ComputeMouthWorldPosition();
            beamFx.ApplyBeam(mouthFinal, castAngleDeg, laserBeamLength, beamZ);
        }

        ReleaseWormLaserFireHoldPose();
        _activeBossLaserBeam = null;

        _laserStretchSegments.RemoveAll(static s => s == null);
        foreach (var seg in _laserStretchSegments)
        {
            if (!seg) continue;
            _activeIndicators.Remove(seg);
            Destroy(seg.gameObject);
        }
        _laserStretchSegments.Clear();

        if (beamFx) beamFx.FadeAndDestroy(0.18f);
        else if (beamGo) Destroy(beamGo);
    }

    private void EnsureLaserStretchSegmentsCreated()
    {
        if (indicatorBaseSprite == null) return;
        _laserStretchSegments.RemoveAll(static s => s == null);
        int n = Mathf.Max(4, laserStretchSegmentCount);
        if (_laserStretchSegments.Count >= n) return;
        float longDur = laserFireDuration + laserChargeDuration + 8f;
        for (int i = _laserStretchSegments.Count; i < n; i++)
        {
            BossAttackIndicator ind = CreateIndicator();
            ind.BeginRect(indicatorBaseSprite, indicatorImminentSprite,
                indicatorBaseColor, indicatorImminentColor,
                ComputeMouthWorldPosition(), new Vector2(0.2f, laserSegmentStripeWidth), 0f,
                longDur, 0.08f, 5.5f, indicatorSortingOrder, _sortingLayerId);
            _laserStretchSegments.Add(ind);
        }
    }

    private void DestroyLaserStretchSegments()
    {
        foreach (var seg in _laserStretchSegments)
        {
            if (!seg) continue;
            _activeIndicators.Remove(seg);
            Destroy(seg.gameObject);
        }
        _laserStretchSegments.Clear();
        _laserRowFadeStarted = null;
    }

    private void DealDamageLaserTip(Vector2 tipWorld, float radius, float damage,
        HashSet<IDamageable> alreadyDamaged)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(tipWorld, radius, playerLayerMask);
        foreach (var hit in hits)
        {
            if (!hit) continue;
            IDamageable d = hit.GetComponentInParent<IDamageable>();
            if (d == null || alreadyDamaged.Contains(d)) continue;
            Vector2 kb = ((Vector2)hit.bounds.center - tipWorld);
            if (kb.sqrMagnitude <= 0.0001f) kb = Vector2.up;
            else kb.Normalize();
            d.TakeHit(damage, kb, 3.5f);
            alreadyDamaged.Add(d);
        }
    }

    private void FreezeWormLaserFireHoldPose()
    {
        if (!animator) return;
        animator.Play(StateLaserFire, 0, 1f);
        animator.Update(0f);
        animator.speed = 0f;
        _wormAnimatorHeldOnLaserFireFrame = true;
    }

    private void ReleaseWormLaserFireHoldPose()
    {
        if (!_wormAnimatorHeldOnLaserFireFrame) return;
        _wormAnimatorHeldOnLaserFireFrame = false;
        if (animator) animator.speed = 1f;
    }

    private void FreezePhaseChangeHoldPose()
    {
        if (!animator) return;
        animator.Play(StatePhaseChange, 0, 1f);
        animator.Update(0f);
        animator.speed = 0f;
        _wormAnimatorHeldOnPhaseChangeFrame = true;
    }

    private void ReleasePhaseChangeHoldPose()
    {
        if (!_wormAnimatorHeldOnPhaseChangeFrame) return;
        _wormAnimatorHeldOnPhaseChangeFrame = false;
        if (animator) animator.speed = 1f;
    }

    private Vector2 ComputeMouthWorldPosition()
    {
        Vector2 pos = transform.position;
        Vector2 offset = laserMouthOffset;
        bool flipX = spriteRenderer && spriteRenderer.flipX;
        bool facingRight = spriteFacesRightByDefault ^ flipX;
        if (!facingRight) offset.x = -offset.x;
        return pos + offset;
    }

    private static Vector2 AngleToVector(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }


    private IEnumerator RepositionRoutine()
    {
        _immuneDuringDigMove = true;
        _state = InternalState.Repositioning;
        Vector3Int targetCell = ChooseRepositionTarget();
        Vector3 startWorld = transform.position;
        Vector2 targetWorld2 = CellCenterWorld(targetCell);
        Vector3 targetWorld = new Vector3(targetWorld2.x, targetWorld2.y, transform.position.z);
        if (Vector3.Distance(startWorld, targetWorld) < 0.25f)
        {
            _immuneDuringDigMove = false;
            _state = InternalState.Idle;
            yield break;
        }

        PlayAnimState(StateDigging);
        ResolveBossAudio()?.PlayDigSfx();
        float digClipLen = GetAnimClipLength(StateDigging, 0.62f);
        for (float digT = 0f; digT < digClipLen; digT += Time.deltaTime)
        {
            if (_phaseTransitionActive)
            {
                _immuneDuringDigMove = false;
                _state = InternalState.Idle;
                yield break;
            }
            yield return null;
        }
        if (_phaseTransitionActive)
        {
            _immuneDuringDigMove = false;
            _state = InternalState.Idle;
            yield break;
        }

        SetUndergroundVisuals(true);

        Vector3Int reposFrom = WorldToCell((Vector2)startWorld);
        bool pathOk = TryGetWalkablePath(reposFrom, targetCell, _pathScratch);
        float pathWorldLen = pathOk && _pathScratch.Count > 0 ? MeasurePathWorldLength(_pathScratch) : 0f;
        if (pathWorldLen < 0.02f)
        {
            pathOk = false;
            pathWorldLen = Vector2.Distance((Vector2)startWorld, targetWorld2);
        }
        pathWorldLen = Mathf.Max(0.02f, pathWorldLen);
        float travelTime = pathWorldLen / Mathf.Max(0.5f, undergroundSpeed);
        float elapsed = 0f;
        float nextRubble = 0f;
        Vector3 lastRubblePos = startWorld;

        while (elapsed < travelTime)
        {
            if (_phaseTransitionActive)
            {
                _immuneDuringDigMove = false;
                SetUndergroundVisuals(false);
                _state = InternalState.Idle;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);
            Vector2 pos;
            Vector2 trailDir = Vector2.right;
            if (pathOk && _pathScratch.Count > 0)
            {
                float distAlong = t * pathWorldLen;
                pos = PointOnWalkablePathAtDistance(_pathScratch, distAlong);
                trailDir = TangentOnWalkablePathAtDistance(_pathScratch, distAlong, 0.1f);
            }
            else
            {
                pos = Vector2.Lerp((Vector2)startWorld, targetWorld2, t);
                Vector2 seg = targetWorld2 - (Vector2)startWorld;
                trailDir = seg.sqrMagnitude > 0.0001f ? seg.normalized : Vector2.right;
            }

            SetBossWorldPosition(pos);

            if (elapsed >= nextRubble)
            {
                nextRubble += rubbleTrailInterval;
                Vector3 here = transform.position;
                if ((here - lastRubblePos).sqrMagnitude > 0.005f)
                {
                    SpawnRubbleBurst(here, 2, 1.12f, rubbleColor, trailDir);
                    lastRubblePos = here;
                }
            }
            yield return null;
        }

        SetBossWorldPosition(new Vector2(targetWorld.x, targetWorld.y));

        SetUndergroundVisuals(false);
        PlayAnimState(StateRising);
        ResolveBossAudio()?.PlayRiseSfx();
        SpawnRubbleBurst(targetWorld, 5, 1.52f, riseRubbleColor, default);

        float riseClipLen = GetAnimClipLength(StateRising, 0.72f);
        float riseDmgDelay = riseClipLen * 0.55f;
        for (float riseT = 0f; riseT < riseDmgDelay; riseT += Time.deltaTime)
        {
            if (_phaseTransitionActive)
            {
                _immuneDuringDigMove = false;
                _state = InternalState.Idle;
                yield break;
            }
            yield return null;
        }
        if (_phaseTransitionActive)
        {
            _immuneDuringDigMove = false;
            _state = InternalState.Idle;
            yield break;
        }

        DealDamageOnCells(new List<Vector3Int> { targetCell }, repositionEmergeRadius, repositionEmergeDamage, 3f);

        float riseRemain = Mathf.Max(0f, riseClipLen - riseDmgDelay);
        for (float riseT = 0f; riseT < riseRemain; riseT += Time.deltaTime)
        {
            if (_phaseTransitionActive)
            {
                _immuneDuringDigMove = false;
                _state = InternalState.Idle;
                yield break;
            }
            yield return null;
        }

        _immuneDuringDigMove = false;
        _state = InternalState.Idle;
        PlayAnimState(StateIdle);
    }

    private Vector3Int ChooseRepositionTarget()
    {
        if (_walkableCells.Count == 0) return GetCurrentCell();

        Vector2 playerPos = Player ? (Vector2)Player.position : (Vector2)transform.position;
        float shieldNorm = CurrentShieldNormalized;
        bool wantDistance = !_shieldBroken && shieldNorm > 0.01f && shieldNorm < shieldRegenSeekThreshold;

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


    public override void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        if (IsDead || _state == InternalState.Dead) return;
        if (_introActive) return;
        if (_phaseTransitionActive) return;
        if (damage <= 0f) return;

        if (_immuneDuringDigMove || _digInvulnerable)
            return;

        float maxS = GetMaxShieldForCurrentPhase();

        if (_currentShield > 0f)
        {
            float absorbed = Mathf.Min(_currentShield, damage);
            _currentShield -= absorbed;
            ResolveBossAudio()?.PlayShieldDamageSfx();
            if (maxS > 0f && _currentShield < maxS)
                _lastShieldDamageTime = Time.time;
            UpdateShieldUI();
            PlayShieldPingFlash();
            if (_currentShield <= 0f)
            {
                _currentShield = 0f;
                _shieldBroken = true;
                if (healthBarUI) healthBarUI.NotifyShieldBroken();
                PriorityCancelOngoingMoves();
                _isStunned = true;
                _state = InternalState.Stunned;
                if (_rb)
                    _rb.linearVelocity = Vector2.zero;
                CrossFadeAnimState(StateDamage, 0.1f);
                if (_shieldBreakFeedbackRoutine != null)
                    StopCoroutine(_shieldBreakFeedbackRoutine);
                Vector2 kb = knockbackDirection.sqrMagnitude > 0.0001f
                    ? knockbackDirection.normalized
                    : -AimDirectionToPlayer();
                _shieldBreakFeedbackRoutine = StartCoroutine(ShieldBreakFeedbackRoutine(kb));
            }
            return;
        }

        if (maxS > 0f && _currentShield < maxS)
            _lastShieldDamageTime = Time.time;

        ResolveBossAudio()?.PlayBossDamageSfx();
        float healthBarNormBeforeHpHit = HealthNormalized;
        base.TakeHit(damage, knockbackDirection, knockbackForce);
        if (IsDead)
        {
            _deathHealthBarDrainFromNormalized = healthBarNormBeforeHpHit;
            return;
        }

        if (!_attackActive)
            StartDamageReaction();
    }

    private void StartDamageReaction()
    {
        if (_damageRoutine != null) StopCoroutine(_damageRoutine);
        _damageRoutine = StartCoroutine(DamageReactionRoutine());
    }

    private IEnumerator DamageReactionRoutine()
    {
        _isStunned = true;
        _state = InternalState.Stunned;
        PlayAnimState(StateDamage);
        if (_rb) _rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(damageStunDuration);
        _isStunned = false;
        if (!IsDead)
        {
            _state = InternalState.Idle;
            PlayAnimState(StateIdle);
        }
        _damageRoutine = null;
    }

    private void PlayShieldPingFlash()
    {
        if (!spriteRenderer) return;
        if (_shieldPingRoutine != null) StopCoroutine(_shieldPingRoutine);
        _shieldPingRoutine = StartCoroutine(ShieldPingRoutine());
    }

    private IEnumerator ShieldPingRoutine()
    {
        if (!spriteRenderer) yield break;
        Color baseColor = _shieldPingBaseColor;
        float t = 0f;
        while (t < shieldPingDuration && spriteRenderer)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / shieldPingDuration);
            float amount = Mathf.Sin(u * Mathf.PI);
            spriteRenderer.color = Color.Lerp(baseColor, shieldPingColor, amount);
            yield return null;
        }
        if (spriteRenderer) spriteRenderer.color = baseColor;
        _shieldPingRoutine = null;
    }

    private void UpdateShieldUI()
    {
        if (!healthBarUI) return;
        healthBarUI.SetShield(CurrentShieldNormalized);
    }

    private IEnumerator ShieldBreakFeedbackRoutine(Vector2 knockbackDirection)
    {
        yield return ShieldBreakSurfacedFeedbackCore(knockbackDirection);
    }

    private IEnumerator ShieldBreakSurfacedFeedbackCore(Vector2 knockbackDirection)
    {
        CameraController camCtrl = phaseTransitionCamera ? phaseTransitionCamera : FindFirstObjectByType<CameraController>();
        if (camCtrl)
            camCtrl.Shake(shieldBreakCameraShake);

        ResolveBossAudio()?.PlayShieldBreakSfx();

        ShieldBreakShockwaveVfx.Spawn(transform, spriteRenderer);

        float prevScale = Time.timeScale;
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(0.055f);
        Time.timeScale = prevScale > 0.02f ? prevScale : 1f;

        if (Rb)
        {
            Rb.linearVelocity = Vector2.zero;
            Vector2 dir = knockbackDirection.sqrMagnitude > 0.0001f ? knockbackDirection.normalized : -AimDirectionToPlayer();
            Rb.AddForce(dir * shieldBreakKnockbackForce, ForceMode2D.Impulse);
        }

        float endTime = Time.time + Mathf.Max(0.1f, shieldBreakStunDuration);
        float zWobbleBase = transform.eulerAngles.z;

        while (Time.time < endTime && !IsDead && !_phaseTransitionActive)
        {
            float t = Time.time;
            if (_spriteVisualIsChild && spriteRenderer)
            {
                float wx = Mathf.Sin(t * 13f) * shieldBreakChildWobbleX
                    + Mathf.Sin(t * 21f) * (shieldBreakChildWobbleX * 0.35f);
                float wy = Mathf.Sin(t * 10f) * shieldBreakChildWobbleY
                    + Mathf.Sin(t * 16.5f) * (shieldBreakChildWobbleY * 0.4f);
                float rz = Mathf.Sin(t * 11.3f) * shieldBreakChildWobbleDegrees
                    + Mathf.Sin(t * 18f) * (shieldBreakChildWobbleDegrees * 0.42f);
                spriteRenderer.transform.localPosition = _spriteVisualBaseLocal + new Vector3(wx, wy, 0f);
                spriteRenderer.transform.localRotation = _spriteVisualBaseLocalRot * Quaternion.Euler(0f, 0f, rz);
            }
            else
            {
                float dz = Mathf.Sin(t * 11f) * shieldBreakWobbleDegrees
                    + Mathf.Sin(t * 17.5f) * (shieldBreakWobbleDegrees * 0.42f)
                    + Mathf.Sin(t * 6.2f) * (shieldBreakWobbleDegrees * 0.22f);
                transform.rotation = Quaternion.Euler(0f, 0f, zWobbleBase + dz);
            }

            yield return null;
        }

        if (!_spriteVisualIsChild)
            transform.rotation = Quaternion.Euler(0f, 0f, zWobbleBase);

        ResetSpriteVisualLocalIfChild();
        DisableStunVisual();
        _isStunned = false;
        _shieldBreakFeedbackRoutine = null;
        if (!IsDead && !_phaseTransitionActive)
        {
            _state = InternalState.Idle;
            PlayAnimState(StateIdle);
        }
    }

    private void DisableStunVisual()
    {
    }

    private void ResetSpriteVisualLocalIfChild()
    {
        if (!_spriteVisualIsChild || !spriteRenderer) return;
        spriteRenderer.transform.localPosition = _spriteVisualBaseLocal;
        spriteRenderer.transform.localRotation = _spriteVisualBaseLocalRot;
    }

    protected override void Die()
    {
        if (_state == InternalState.Dead) return;
        _state = InternalState.Dead;
        _immuneDuringDigMove = false;
        _introActive = false;
        if (_behaviorRoutine != null) StopCoroutine(_behaviorRoutine);
        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        if (_damageRoutine != null) StopCoroutine(_damageRoutine);
        if (_repositionRoutine != null) StopCoroutine(_repositionRoutine);
        _repositionRoutine = null;
        if (_phaseTransitionRoutine != null) StopCoroutine(_phaseTransitionRoutine);
        _phaseTransitionActive = false;
        _phaseChangePending = false;
        ReleaseWormLaserFireHoldPose();
        ReleasePhaseChangeHoldPose();
        if (_shieldPingRoutine != null) StopCoroutine(_shieldPingRoutine);
        if (_shieldBreakFeedbackRoutine != null) StopCoroutine(_shieldBreakFeedbackRoutine);
        _shieldBreakFeedbackRoutine = null;
        DisableStunVisual();
        ResetSpriteVisualLocalIfChild();
        DestroyActiveIndicator();
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        SetUndergroundVisuals(false);
        if (_rb) _rb.linearVelocity = Vector2.zero;

        if (healthBarUI)
            yield return healthBarUI.AnimateHealthFillTo(0f, deathHealthDrainSeconds, _deathHealthBarDrainFromNormalized);

        CameraController cam = phaseTransitionCamera ? phaseTransitionCamera : FindFirstObjectByType<CameraController>();
        if (cam)
            yield return cam.PlayDeathKillImpactRoutine();
        if (!escapeSequenceManager)
            escapeSequenceManager = FindFirstObjectByType<BossEscapeSequenceManager>();
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        var playerController = playerGo ? playerGo.GetComponent<PlayerController>() : null;
        Transform playerTf = playerGo ? playerGo.transform : null;

        if (playerController)
            playerController.LockControlsForSeconds(600f);

        Transform bossTf = transform;
        Transform panProxy = null;

        PlayAnimState(StateIdle);

        if (playerTf && cam)
        {
            panProxy = new GameObject("DeathCutscenePanProxy").transform;
            Vector3 fromPos = playerTf.position;
            fromPos.z = bossTf.position.z;
            Vector3 toPos = bossTf.position;
            panProxy.position = fromPos;
            cam.LockToTransform(panProxy);
            yield return PanProxyLerp(panProxy, fromPos, toPos, deathPanPlayerToBossSeconds);
            cam.LockToTransform(bossTf);
        }
        else if (cam)
            cam.LockToTransform(bossTf);

        float dyingLen = GetAnimClipLength(StateDying, 0.96f);
        PlayAnimState(StateDying);
        ResolveBossAudio()?.PlayDeathSfxForAnimationDuration(dyingLen);

        yield return new WaitForSeconds(dyingLen);

        HideBossAfterDeathVisuals();

        if (healthBarUI)
            yield return healthBarUI.FadeOutForDeath(deathHealthBarFadeSeconds);

        bool teleporterPanFinished = false;
        if (postBossTeleporter && cam)
        {
            float zRef = postBossTeleporter.transform.position.z;
            Vector3 bossPos = bossTf.position;
            bossPos.z = zRef;
            Vector3 telePos = GetTeleporterPanWorldPosition(zRef);

            postBossTeleporter.SetActive(true);
            if (!panProxy)
            {
                panProxy = new GameObject("DeathCutscenePanProxy").transform;
                panProxy.position = bossPos;
                }
                else
                panProxy.position = bossPos;

            cam.LockToTransform(panProxy);
            yield return PanAndFadeTeleporterReveal(panProxy, bossPos, telePos, postBossTeleporter, deathPanBossToTeleporterSeconds, deathTeleporterFadeSeconds);

            if (escapeSequenceManager)
                yield return escapeSequenceManager.RunTeleporterRevealRumble(cam);

            if (playerTf)
            {
                Vector3 fromT = telePos;
                fromT.z = playerTf.position.z;
                Vector3 toP = playerTf.position;
                panProxy.position = fromT;
                cam.LockToTransform(panProxy);
                yield return PanProxyLerp(panProxy, fromT, toP, deathPanTeleporterToPlayerSeconds);
            }

            cam.LockToPlayer();
            teleporterPanFinished = true;
        }
        else if (postBossTeleporter)
        {
            postBossTeleporter.SetActive(true);
            yield return FadeTeleporterInPlace(postBossTeleporter, deathTeleporterFadeSeconds);
            if (escapeSequenceManager)
                yield return escapeSequenceManager.RunTeleporterRevealRumble(cam);
        }

        if (cam && !teleporterPanFinished)
            cam.LockToPlayer();

        if (panProxy)
        {
            Destroy(panProxy.gameObject);
            panProxy = null;
        }

        if (playerController)
            playerController.UnlockControlsImmediate();

        var bossAudio = ResolveBossAudio();
        if (postBossTeleporter && bossAudio)
            bossAudio.BeginEscapeMusic();

        if (postBossTeleporter && escapeSequenceManager)
            escapeSequenceManager.BeginEscapePhase();

        base.Die();
    }

    private void HideBossAfterDeathVisuals()
    {
        if (_activeBossLaserBeam)
        {
            Destroy(_activeBossLaserBeam);
            _activeBossLaserBeam = null;
        }

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = false;
        if (animator)
            animator.enabled = false;
        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            c.enabled = false;
    }

    private Vector3 GetTeleporterPanWorldPosition(float zWorld)
    {
        if (!postBossTeleporterPan)
            return new Vector3(0f, 0f, zWorld);

        Vector3 p = postBossTeleporterPan.position;
        p.z = zWorld;
        return p;
    }

    private static IEnumerator PanProxyLerp(Transform proxy, Vector3 from, Vector3 to, float duration)
    {
        if (!proxy)
            yield break;
        if (duration <= 0.0001f)
        {
            proxy.position = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            proxy.position = Vector3.Lerp(from, to, u);
            yield return null;
        }
        proxy.position = to;
    }

    private static IEnumerator PanAndFadeTeleporterReveal(Transform panProxy, Vector3 fromPos, Vector3 toPos,
        GameObject teleporterGo, float panDuration, float fadeDuration)
    {
        var tilemaps = teleporterGo.GetComponentsInChildren<Tilemap>(true);
        var sprites = teleporterGo.GetComponentsInChildren<SpriteRenderer>(true);
        var cols = teleporterGo.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
            c.enabled = false;

        var tileBase = new Color[tilemaps.Length];
        for (int i = 0; i < tilemaps.Length; i++)
        {
            tileBase[i] = tilemaps[i].color;
            var tc = tileBase[i];
            tc.a = 0f;
            tilemaps[i].color = tc;
        }

        var spriteBase = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            spriteBase[i] = sprites[i].color;
            var sc = spriteBase[i];
            sc.a = 0f;
            sprites[i].color = sc;
        }

        float dur = Mathf.Max(0.01f, Mathf.Max(panDuration, fadeDuration));
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float uPan = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, panDuration));
            float uFade = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, fadeDuration));
            float smoothPan = Mathf.SmoothStep(0f, 1f, uPan);
            if (panProxy)
                panProxy.position = Vector3.Lerp(fromPos, toPos, smoothPan);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var c = tileBase[i];
                c.a = tileBase[i].a * uFade;
                tilemaps[i].color = c;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                var c = spriteBase[i];
                c.a = spriteBase[i].a * uFade;
                sprites[i].color = c;
            }

            yield return null;
        }

        if (panProxy)
            panProxy.position = toPos;
        for (int i = 0; i < tilemaps.Length; i++)
            tilemaps[i].color = tileBase[i];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i].color = spriteBase[i];
        foreach (var c in cols)
            c.enabled = true;
    }

    private static IEnumerator FadeTeleporterInPlace(GameObject teleporterGo, float fadeDuration)
    {
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        var tilemaps = teleporterGo.GetComponentsInChildren<Tilemap>(true);
        var sprites = teleporterGo.GetComponentsInChildren<SpriteRenderer>(true);
        var cols = teleporterGo.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
            c.enabled = false;

        var tileBase = new Color[tilemaps.Length];
        for (int i = 0; i < tilemaps.Length; i++)
        {
            tileBase[i] = tilemaps[i].color;
            var tc = tileBase[i];
            tc.a = 0f;
            tilemaps[i].color = tc;
        }

        var spriteBase = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            spriteBase[i] = sprites[i].color;
            var sc = spriteBase[i];
            sc.a = 0f;
            sprites[i].color = sc;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float uFade = Mathf.Clamp01(t / fadeDuration);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var c = tileBase[i];
                c.a = tileBase[i].a * uFade;
                tilemaps[i].color = c;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                var c = spriteBase[i];
                c.a = spriteBase[i].a * uFade;
                sprites[i].color = c;
            }

            yield return null;
        }

        for (int i = 0; i < tilemaps.Length; i++)
            tilemaps[i].color = tileBase[i];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i].color = spriteBase[i];
        foreach (var c in cols)
            c.enabled = true;
    }


    private void BindHealthBar()
    {
        if (!healthBarUI) return;
        healthBarUI.Bind(this);
        healthBarUI.SetHealth(HealthNormalized);
        healthBarUI.SetShield(CurrentShieldNormalized);
        _lastObservedPhase = CurrentPhase;
        _phaseChangePending = false;
    }

    private IEnumerator PhaseTransitionSequence(int newPhase)
    {
        _phaseTransitionActive = true;
        _phaseChangePending = false;
        if (_shieldBreakFeedbackRoutine != null)
        {
            StopCoroutine(_shieldBreakFeedbackRoutine);
            _shieldBreakFeedbackRoutine = null;
        }
        ResetSpriteVisualLocalIfChild();
        DisableStunVisual();
        PriorityCancelOngoingMoves();
        _isStunned = false;
        _state = InternalState.Idle;

        _shieldBroken = false;
        _currentShield = 0f;
        _lastShieldDamageTime = Time.time;
        UpdateShieldUI();

        Vector2 aim = AimDirectionToPlayer();
        FaceDirection(aim);

        CameraController cam = phaseTransitionCamera ? phaseTransitionCamera : FindFirstObjectByType<CameraController>();
        float enterShake = newPhase >= 3 ? phaseTransitionShakeEnterPhase3 : phaseTransitionShakeEnterPhase2;
        if (cam) cam.Shake(enterShake);
        if (cam) cam.PhaseTransitionZoomIn(transform);

        if (newPhase == 2) ResolveBossAudio()?.PlayPhaseTransitionToPhase2Sfx();
        else if (newPhase == 3) ResolveBossAudio()?.PlayPhaseTransitionToPhase3Sfx();

        if (healthBarUI)
            healthBarUI.NotifyPhaseChange(newPhase);

        float phaseClipLen = GetAnimClipLength(StatePhaseChange, 0.88f);
        CrossFadeAnimState(StatePhaseChange, 0.1f);
        float introT = 0f;
        while (introT < phaseClipLen)
        {
            introT += Time.deltaTime;
            if (_rb) _rb.linearVelocity = Vector2.zero;
            yield return null;
        }

        FreezePhaseChangeHoldPose();

        float maxShield = GetMaxShieldForCurrentPhase();
        float fillDur = newPhase >= 3 ? phaseTransitionShieldFillPhase3 : phaseTransitionShieldFillPhase2;
        fillDur = Mathf.Max(0.05f, fillDur);
        float fillT = 0f;
        while (fillT < fillDur)
        {
            fillT += Time.deltaTime;
            float u = Mathf.Clamp01(fillT / fillDur);
            _currentShield = maxShield * u;
            UpdateShieldUI();
            if (_rb) _rb.linearVelocity = Vector2.zero;
            yield return null;
        }

        _currentShield = maxShield;
        _lastShieldDamageTime = Time.time;
        UpdateShieldUI();
        if (healthBarUI)
            healthBarUI.NotifyShieldRestored(newPhase);

        float holdAfter = newPhase >= 3 ? phaseTransitionLaserHoldAfterShieldPhase3 : phaseTransitionLaserHoldAfterShieldPhase2;
        holdAfter = Mathf.Max(0f, holdAfter);
        float holdT = 0f;
        while (holdT < holdAfter)
        {
            holdT += Time.deltaTime;
            if (_rb) _rb.linearVelocity = Vector2.zero;
            yield return null;
        }

        ReleasePhaseChangeHoldPose();
        PlayAnimState(StateIdle);

        if (cam) cam.PhaseTransitionZoomRestore();
        if (cam && phaseTransitionShakeResume > 0.0001f)
            cam.Shake(phaseTransitionShakeResume);

        var bossAudio = ResolveBossAudio();
        if (bossAudio)
            bossAudio.NotifyBossPhase(newPhase);

        _phaseTransitionActive = false;
        _phaseTransitionRoutine = null;
    }

    private Vector2 AimDirectionToPlayer()
    {
        if (!Player) return Vector2.right;
        Vector2 d = (Player.position - transform.position);
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    private void FaceDirection(Vector2 direction)
    {
        if (!spriteRenderer) return;
        if (Mathf.Abs(direction.x) < Mathf.Max(0.001f, flipThreshold)) return;
        bool playerOnRight = direction.x > 0f;
        bool desiredFlipX = spriteFacesRightByDefault ? (direction.x < 0f) : playerOnRight;
        if (desiredFlipX == spriteRenderer.flipX) return;
        if (Time.time - _lastFlipTime < Mathf.Max(0f, minFlipInterval)) return;
        spriteRenderer.flipX = desiredFlipX;
        _lastFlipTime = Time.time;
    }

    private void PlayAnimState(string stateName)
    {
        if (!animator) return;
        if (!animator.enabled) animator.enabled = true;
        animator.speed = 1f;
        animator.Play(stateName, 0, 0f);
    }

    private void CrossFadeAnimState(string stateName, float duration)
    {
        if (!animator) return;
        if (!animator.enabled) animator.enabled = true;
        animator.speed = 1f;
        animator.CrossFade(stateName, duration, 0, 0f);
    }

    private float GetAnimClipLength(string stateName, float fallback)
    {
        if (!animator || animator.runtimeAnimatorController == null) return fallback;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip && clip.name == stateName)
                return clip.length;
        }
        return fallback;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private List<Vector3Int> GetArcCells(Vector2 center, Vector2 forward, float range, float halfArcDegrees)
    {
        var cells = new List<Vector3Int>();
        float cosThreshold = Mathf.Cos(halfArcDegrees * Mathf.Deg2Rad);
        foreach (var cell in _walkableCells)
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

    private void DealDamageOnCells(List<Vector3Int> cells, float radius, float damage, float knockbackForce)
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
                IDamageable d = hit.GetComponentInParent<IDamageable>();
                if (d == null || dealt.Contains(d)) continue;
                Vector2 kb = ((Vector2)hit.bounds.center - center);
                if (kb.sqrMagnitude <= 0.0001f) kb = Vector2.up;
                else kb.Normalize();
                d.TakeHit(damage, kb, knockbackForce);
                dealt.Add(d);
            }
        }
    }

    private void SetUndergroundVisuals(bool underground)
    {
        _digInvulnerable = underground;
        if (_mainCollider) _mainCollider.enabled = !underground;
        if (spriteRenderer)
        {
            spriteRenderer.enabled = !underground;
            if (!underground)
            {
                Color c = spriteRenderer.color;
                c.a = _baseAlpha;
                spriteRenderer.color = c;
                _shieldPingBaseColor = c;
            }
        }
    }


    private BossAttackIndicator SpawnRectIndicator(Vector2 center, Vector2 size, float angleDegrees,
        float duration, float imminentFraction, bool tracked = false)
    {
        if (indicatorBaseSprite == null) return null;
        var ind = CreateIndicator();
        ind.BeginRect(indicatorBaseSprite, indicatorImminentSprite,
            indicatorBaseColor, indicatorImminentColor,
            center, size, angleDegrees,
            duration, imminentFraction, 5.5f, indicatorSortingOrder, _sortingLayerId);
        if (tracked) _trackedIndicator = ind;
        return ind;
    }

    private IEnumerator WaitWithPhaseSpeed(float seconds)
    {
        float scaled = seconds / Mathf.Max(0.01f, PhaseSpeedMultiplier());
        yield return new WaitForSeconds(scaled);
    }

    public override void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        if (_invulnerable) return;
        base.TakeHit(damage, knockbackDirection, knockbackForce);
    }

    protected override void Die(DamageContext context = default)
    {
        _state = BossState.Dead;
        if (healthBarUI)
            healthBarUI.HideBar();
        ClearTelegraphs();
        base.Die();
    }

    private void UpdateTrackedIndicatorCenter(Vector2 center)
    {
        if (!_trackedIndicator) return;
        _trackedIndicator.UpdateCenter(center);
    }

    private void ForceAllIndicatorsImminent()
    {
        foreach (var ind in _activeIndicators)
        {
            if (ind) ind.ForceImminent();
        }
    }

    private void DestroyActiveIndicator()
    {
        foreach (var ind in _activeIndicators)
        {
            if (ind) Destroy(ind.gameObject);
        }
        _activeIndicators.Clear();
        _laserStretchSegments.Clear();
        _trackedIndicator = null;
    }


    private void SpawnRubbleBurst(Vector3 position, int puffs, float lifetime, Color color, Vector2 trailDirection = default)
    {
        if (puffs <= 0) return;
        bool alongPath = trailDirection.sqrMagnitude > 0.0001f;
        Vector2 dirN = alongPath ? trailDirection.normalized : Vector2.right;
        Vector2 perp = new Vector2(-dirN.y, dirN.x);

        for (int i = 0; i < puffs * 3; i++)
        {
            Vector2 offset;
            Vector2 burstVel;
            if (alongPath)
            {
                float along = Random.Range(-0.03f, 0.1f);
                float across = Random.Range(-0.11f, 0.11f);
                offset = dirN * along + perp * across + Random.insideUnitCircle * 0.035f;
                burstVel = perp * Random.Range(-0.65f, 0.65f) + dirN * Random.Range(-0.12f, 0.45f) + Random.insideUnitCircle * 0.12f;
            }
            else
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float rad = Random.Range(0.12f, 0.28f);
                offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                float a2 = Random.Range(0f, Mathf.PI * 2f);
                burstVel = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * Random.Range(0.45f, 1.15f) + Random.insideUnitCircle * 0.15f;
            }

            var go = new GameObject("BossRubblePuff");
            go.transform.position = new Vector3(position.x + offset.x, position.y + offset.y, position.z + 0.02f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            sr.color = color;
            if (spriteRenderer)
                sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder = rubbleSortingOrder;
            float s = Random.Range(1.5f, 2.5f);
            go.transform.localScale = Vector3.one * s;
            go.AddComponent<BossRubblePuffFx>().Run(sr, color, lifetime, burstVel);
        }
    }

    private static Sprite s_whiteSprite;
    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite) return s_whiteSprite;
        Texture2D t = Texture2D.whiteTexture;
        s_whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }
}

public sealed class BossRubblePuffFx : MonoBehaviour
{
    public void Run(SpriteRenderer sr, Color baseColor, float lifetime, Vector2 outwardVelocity)
    {
        StartCoroutine(RunRoutine(sr, baseColor, lifetime, outwardVelocity));
    }

    private IEnumerator RunRoutine(SpriteRenderer sr, Color baseColor, float lifetime, Vector2 velocity)
    {
        if (!sr) yield break;
        float t = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * Random.Range(1.12f, 1.42f);
        float spin = Random.Range(-180f, 180f);
        float ang = Random.Range(0f, 360f);
        while (t < lifetime && sr)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / lifetime);
            var c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, u * u);
            sr.color = c;
            transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, u));
            velocity *= Mathf.Exp(-8f * Time.deltaTime);
            transform.position += (Vector3)(velocity * Time.deltaTime);
            ang += spin * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
            yield return null;
        }
        if (sr) Destroy(sr.gameObject);
    }
}
