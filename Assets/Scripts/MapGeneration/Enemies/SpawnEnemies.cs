using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine;

using Quaternion = UnityEngine.Quaternion;

public class SpawnEnemies : MonoBehaviour
{
    [Header("Spawn layer")]
    [Tooltip("Tilemap whose painted cells are valid enemy spawn positions (dedicated layer recommended).")]
    [SerializeField] private Tilemap enemySpawnTilemap;

    [Header("Player Safety")]
    [Tooltip("Tiles within this world-space radius of the chunk's TeleportEntry will be excluded from enemy spawning so the player has a brief safe zone after teleporting in.")]
    [SerializeField] private float playerEntrySafeRadius = 4.75f;

    [Header("Ground validation")]
    [Tooltip("Raycast downward to ensure the tile is over solid geometry (skips bad tiles painted over void).")]
    [SerializeField] private bool requireGroundBelowSpawn = true;
    [SerializeField] private LayerMask groundCheckMask;
    [SerializeField] private float groundProbeUp = 2.5f;
    [SerializeField] private float groundProbeDown = 8f;

    /// <summary>
    /// Places enemies on a random subset of cells from <see cref="enemySpawnTilemap"/>.
    /// <paramref name="fillFraction"/> is the fraction of painted cells that receive an enemy (0–1).
    /// Tiles within <see cref="playerEntrySafeRadius"/> of this chunk's <c>TeleportEntry</c>
    /// are excluded so the player has breathing room when entering.
    /// </summary>
    /// <returns>List of spawned enemy GameObjects.</returns>
    public List<GameObject> SpawnEnemiesFromTileLayer(List<(GameObject prefab, float weight)> enemyPool, float fillFraction)
    {
        EnsureGroundMask();

        var spawned = new List<GameObject>();
        if (enemySpawnTilemap == null)
        {
            Debug.LogWarning($"{nameof(SpawnEnemies)} on {name}: assign {nameof(enemySpawnTilemap)}.", this);
            return spawned;
        }

        if (enemyPool == null || enemyPool.Count == 0)
            return spawned;

        fillFraction = Mathf.Clamp01(fillFraction);

        Vector3? safePoint = null;
        var entry = GetComponentInParent<TeleportEntry>() ?? GetComponentInChildren<TeleportEntry>();
        if (entry == null)
        {
            var parent = transform.parent;
            if (parent != null)
                entry = parent.GetComponentInChildren<TeleportEntry>();
        }
        if (entry != null)
            safePoint = entry.transform.position;

        float safeRadiusSqr = playerEntrySafeRadius * playerEntrySafeRadius;

        var availableTiles = new List<Vector3Int>();
        BoundsInt bounds = enemySpawnTilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (!enemySpawnTilemap.HasTile(pos))
                continue;

            if (safePoint.HasValue)
            {
                Vector3 worldPos = enemySpawnTilemap.GetCellCenterWorld(pos);
                Vector2 delta = (Vector2)worldPos - (Vector2)safePoint.Value;
                if (delta.sqrMagnitude < safeRadiusSqr)
                    continue;
            }

            availableTiles.Add(pos);
        }

        if (availableTiles.Count == 0)
        {
            Debug.LogWarning($"No spawn tiles found in chunk {name} (after safe-radius filter)");
            return spawned;
        }

        int targetCount = Mathf.Clamp(Mathf.RoundToInt(availableTiles.Count * fillFraction), 1, availableTiles.Count);

        Shuffle(availableTiles);

        Transform parentTransform = transform.parent ?? transform;

        var usedTiles = new HashSet<Vector3Int>();
        int placed = 0;

        for (int i = 0; i < availableTiles.Count && placed < targetCount; i++)
        {
            Vector3Int tilePos = availableTiles[i];
            Vector3 spawnPos = enemySpawnTilemap.GetCellCenterWorld(tilePos);
            if (!IsSpawnPositionOverGround(spawnPos))
                continue;

            GameObject prefab = GetRandomEnemyType(enemyPool);
            var go = Instantiate(prefab, spawnPos, Quaternion.identity, parentTransform);
            spawned.Add(go);
            usedTiles.Add(tilePos);
            placed++;
        }

        if (placed < targetCount && requireGroundBelowSpawn)
        {
            for (int i = 0; i < availableTiles.Count && placed < targetCount; i++)
            {
                Vector3Int tilePos = availableTiles[i];
                if (usedTiles.Contains(tilePos))
                    continue;

                Vector3 spawnPos = enemySpawnTilemap.GetCellCenterWorld(tilePos);
                GameObject prefab = GetRandomEnemyType(enemyPool);
                var go = Instantiate(prefab, spawnPos, Quaternion.identity, parentTransform);
                spawned.Add(go);
                usedTiles.Add(tilePos);
                placed++;
            }
        }

        return spawned;
    }

    /// <summary>
    /// Picks a world position on this chunk's enemy spawn tilemap near a reference point (e.g. exit teleporter).
    /// Does not use the TeleportEntry safe-radius filter so exit-adjacent tiles stay eligible.
    /// </summary>
    public bool TryPickSpawnWorldPositionNear(Vector2 referenceWorld, float minDistance, float maxDistance, out Vector3 spawnWorld)
    {
        spawnWorld = default;
        EnsureGroundMask();

        if (enemySpawnTilemap == null)
            return false;

        minDistance = Mathf.Max(0f, minDistance);
        maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);

        var candidates = new List<Vector3>();
        BoundsInt bounds = enemySpawnTilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (!enemySpawnTilemap.HasTile(pos))
                continue;

            Vector3 w = enemySpawnTilemap.GetCellCenterWorld(pos);
            float d = Vector2.Distance((Vector2)w, referenceWorld);
            if (d >= minDistance && d <= maxDistance)
                candidates.Add(w);
        }

        if (candidates.Count == 0)
        {
            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                if (!enemySpawnTilemap.HasTile(pos))
                    continue;
                candidates.Add(enemySpawnTilemap.GetCellCenterWorld(pos));
            }
        }

        ShuffleWorld(candidates);

        foreach (Vector3 w in candidates)
        {
            if (!IsSpawnPositionOverGround(w))
                continue;
            spawnWorld = w;
            return true;
        }

        return false;
    }

    private void EnsureGroundMask()
    {
        if (groundCheckMask.value == 0)
            groundCheckMask = LayerMask.GetMask("Ground", "Default", "Obstacles");
    }

    private bool IsSpawnPositionOverGround(Vector3 worldPos)
    {
        if (!requireGroundBelowSpawn || groundCheckMask.value == 0)
            return true;

        Vector2 origin = (Vector2)worldPos + Vector2.up * groundProbeUp;
        var hit = Physics2D.Raycast(origin, Vector2.down, groundProbeUp + groundProbeDown, groundCheckMask);
        return hit.collider != null;
    }

    private static void ShuffleWorld(List<Vector3> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void Shuffle(List<Vector3Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private GameObject GetRandomEnemyType(List<(GameObject prefab, float weight)> enemyPool)
    {
        float totalWeight = 0f;
        foreach (var entry in enemyPool)
            totalWeight += entry.weight;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var entry in enemyPool)
        {
            cumulative += entry.weight;
            if (roll < cumulative)
                return entry.prefab;
        }

        // Fallback to last entry in case of floating point imprecision.
        return enemyPool[enemyPool.Count - 1].prefab;
    }
}