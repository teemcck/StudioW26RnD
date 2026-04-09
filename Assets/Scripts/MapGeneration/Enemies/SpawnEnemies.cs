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
        var cells = new List<Vector3Int>();
        BoundsInt bounds = enemySpawnTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (enemySpawnTilemap.HasTile(pos))
                cells.Add(pos);
        }

        if (cells.Count == 0)
            return 0;

        int targetCount = Mathf.Clamp(Mathf.RoundToInt(cells.Count * fillFraction), 0, cells.Count);
        if (targetCount == 0)
            return 0;

        Shuffle(cells);

        Transform parent = transform;
        int spawned = 0;
        for (int i = 0; i < targetCount; i++)
        {
            Vector3 worldPos = enemySpawnTilemap.GetCellCenterWorld(cells[i]);
            GameObject prefab = GetRandomEnemyType(enemyPool);
            Instantiate(prefab, worldPos, Quaternion.identity, parent);
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