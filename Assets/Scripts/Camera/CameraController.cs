using UnityEngine;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Cinemachine References")]
    [SerializeField] private CinemachineCamera cineCamera;
    [SerializeField] private CinemachineImpulseSource impulseSource;

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
}