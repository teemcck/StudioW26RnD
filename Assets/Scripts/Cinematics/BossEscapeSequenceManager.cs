using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class BossEscapeSequenceManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;

    [SerializeField] private Tilemap enemySpawnTilemap;

    [SerializeField] private Collider2D spawnRegion;

    [SerializeField] private Transform spawnParent;

    [SerializeField] private Transform escapeTeleporterPoint;

    [SerializeField] private Transform playerSpawnPoint;

    [SerializeField] private List<WeightedEnemyPrefab> enemyPrefabs = new List<WeightedEnemyPrefab>();

    [Header("Teleporter reveal")]
    [SerializeField] private float teleporterRevealAfterFirstRumbleSeconds = 0.35f;

    [Header("World rumble")]
    [SerializeField] private float rumbleShakeIntensity = 0.26f;
    [SerializeField] private float rumbleIntervalMinSeconds = 0.22f;
    [SerializeField] private float rumbleIntervalMaxSeconds = 0.55f;

    [Header("Spawn")]
    [SerializeField] private int immediateSpawnCount = 5;
    [SerializeField] private float minTileDistanceFromPlayer = 3f;
    [SerializeField] private float maxTileDistanceFromPlayer = 8f;
    [SerializeField] private float maxTileDistanceFromTeleporter = 10f;
    [SerializeField] private int spawnRegionPresampleCount = 200;
    [SerializeField] private float spawnCooldownMinSeconds = 1.5f;
    [SerializeField] private float spawnCooldownMaxSeconds = 4f;
    [SerializeField] private int maxAliveEnemies = 10;
    [SerializeField] private int maxTotalSpawns = 30;

    [Header("Difficulty")]
    [Tooltip("Multiplies final-floor scaling so escape spawns are slightly tougher than the last normal floor.")]
    [SerializeField] private float escapeEnemyHealthScaleBonus = 1.16f;
    [SerializeField] private float escapeEnemyDamageScaleBonus = 1.12f;

    private Coroutine _spawnLoop;
    private Coroutine _rumbleLoop;
    private bool _escapeActive;
    private int _totalSpawned;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private List<Vector2> _spawnCandidateWorlds = new List<Vector2>();

    private bool HasSpawnArea =>
        enemySpawnTilemap != null || spawnRegion != null;

    public IEnumerator RunTeleporterRevealRumble(CameraController camOverride)
    {
        CameraController cam = camOverride ? camOverride : ResolveCamera();
        if (!cam)
            yield break;

        cam.ShakeRumble(rumbleShakeIntensity);
        yield return new WaitForSeconds(Mathf.Max(0f, teleporterRevealAfterFirstRumbleSeconds));
    }

    public void BeginEscapePhase()
    {
        if (_escapeActive)
            return;

        _escapeActive = true;
        if (!cameraController)
            cameraController = ResolveCamera();

        if (_rumbleLoop == null)
            _rumbleLoop = StartCoroutine(EscapeRumbleLoop());

        if (_spawnLoop == null && enemyPrefabs != null && enemyPrefabs.Count > 0 && HasSpawnArea)
            _spawnLoop = StartCoroutine(EscapeSpawnLoop());
    }

    public void StopEscapeSequence()
    {
        _escapeActive = false;
        if (_rumbleLoop != null)
        {
            StopCoroutine(_rumbleLoop);
            _rumbleLoop = null;
        }

        if (_spawnLoop != null)
        {
            StopCoroutine(_spawnLoop);
            _spawnLoop = null;
        }
    }

    private void OnDestroy()
    {
        StopEscapeSequence();
    }

    private CameraController ResolveCamera()
    {
        if (cameraController)
            return cameraController;
        return FindFirstObjectByType<CameraController>();
    }

    private Transform ResolvePlayerTransform()
    {
        if (playerSpawnPoint)
            return playerSpawnPoint;
        var go = GameObject.FindGameObjectWithTag("Player");
        return go ? go.transform : null;
    }

    private float GetTileWorldSize()
    {
        if (enemySpawnTilemap)
            return (enemySpawnTilemap.cellSize.x + enemySpawnTilemap.cellSize.y) * 0.5f;
        return 1f;
    }

    private void RebuildSpawnCandidates()
    {
        _spawnCandidateWorlds.Clear();
        if (enemySpawnTilemap != null)
        {
            BoundsInt bounds = enemySpawnTilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!enemySpawnTilemap.HasTile(cell))
                    continue;
                _spawnCandidateWorlds.Add(enemySpawnTilemap.GetCellCenterWorld(cell));
            }

            return;
        }

        if (spawnRegion != null)
        {
            Bounds b = spawnRegion.bounds;
            for (int i = 0; i < spawnRegionPresampleCount; i++)
            {
                _spawnCandidateWorlds.Add(new Vector2(
                    Random.Range(b.min.x, b.max.x),
                    Random.Range(b.min.y, b.max.y)));
            }
        }
    }

    private static float DistanceInTiles(Vector2 a, Vector2 b, float tileWorldSize)
    {
        return Vector2.Distance(a, b) / Mathf.Max(0.0001f, tileWorldSize);
    }

    private Vector2? PickSpawnWorldPosition()
    {
        if (_spawnCandidateWorlds.Count == 0)
            return null;

        Transform playerTf = ResolvePlayerTransform();
        Vector2 playerPos = playerTf ? (Vector2)playerTf.position : Vector2.zero;
        Vector2 telePos = escapeTeleporterPoint ? (Vector2)escapeTeleporterPoint.position : playerPos;

        float ts = GetTileWorldSize();

        bool teleporterPriority = _totalSpawned >= 5 && _totalSpawned % 5 == 0;

        var playerRing = new List<Vector2>();
        var teleRing = new List<Vector2>();

        foreach (Vector2 w in _spawnCandidateWorlds)
        {
            if (playerTf)
            {
                float dp = DistanceInTiles(w, playerPos, ts);
                if (dp > minTileDistanceFromPlayer && dp < maxTileDistanceFromPlayer)
                    playerRing.Add(w);
            }

            if (escapeTeleporterPoint)
            {
                float dt = DistanceInTiles(w, telePos, ts);
                if (dt <= maxTileDistanceFromTeleporter)
                    teleRing.Add(w);
            }
        }

        if (teleporterPriority)
        {
            if (teleRing.Count > 0)
                return teleRing[Random.Range(0, teleRing.Count)];
            if (playerRing.Count > 0)
                return playerRing[Random.Range(0, playerRing.Count)];
        }
        else
        {
            if (playerRing.Count > 0)
                return playerRing[Random.Range(0, playerRing.Count)];
            if (teleRing.Count > 0)
                return teleRing[Random.Range(0, teleRing.Count)];
        }

        return null;
    }

    private bool TrySpawnOne(Transform parent, List<(GameObject prefab, float weight)> pool)
    {
        if (_totalSpawned >= maxTotalSpawns)
            return false;
        PruneSpawnedList();
        if (_spawned.Count >= maxAliveEnemies)
            return false;

        Vector2? pos = PickSpawnWorldPosition();
        if (!pos.HasValue)
            return false;

        GameObject prefab = GetRandomPrefab(pool);
        if (!prefab)
            return false;

        GameObject instance = Instantiate(prefab, pos.Value, Quaternion.identity, parent);
        ApplyEscapeEnemyScaling(instance);
        _spawned.Add(instance);
        _totalSpawned++;
        return true;
    }

    private void ApplyEscapeEnemyScaling(GameObject instance)
    {
        if (instance == null)
            return;

        int floorIdx = Mathf.Max(0, WorldProgression.BossFloorIndex - 1);
        if (GameplayHandler.Instance != null)
            floorIdx = Mathf.Max(floorIdx, GameplayHandler.Instance.CurrentFloorIndex);

        float healthMult = FloorScalingCurve.GetHealthMult(floorIdx);
        float damageMult = FloorScalingCurve.GetDamageMult(floorIdx);

        var enemy = instance.GetComponent<EnemyBase>();
        if (enemy == null)
            return;

        float hBonus = Mathf.Max(1f, escapeEnemyHealthScaleBonus);
        float dBonus = Mathf.Max(1f, escapeEnemyDamageScaleBonus);

        enemy.ApplyRuntimeScaling(healthMult * hBonus, 1f, damageMult * dBonus);
    }

    private IEnumerator EscapeRumbleLoop()
    {
        while (_escapeActive)
        {
            if (cameraController)
                cameraController.ShakeRumble(rumbleShakeIntensity);
            yield return new WaitForSeconds(Random.Range(rumbleIntervalMinSeconds, rumbleIntervalMaxSeconds));
        }

        _rumbleLoop = null;
    }

    private IEnumerator EscapeSpawnLoop()
    {
        var pool = BuildWeightedPool();
        if (pool.Count == 0)
        {
            _spawnLoop = null;
            yield break;
        }

        Transform parent = spawnParent ? spawnParent : transform;
        RebuildSpawnCandidates();
        if (_spawnCandidateWorlds.Count == 0)
        {
            _spawnLoop = null;
            yield break;
        }

        int burst = Mathf.Min(immediateSpawnCount, maxTotalSpawns);
        for (int i = 0; i < burst && _escapeActive; i++)
        {
            if (!TrySpawnOne(parent, pool))
                break;
        }

        int nullSpawnStreak = 0;
        while (_escapeActive && _totalSpawned < maxTotalSpawns)
        {
            PruneSpawnedList();
            if (_spawned.Count >= maxAliveEnemies)
            {
                yield return new WaitForSeconds(0.35f);
                continue;
            }

            if (!TrySpawnOne(parent, pool))
            {
                nullSpawnStreak++;
                if (nullSpawnStreak >= 12)
                    break;

                yield return new WaitForSeconds(0.5f);
                continue;
            }

            nullSpawnStreak = 0;
            yield return new WaitForSeconds(Random.Range(spawnCooldownMinSeconds, spawnCooldownMaxSeconds));
        }

        _spawnLoop = null;
    }

    private void PruneSpawnedList()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] == null)
                _spawned.RemoveAt(i);
        }
    }

    private static List<(GameObject prefab, float weight)> BuildWeightedPoolFromInspector(List<WeightedEnemyPrefab> list)
    {
        var pool = new List<(GameObject, float)>();
        if (list == null)
            return pool;
        foreach (var entry in list)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0f)
                pool.Add((entry.prefab, entry.weight));
        }

        return pool;
    }

    private List<(GameObject prefab, float weight)> BuildWeightedPool()
    {
        return BuildWeightedPoolFromInspector(enemyPrefabs);
    }

    private static GameObject GetRandomPrefab(List<(GameObject prefab, float weight)> pool)
    {
        float totalWeight = 0f;
        foreach (var entry in pool)
            totalWeight += entry.weight;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var entry in pool)
        {
            cumulative += entry.weight;
            if (roll < cumulative)
                return entry.prefab;
        }

        return pool[pool.Count - 1].prefab;
    }
}
