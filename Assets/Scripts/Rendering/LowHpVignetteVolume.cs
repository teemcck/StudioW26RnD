using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Volume))]
public sealed class LowHpVignetteVolume : MonoBehaviour
{
    [SerializeField] private float activationThreshold = 0.25f;
    [SerializeField] private float lerpSpeed = 4f;

    private Volume _volume;
    private PlayerHealth _playerHealth;

    private void Awake()
    {
        _volume = GetComponent<Volume>();
    }

    private void Update()
    {
        if (_playerHealth == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerHealth = player.GetComponent<PlayerHealth>();
        }

        float target = 0f;
        if (_playerHealth != null)
        {
            float norm = _playerHealth.HealthNormalized;
            if (norm < activationThreshold)
                target = 1f - Mathf.Clamp01(norm / activationThreshold);
        }

        _volume.weight = Mathf.Lerp(_volume.weight, target, Mathf.Clamp01(Time.deltaTime * lerpSpeed));
    }
}
