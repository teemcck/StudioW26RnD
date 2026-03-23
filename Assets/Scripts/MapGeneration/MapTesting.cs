using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapTesting : MonoBehaviour
{
    [SerializeField] private MapSpawner mapSpawner;

    private List<GameObject> _chunks;

    private void Start()
    {
        if (mapSpawner == null)
        {
            Debug.Log("MapSpawner is missing.");
            return;
        }

        _chunks = mapSpawner.GenerateSequence();
    }
}