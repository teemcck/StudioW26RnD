using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

[System.Serializable]
public class WeightedEnemyPrefab
{
    public GameObject prefab;
    [Min(0.001f)] public float weight = 1f;
}

public class MapSpawner : MonoBehaviour
{
    public static MapSpawner Instance { get; private set; }

    [Header("Map Generation Settings")]
    [SerializeField] private float chunkSpacing = 5f;

    [Header("Enemy spawn (tile layer)")]
    [SerializeField] private List<WeightedEnemyPrefab> worldOneEnemyPool = new List<WeightedEnemyPrefab>();
    [SerializeField] private List<WeightedEnemyPrefab> worldTwoEnemyPool = new List<WeightedEnemyPrefab>();

    [Header("References")]
    [SerializeField] private ChunkGen chunkGen;
    [SerializeField] private Transform chunkContainer;

    private List<GameObject> _chunks = new List<GameObject>();
    private float _chunkOffset;

    public int MinNumChunks => GameConstants.MinChunkCount;
    public int MaxNumChunks => GameConstants.MaxChunkCount;

    /// <summary>Total enemies spawned in the last <see cref="GenerateRandomSequence"/> call.</summary>
    public int LastSpawnedEnemyCount { get; private set; }

    /// <summary>
    /// Referenced by GameplayHandler.cs.
    /// Used to place player object at the beginning of the level.
    /// </summary>
    public Vector2 SpawnPosition {get; private set;}

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (chunkGen == null)
            Debug.LogError("ChunkGen reference is missing in MapSpawner.");
        if (chunkContainer == null)
            Debug.LogError("ChunkContainer reference is missing in MapSpawner.");
        if (worldOneEnemyPool == null || worldOneEnemyPool.Count == 0)
            Debug.LogError("World 1 enemy pool is empty in MapSpawner.");
        if (worldTwoEnemyPool == null || worldTwoEnemyPool.Count == 0)
            Debug.LogError("World 2 enemy pool is empty in MapSpawner.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    
    public List<GameObject> GenerateRandomSequence(int difficulty, int numChunks, WorldBand band)
    {
        ResetMap();
        LastSpawnedEnemyCount = 0;

        float fillFraction = DifficultyToFillFraction(difficulty);
        var weightedPool = BuildWeightedPool(band);
        Debug.Log($"Generating {numChunks} chunks; difficulty {difficulty} → fill {fillFraction:P0}.");

        int floorIndex = GameplayHandler.Instance != null ? GameplayHandler.Instance.CurrentFloorIndex : 0;
        float healthMult = FloorScalingCurve.GetHealthMult(floorIndex);
        float damageMult = FloorScalingCurve.GetDamageMult(floorIndex);

        var allSpawned = new List<GameObject>();

        for (int i = 0; i < numChunks; i++)
        {
            GameObject prefab = chunkGen.GetRandomMapChunk(band);
            if (prefab == null)
            {
                Debug.LogError($"Chunk {i} prefab is null, skipping.");
                continue;
            }

            GameObject chunk = Instantiate(prefab, chunkContainer);
            chunk.name = prefab.name;
            chunk.transform.position = new Vector3(_chunkOffset, 0, 0);
            Physics2D.SyncTransforms();
            _chunks.Add(chunk);

            Tilemap tm = chunk.GetComponentInChildren<Tilemap>();
            float width = tm != null ? tm.localBounds.size.x : 10f;
            if (tm == null)
                Debug.LogWarning($"No Tilemap on {prefab.name}, defaulting to 10 unit width.");

            SpawnEnemies spawn = chunk.GetComponentInChildren<SpawnEnemies>();
            if (spawn != null && weightedPool.Count > 0)
            {
                var spawnedHere = spawn.SpawnEnemiesFromTileLayer(weightedPool, fillFraction);
                allSpawned.AddRange(spawnedHere);
                LastSpawnedEnemyCount += spawnedHere.Count;
            }

            _chunkOffset += width + chunkSpacing;
        }

        ApplyFloorScaling(allSpawned, healthMult, damageMult);

        if (_chunks.Count > 0)
            SpawnPosition = _chunks[0].GetComponentInChildren<TeleportEntry>().transform.position;

        LinkTeleporters();

        if (_chunks.Count > 0 && weightedPool.Count > 0)
        {
            int eliteCount = DetermineEliteCountForFloor(floorIndex);
            for (int i = 0; i < eliteCount; i++)
                SpawnEliteAtEndpoint(_chunks[_chunks.Count - 1], weightedPool, healthMult, damageMult, i);
        }

        return _chunks;
    }

    private static int DetermineEliteCountForFloor(int floorIndex)
    {
        bool milestone = WorldProgression.IsBossFloor(floorIndex + 1) || WorldProgression.IsWorldTwoTransition(floorIndex + 1);
        bool lateFloor = floorIndex >= 3;
        return (milestone || (lateFloor && Random.value < 0.35f)) ? 2 : 1;
    }

    private static void ApplyFloorScaling(List<GameObject> enemies, float healthMult, float damageMult)
    {
        if (enemies == null || enemies.Count == 0)
            return;
        if (Mathf.Approximately(healthMult, 1f) && Mathf.Approximately(damageMult, 1f))
            return;

        for (int i = 0; i < enemies.Count; i++)
        {
            var go = enemies[i];
            if (go == null) continue;
            var enemy = go.GetComponent<EnemyBase>();
            if (enemy == null) continue;
            enemy.ApplyRuntimeScaling(healthMult, 1f, damageMult);
        }
    }

    private void SpawnEliteAtEndpoint(GameObject lastChunk, List<(GameObject prefab, float weight)> weightedPool, float healthMult, float damageMult, int indexInGroup)
    {
        var tp = lastChunk.GetComponentInChildren<Teleporter>();
        Vector3 center = tp != null ? tp.transform.position : lastChunk.transform.position;
        Vector2 teleRef = (Vector2)center;

        Vector3 spawnWorld = default;
        bool placedOnTilemap = false;
        var spawners = lastChunk.GetComponentsInChildren<SpawnEnemies>(true);
        foreach (var spawner in spawners)
        {
            if (spawner == null)
                continue;
            if (spawner.TryPickSpawnWorldPositionNear(teleRef, 0.35f, 11f, out spawnWorld))
            {
                placedOnTilemap = true;
                break;
            }
        }

        if (!placedOnTilemap)
            spawnWorld = FindEliteSpawnOnGroundNear(teleRef, lastChunk.transform.position.z, indexInGroup);

        GameObject prefab = weightedPool[Random.Range(0, weightedPool.Count)].prefab;
        if (prefab == null) return;

        var go = Instantiate(prefab, spawnWorld, Quaternion.identity, lastChunk.transform);
        if (go.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.simulated = true;
            rb.WakeUp();
        }

        Physics2D.SyncTransforms();

        var enemy = go.GetComponent<EnemyBase>();
        if (enemy != null)
            enemy.ApplyRuntimeScaling(healthMult, 1f, damageMult);

        if (go.GetComponent<EliteModifier>() == null)
            go.AddComponent<EliteModifier>();

        LastSpawnedEnemyCount++;
    }

    /// <summary>
    /// Last resort: raycast down from above the teleporter — requires a hit (solid ground), never "empty air".
    /// </summary>
    private static Vector3 FindEliteSpawnOnGroundNear(Vector2 teleporterWorld, float chunkZ, int indexInGroup)
    {
        int mask = LayerMask.GetMask("Ground", "Default", "Obstacles");
        if (mask == 0)
            mask = Physics2D.DefaultRaycastLayers;

        float baseAngle = indexInGroup * 77.3f;
        for (int ring = 0; ring < 14; ring++)
        {
            float r = 0.35f + ring * 0.42f;
            int spokes = 8 + ring / 2;
            for (int s = 0; s < spokes; s++)
            {
                float ang = (baseAngle + s * (360f / spokes)) * Mathf.Deg2Rad;
                Vector2 horizontal = teleporterWorld + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                Vector2 origin = horizontal + Vector2.up * 14f;
                var hit = Physics2D.Raycast(origin, Vector2.down, 28f, mask);
                if (hit.collider != null)
                    return new Vector3(hit.point.x, hit.point.y + 0.12f, chunkZ);
            }
        }

        Vector2 up = teleporterWorld + Vector2.up * 14f;
        var lastHit = Physics2D.Raycast(up, Vector2.down, 28f, mask);
        if (lastHit.collider != null)
            return new Vector3(lastHit.point.x, lastHit.point.y + 0.12f, chunkZ);

        return new Vector3(teleporterWorld.x, teleporterWorld.y, chunkZ);
    }

    float DifficultyToFillFraction(int difficulty)
    {
        float t = Mathf.InverseLerp(GameConstants.MinDifficulty, GameConstants.MaxDifficulty, difficulty);
        float percent = Mathf.Lerp(GameConstants.MinEnemyFillPercent, GameConstants.MaxEnemyFillPercent, t);
        return Mathf.Clamp01(percent / 100f);
    }

    List<(GameObject prefab, float weight)> BuildWeightedPool(WorldBand band)
    {
        var list = new List<(GameObject, float)>();
        var sourcePool = GetEnemyPoolForBand(band);
        if (sourcePool == null)
            return list;

        foreach (var entry in sourcePool)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0f)
                list.Add((entry.prefab, entry.weight));
        }

        return list;
    }

    private List<WeightedEnemyPrefab> GetEnemyPoolForBand(WorldBand band)
    {
        return band switch
        {
            WorldBand.WorldOne when worldOneEnemyPool != null && worldOneEnemyPool.Count > 0 => worldOneEnemyPool,
            WorldBand.WorldTwo when worldTwoEnemyPool != null && worldTwoEnemyPool.Count > 0 => worldTwoEnemyPool,
            _ => null
        };
    }

    // This is a meme function for the debug menu.
    public GameObject SpawnDebugRandomEnemyNear(Vector2 center, float radius = 2.5f)
    {
        WorldBand band = GameplayHandler.Instance != null
            ? WorldProgression.GetBandForFloor(GameplayHandler.Instance.CurrentFloorIndex)
            : WorldBand.WorldOne;

        List<(GameObject prefab, float weight)> pool = BuildWeightedPool(band);
        if (pool.Count == 0)
            return null;

        GameObject prefab = GetRandomPrefab(pool);
        if (!prefab)
            return null;

        Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Max(0.5f, radius);
        return Instantiate(prefab, center + offset, Quaternion.identity, chunkContainer ? chunkContainer : transform);
    }

    private void LinkTeleporters()
    {
        int count = _chunks.Count;

        // Link each teleporter to the entry point of the next chunk.
        // The last chunk's teleporter is intentionally left with destination = null.
        // Teleporter.cs checks for this and raises PlayerReachedEndpointEvent instead.
        for (int i = 0; i < count - 1; i++)
        {
            Teleporter tp = _chunks[i].GetComponentInChildren<Teleporter>();
            TeleportEntry entry = _chunks[i + 1].GetComponentInChildren<TeleportEntry>();

            if (tp == null)
            {
                Debug.LogWarning($"No Teleporter on chunk {i} ({_chunks[i].name}).");
                continue;
            }
            if (entry == null)
            {
                Debug.LogWarning($"No TeleportEntry on chunk {i + 1} ({_chunks[i + 1].name}).");
                continue;
            }

            tp.destination = entry.transform;
        }
    }

    private void ResetMap()
    {
        foreach (GameObject chunk in _chunks)
            Destroy(chunk);

        _chunks.Clear();
        _chunkOffset = 0f;
    }

    public bool ArePositionsInSameChunk(Vector2 a, Vector2 b)
    {
        if (!TryGetChunkIndexAtWorldPosition(a, out int aIdx)) return false;
        if (!TryGetChunkIndexAtWorldPosition(b, out int bIdx)) return false;
        return aIdx == bIdx;
    }

    public bool TryGetChunkIndexAtWorldPosition(Vector2 worldPos, out int index)
    {
        index = -1;
        for (int i = 0; i < _chunks.Count; i++)
        {
            GameObject chunk = _chunks[i];
            if (!chunk) continue;

            if (!TryGetMergedTilemapWorldBounds(chunk, out Bounds worldBounds))
                continue;

            Vector3 p = new Vector3(worldPos.x, worldPos.y, worldBounds.center.z);
            if (worldBounds.Contains(p))
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    public bool TryGetChunkWorldBoundsAtWorldPosition(Vector2 worldPos, out Bounds bounds)
    {
        bounds = default;
        for (int i = 0; i < _chunks.Count; i++)
        {
            GameObject chunk = _chunks[i];
            if (!chunk) continue;

            if (!TryGetMergedTilemapWorldBounds(chunk, out Bounds worldBounds))
                continue;

            Vector3 p = new Vector3(worldPos.x, worldPos.y, worldBounds.center.z);
            if (!worldBounds.Contains(p))
                continue;

            bounds = worldBounds;
            return true;
        }

        return false;
    }

    private static bool TryGetMergedTilemapWorldBounds(GameObject chunk, out Bounds merged)
    {
        merged = default;
        Tilemap[] maps = chunk.GetComponentsInChildren<Tilemap>(true);
        if (maps == null || maps.Length == 0)
            return false;

        merged = GetTilemapWorldBounds(maps[0]);
        for (int i = 1; i < maps.Length; i++)
            merged.Encapsulate(GetTilemapWorldBounds(maps[i]));
        return true;
    }

    private static Bounds GetTilemapWorldBounds(Tilemap tilemap)
    {
        Bounds local = tilemap.localBounds;
        Vector3 worldCenter = tilemap.transform.TransformPoint(local.center);
        Vector3 lossy = tilemap.transform.lossyScale;
        Vector3 worldSize = new Vector3(
            Mathf.Abs(local.size.x * lossy.x),
            Mathf.Abs(local.size.y * lossy.y),
            Mathf.Abs(local.size.z * lossy.z));
        return new Bounds(worldCenter, worldSize);
    }

    private static GameObject GetRandomPrefab(List<(GameObject prefab, float weight)> pool)
    {
        float totalWeight = 0f;
        foreach (var entry in pool)
            totalWeight += entry.weight;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
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
