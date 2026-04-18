using UnityEngine;
using System.Collections.Generic;

public class ChunkGen : MonoBehaviour
{
    public static ChunkGen Instance { get; private set; }

    [Header("World Chunk Pools")]
    [SerializeField] private List<GameObject> worldOneChunkPool = new();
    [SerializeField] private List<GameObject> worldTwoChunkPool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (worldOneChunkPool == null || worldOneChunkPool.Count == 0)
            Debug.LogError("World 1 chunk pool is empty in ChunkGen.");
        if (worldTwoChunkPool == null || worldTwoChunkPool.Count == 0)
            Debug.LogError("World 2 chunk pool is empty in ChunkGen.");
    }

    public GameObject GetRandomMapChunk(WorldBand band)
    {
        var activePool = GetPoolForBand(band);
        if (activePool == null || activePool.Count == 0)
        {
            Debug.LogError($"Chunk pool for {band} is empty, cannot get a chunk.");
            return null;
        }
        return activePool[Random.Range(0, activePool.Count)];
    }

    private List<GameObject> GetPoolForBand(WorldBand band)
    {
        return band switch
        {
            WorldBand.WorldOne when worldOneChunkPool != null && worldOneChunkPool.Count > 0 => worldOneChunkPool,
            WorldBand.WorldTwo when worldTwoChunkPool != null && worldTwoChunkPool.Count > 0 => worldTwoChunkPool,
            _ => null
        };
    }
}
