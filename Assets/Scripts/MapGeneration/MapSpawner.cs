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
        Debug.Log("Generating " + numChunks + " chunks");

        for (int i = 0; i < numChunks; ++i)
        {
            GameObject chunkPrefab = chunkGen.GetRandomMapChunk();
            GameObject chunk = Instantiate(chunkPrefab, chunkContainer);
            Debug.Log("Island " + i + ": " + chunk.name);

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

            _chunkOffset += width + 5;
        }

        LinkTeleporters();
        return _chunks;
    }

    private void SpawnEnemies()
    {
        // Placeholder for now.
        // List<(GameObject, float)> enemyPool
        // {
        //     ()
        // };


        int numChunks = _chunks.Count;
        for (int i = 0; i < numChunks; ++i)
        {
            // _chunks[i].GetComponent<SpawnEnemies>()
            //     .SpawnEnemiesOnChunk();
        }
    }
    
    private void LinkTeleporters()
    {
        int numChunks = _chunks.Count;
        for (int i = 0; i < numChunks - 1; ++i)
        {
            Teleporter tp = _chunks[i].GetComponentInChildren<Teleporter>();
            Transform entry = _chunks[i + 1].GetComponentInChildren<TeleportEntry>().transform;
            tp.destination = entry;
        }
        // Remove last teleporter. This needs to be a win condition eventually.
        _chunks[numChunks - 1].GetComponentInChildren<Teleporter>().gameObject.SetActive(false);
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