using UnityEngine;

public class MapTesting : MonoBehaviour
{
    [SerializeField] private MapSpawner mapSpawner;

    private void Start()
    {
        if (mapSpawner == null)
        {
            Debug.LogError("MapSpawner reference is missing in MapTesting.");
            return;
        }
        mapSpawner.GenerateSequence();
    }
}