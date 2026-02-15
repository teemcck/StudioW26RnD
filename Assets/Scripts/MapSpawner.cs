using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class MapSpawner : MonoBehaviour
{
    [Header("Map Generation Settings")]
    [SerializeField] private int minNumChunks;
    [SerializeField] private int maxNumChunks;
    [Header("Chunk Generation References")]
    [SerializeField] private ChunkGen chunkGen;
    [SerializeField] private Transform chunkContainer;
    private List<GameObject> _chunks = new List<GameObject>();
    private float _chunkOffset;

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
        ResetMap();
        int numChunks = Random.Range(minNumChunks, maxNumChunks + 1);
    
        for (int i = 0; i < numChunks; ++i)
        {
            GameObject chunkPrefab = chunkGen.GetRandomMapChunk();
            GameObject chunk = Instantiate(chunkPrefab, chunkContainer);
            
            chunk.transform.position = new Vector3(_chunkOffset, 0, 0);
            _chunks.Add(chunk);

            Tilemap tm = chunk.GetComponentInChildren<Tilemap>();
            
            // Calculate cumulative offset (since islands progress from origin to right).
            // This will probably change later.
            float width;
            if (tm == null)
            {
                Debug.LogError("No Tilemap found for " + chunkPrefab.name + 
                                 ", defaulting to 10 unit offset.");
                width = 10f;
            }
            else
            {
                width = tm.localBounds.size.x;
            }
            _chunkOffset += width + 20; 
        }
        return _chunks;
    }

    private void ResetMap()
    {
        foreach (GameObject chunk in _chunks)
        {
            Destroy(chunk);
        }
        _chunkOffset = 0; 
        _chunks.Clear();
    }
}