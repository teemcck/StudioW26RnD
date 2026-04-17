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
    [SerializeField] private List<WeightedEnemyPrefab> enemyPool = new List<WeightedEnemyPrefab>();

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
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    
    public List<GameObject> GenerateRandomSequence(int difficulty)
    {
        ResetMap();
        LastSpawnedEnemyCount = 0;

        int numChunks = Random.Range(GameConstants.MinChunkCount, GameConstants.MaxChunkCount + 1);
        float fillFraction = DifficultyToFillFraction(difficulty);
        var weightedPool = BuildWeightedPool();
        Debug.Log($"Generating {numChunks} chunks; difficulty {difficulty} → fill {fillFraction:P0}.");

        for (int i = 0; i < numChunks; i++)
        {
            GameObject prefab = chunkGen.GetRandomMapChunk();
            if (prefab == null)
            {
                Debug.LogError($"Chunk {i} prefab is null, skipping.");
                continue;
            }

            GameObject chunk = Instantiate(prefab, chunkContainer);
            chunk.name = prefab.name;
            chunk.transform.position = new Vector3(_chunkOffset, 0, 0);
            _chunks.Add(chunk);

            Tilemap tm = chunk.GetComponentInChildren<Tilemap>();
            float width = tm != null ? tm.localBounds.size.x : 10f;
            if (tm == null)
                Debug.LogWarning($"No Tilemap on {prefab.name}, defaulting to 10 unit width.");

            SpawnEnemies spawn = chunk.GetComponentInChildren<SpawnEnemies>();
            if (spawn != null && weightedPool.Count > 0)
                LastSpawnedEnemyCount += spawn.SpawnEnemiesFromTileLayer(weightedPool, fillFraction);

            _chunkOffset += width + chunkSpacing;
        }

        // Initial spawn position determined by TeleportEntry of initial chunk.
        SpawnPosition = _chunks[0].GetComponentInChildren<TeleportEntry>().transform.position;

        LinkTeleporters();
        return _chunks;
    }

    float DifficultyToFillFraction(int difficulty)
    {
        float t = Mathf.InverseLerp(GameConstants.MinDifficulty, GameConstants.MaxDifficulty, difficulty);
        float percent = Mathf.Lerp(GameConstants.MinEnemyFillPercent, GameConstants.MaxEnemyFillPercent, t);
        return Mathf.Clamp01(percent / 100f);
    }

    List<(GameObject prefab, float weight)> BuildWeightedPool()
    {
        var list = new List<(GameObject, float)>();
        if (enemyPool == null)
            return list;

        foreach (var entry in enemyPool)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0f)
                list.Add((entry.prefab, entry.weight));
        }

        return list;
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

            Tilemap tm = chunk.GetComponentInChildren<Tilemap>();
            if (!tm) continue;

            Bounds worldBounds = GetTilemapWorldBounds(tm);
            Vector3 p = new Vector3(worldPos.x, worldPos.y, worldBounds.center.z);
            if (worldBounds.Contains(p))
            {
                index = i;
                return true;
            }
        }

        return false;
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
}