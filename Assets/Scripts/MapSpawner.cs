using UnityEngine;
using System.Collections.Generic;

public class MapSpawner : MonoBehaviour
{
    [Header("Map Generation Settings")]
    [SerializeField] private int minNumChunks;
    [SerializeField] private int maxNumChunks;
    [Header("Chunk Generation References")]
    [SerializeField] private ChunkGen chunkGen;
    [SerializeField] private Transform chunkContainer;
    private List<GameObject> chunks;
    private float chunkOffset = 0; // Stores how much to offset the next chunk by.

    private void Awake()
    {
        if (chunkGen == null)
        {
            Debug.LogError("ChunkGen reference is missing in MapSpawner.");
        }
        if (chunkContainer == null)
        {
            Debug.LogError("ChunkContainer reference is missing in MapSpawner.");
        }
    }

    public List<GameObject> GenerateSequence()
    {
        int numChunks = Random.Range(minNumChunks, maxNumChunks + 1);
        Debug.Log($"Generating {numChunks} chunks.");
        for (int i = 0; i < numChunks; ++i)
        {
            GameObject chunk = Instantiate(chunkGen.GetRandomMapChunk(), chunkContainer);
            chunk.transform.position = new Vector3(chunkOffset, 0, 0);
            chunks.Add(chunk);
            chunkOffset += chunk.transform.localScale.x + 10; // Update offset for next chunk.
        }
        return chunks;
    }
}