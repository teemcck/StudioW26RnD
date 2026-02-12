using System.Runtime.CompilerServices;
using UnityEngine;

[RequiredAttribute(MapGen)]
public class MapSpawner : MonoBehavior
{
    [SerializeField] private Range numChunks;
    private List<GameObject> chunks;
    private ChunkGen chunkGen; 

    private void Awake()
    {
        chunkGen = GetComponent<ChunkGen>();
    }

    public void GenerateSequence()
    {
        int numChunks = Random.Range(numChunks);
        for (int i = 0; i < numChunks; ++i)
        {
            chunks = Instantiate(chunkGen.GetRandomMapChunk());
        }
    }
}