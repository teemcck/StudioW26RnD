using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine;

using Quaternion = UnityEngine.Quaternion;

public class SpawnEnemies : MonoBehaviour
{
    [Header("Spawn layer")]
    [Tooltip("Tilemap whose painted cells are valid enemy spawn positions (dedicated layer recommended).")]
    [SerializeField] private Tilemap enemySpawnTilemap;

    /// <summary>
    /// Places enemies on a random subset of cells from <see cref="enemySpawnTilemap"/>.
    /// <paramref name="fillFraction"/> is the fraction of painted cells that receive an enemy (0–1).
    /// </summary>
    /// <returns>Number of enemies spawned.</returns>
    public int SpawnEnemiesFromTileLayer(List<(GameObject prefab, float weight)> enemyPool, float fillFraction)
    {
        if (enemySpawnTilemap == null)
        {
            Debug.LogWarning($"{nameof(SpawnEnemies)} on {name}: assign {nameof(enemySpawnTilemap)}.", this);
            return 0;
        }

        if (enemyPool == null || enemyPool.Count == 0)
            return 0;

        fillFraction = Mathf.Clamp01(fillFraction);

        // Get all tiles that have something painted on them
        var availableTiles = new List<Vector3Int>();
        BoundsInt bounds = enemySpawnTilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (enemySpawnTilemap.HasTile(pos))
            {
                availableTiles.Add(pos);
            }
        }

        if (availableTiles.Count == 0)
        {
            Debug.LogWarning($"No spawn tiles found in chunk {name}");
            return 0;
        }

        // Determine how many enemies to spawn
        int targetCount = Mathf.Clamp(Mathf.RoundToInt(availableTiles.Count * fillFraction), 1, availableTiles.Count);

        // Shuffle the available tiles to randomize which ones get enemies
        Shuffle(availableTiles);

        // Spawn enemies at the center of randomly selected tiles
        Transform parent = transform.parent ?? transform; // Spawn as children of chunk root
        int spawned = 0;

        for (int i = 0; i < targetCount && i < availableTiles.Count; i++)
        {
            Vector3Int tilePos = availableTiles[i];

            // Get the world position of the tile center
            Vector3 spawnPos = enemySpawnTilemap.GetCellCenterWorld(tilePos);

            // Select random enemy type
            GameObject prefab = GetRandomEnemyType(enemyPool);

            // Spawn the enemy
            Instantiate(prefab, spawnPos, Quaternion.identity, parent);
            spawned++;
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