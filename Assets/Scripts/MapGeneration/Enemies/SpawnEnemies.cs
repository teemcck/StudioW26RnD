using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine;

using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;

public class SpawnEnemies : MonoBehaviour
{
    /// <summary>
    /// Spawns enemies across a chunk using Poisson disk sampling in square around chunk. 
    /// Enemies that fall outside the mapped outside the chunk are voided. 
    /// </summary>
    /// <param name="tm">The tilemap defining valid spawn area.</param>
    /// <param name="spacing">Minimum world-space distance between each spawned enemy.</param>
    /// <param name="enemyPrefabs">Each entry pairs an enemy prefab with its relative spawn weight.</param>
    public void SpawnEnemiesOnChunk(Tilemap map, List<(GameObject prefab, float weight)> enemyPool, float spacing)
    {
        // This might need to be modified later to be less area dependent.
        Vector2 chunkWorldPos = transform.position;
        Vector2 spawnAreaSize = map.localBounds.size;

        List<Vector2> spawnPoints = Poisson.GeneratePoint(spacing, spawnAreaSize);

        foreach (Vector2 point in spawnPoints)
        {
            Vector2 worldPos = chunkWorldPos + point;
            GameObject enemyObject = GetRandomEnemyType(enemyPool);

            Instantiate(enemyObject, worldPos, Quaternion.identity);
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