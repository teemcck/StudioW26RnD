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

        if (_chunks != null && _chunks.Count > 0)
        {
            StartCoroutine(PanThroughChunks());
        }
    }
    
    // Debug purposes until teleporters may trigger a pan.
    private IEnumerator PanThroughChunks()
    {
        int currentIndex = 0;

        while (true)
        {
            GameObject targetChunk = _chunks[currentIndex];
            
            cameraController.LockToTransform(targetChunk.transform);
            cameraController.Shake(shakeIntensity);

            yield return new WaitForSeconds(waitTime);

            currentIndex = (currentIndex + 1) % _chunks.Count;
        }
    }
}