using UnityEngine;

public class ChunkGen : MonoBehavior
{
    [Header("List of all possible map \"chunks\" to select from")]
    [SerializeField] private list<GameObject> chunkPool; 
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
    }

    private void Start() {
        chunkLen = chunkPool.Count;
    }

    public GameObject GetRandomMapChunk()
    {
        int random = Random.range(0, chunkLen);
        return chunks[random];
    }
}