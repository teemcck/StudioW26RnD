using System;
using UnityEngine;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Cinemachine References")] [SerializeField]
    private CinemachineCamera cineCamera;

    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private Transform playerTransform;
    private CinemachineConfiner2D _confiner;

    private void Awake()
    {
        if (cineCamera == null)
        {
            Debug.Log("Cinemachine Cinecamera is unassigned.");
        }

        if (playerTransform == null)
        {
            Debug.Log("Player is unassigned.");
        }

        // Fetch confiner.
        if (!TryGetComponent<CinemachineConfiner2D>(out _confiner))
        {
            Debug.Log("Cinemachine Confiner could not be found. " +
                      "This is 100% intentional and will be fixed later at some point.");
        }
    }

    /// <summary>
    /// Tells the Cine Camera to start following a new target.
    /// Cinemachine handles the smoothing automatically!
    /// </summary>
    public void LockToTransform(Transform target)
    {
        if (cineCamera != null)
        {
            cineCamera.Follow = target;
        }
    }

    /// <summary>
    /// Tells the CineCamera to start following the player.
    /// </summary>
    public void LockToPlayer()
    {
        if (cineCamera != null && playerTransform != null)
        {
            cineCamera.Follow = playerTransform;
        }
    }

    /// <summary>
    /// Triggers a screen shake with a specific intensity.
    /// </summary>
    /// <param name="intensity">How violent the shake is (try 0.1 to 1.0)</param>
    public void Shake(float intensity = 0.2f)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(intensity);
        }
    }

    /// <summary>
    /// Update Cinemachine Camera Confiner collider.
    /// This is the area the Cinemachine camera is restricted to.
    /// </summary>
    public void UpdateConfinerCollider(Collider2D collider)
    {
        if (_confiner != null)
        {
            _confiner.BoundingShape2D = collider; // Update collider.
            _confiner.InvalidateBoundingShapeCache(); // Rebuild collider.
        }
        else
        {
            Debug.Log("Confiner is unassigned.");
        }
    }
}