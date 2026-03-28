using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class MapSpawner : MonoBehaviour
{
    [Header("Map Generation Settings")]
    [SerializeField] private int minNumChunks = 3;
    [SerializeField] private int maxNumChunks = 7;
    [SerializeField] private float chunkSpacing = 5f;

    [Header("References")]
    [SerializeField] private ChunkGen chunkGen;
    [SerializeField] private Transform chunkContainer;

    private List<GameObject> _chunks = new List<GameObject>();
    private float _chunkOffset;

    public int MinNumChunks => minNumChunks;
    public int MaxNumChunks => maxNumChunks;

    /// <summary>
    /// Referenced by GameplayHandler.cs.
    /// Used to place player object at the beginning of the level.
    /// </summary>
    public Vector2 SpawnPosition {get; private set;}

    private void Awake()
    {
        if (chunkGen == null)
            Debug.LogError("ChunkGen reference is missing in MapSpawner.");
        if (chunkContainer == null)
            Debug.LogError("ChunkContainer reference is missing in MapSpawner.");
    }

    /// <param name="difficulty">Reserved for future use (scaling chunk count, etc.)</param>
    public List<GameObject> GenerateSequence(int difficulty = 1)
    {
        ResetMap();
        int numChunks = Random.Range(minNumChunks, maxNumChunks + 1);
        Debug.Log($"Generating {numChunks} chunks at difficulty {difficulty}.");

        for (int i = 0; i < numChunks; i++)
        {
            GameObject prefab = chunkGen.GetRandomMapChunk();
            if (prefab == null)
            {
                Debug.LogError($"Chunk {i} prefab is null, skipping.");
                continue;
            }

            GameObject chunk = Instantiate(prefab, chunkContainer);
            chunk.name = prefab.name;
            chunk.transform.position = new Vector3(_chunkOffset, 0, 0);
            _chunks.Add(chunk);

            Tilemap tm = chunk.GetComponentInChildren<Tilemap>();
            float width = tm != null ? tm.localBounds.size.x : 10f;
            if (tm == null)
                Debug.LogWarning($"No Tilemap on {prefab.name}, defaulting to 10 unit width.");

            _chunkOffset += width + chunkSpacing;
        }

        // Initial spawn position determined by TeleportEntry of initial chunk.
        SpawnPosition = _chunks[0].GetComponentInChildren<TeleportEntry>().transform.position;

        LinkTeleporters();
        return _chunks;
    }

    private void LinkTeleporters()
    {
        int count = _chunks.Count;

        // Link each teleporter to the entry point of the next chunk.
        // The last chunk's teleporter is intentionally left with destination = null.
        // Teleporter.cs checks for this and raises PlayerReachedEndpointEvent instead.
        for (int i = 0; i < count - 1; i++)
        {
            Teleporter tp = _chunks[i].GetComponentInChildren<Teleporter>();
            TeleportEntry entry = _chunks[i + 1].GetComponentInChildren<TeleportEntry>();

            if (tp == null)
            {
                Debug.LogWarning($"No Teleporter on chunk {i} ({_chunks[i].name}).");
                continue;
            }
            if (entry == null)
            {
                Debug.LogWarning($"No TeleportEntry on chunk {i + 1} ({_chunks[i + 1].name}).");
                continue;
            }

            tp.destination = entry.transform;
        }
    }

    private void ResetMap()
    {
        foreach (GameObject chunk in _chunks)
            Destroy(chunk);

        _chunks.Clear();
        _chunkOffset = 0f;
    }
}