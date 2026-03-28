using UnityEngine;
using System.Collections.Generic;

public class ChunkGen : MonoBehaviour
{
    public static ChunkGen Instance { get; private set; }

    [Header("List of all possible map \"chunks\" to select from")]
    [SerializeField] private List<GameObject> chunkPool;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (chunkPool == null || chunkPool.Count == 0)
            Debug.LogError("Chunk pool is empty in ChunkGen.");
    }

    public GameObject GetRandomMapChunk()
    {
        if (chunkPool == null || chunkPool.Count == 0)
        {
            Debug.LogError("Chunk pool is empty, cannot get a chunk.");
            return null;
        }
        return chunkPool[Random.Range(0, chunkPool.Count)];
    }
}