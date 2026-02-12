using UnityEngine;
using System.Collections.Generic;

public class ChunkGen : MonoBehaviour
{
    [Header("List of all possible map \"chunks\" to select from")]
    [SerializeField] private List<GameObject> chunkPool; 
    private ChunkGen instance;
    
    private int chunkLen;

    private void Awake()
    {
        // Replace existing if necessary.
        if (instance != null)
        {
            Destroy(instance);
            instance = this;
        }

        if (chunkPool == null || chunkPool.Count == 0)
        {
            Debug.LogError("Chunk pool is empty in ChunkGen.");
        }
    }

    private void Start() {
        chunkLen = chunkPool.Count;
    }

    public GameObject GetRandomMapChunk()
    {
        int random = Random.Range(0, chunkLen);
        return chunkPool[random];
    }
}