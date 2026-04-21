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
    [SerializeField] private float playerEntrySafeRadius = 3.5f;

    /// <summary>
    /// Places enemies on a random subset of cells from <see cref="enemySpawnTilemap"/>.
    /// <paramref name="fillFraction"/> is the fraction of painted cells that receive an enemy (0–1).
    /// Tiles within <see cref="playerEntrySafeRadius"/> of this chunk's <c>TeleportEntry</c>
    /// are excluded so the player has breathing room when entering.
    /// </summary>
    /// <returns>List of spawned enemy GameObjects.</returns>
    public List<GameObject> SpawnEnemiesFromTileLayer(List<(GameObject prefab, float weight)> enemyPool, float fillFraction)
    {
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

        for (int i = 0; i < targetCount && i < availableTiles.Count; i++)
        {
            Vector3Int tilePos = availableTiles[i];

            Vector3 spawnPos = enemySpawnTilemap.GetCellCenterWorld(tilePos);

            GameObject prefab = GetRandomEnemyType(enemyPool);

            var go = Instantiate(prefab, spawnPos, Quaternion.identity, parentTransform);
            spawned.Add(go);
        }

        return spawned;
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