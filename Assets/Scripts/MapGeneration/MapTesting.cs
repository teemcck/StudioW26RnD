using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapTesting : MonoBehaviour
{
    [SerializeField] private MapSpawner mapSpawner;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private float waitTime = 2f;
    [SerializeField] private float shakeIntensity = 0.5f;

    private List<GameObject> _chunks;

    private void Start()
    {
        if (mapSpawner == null || cameraController == null) return;

        _chunks = mapSpawner.GenerateSequence();
    }
}