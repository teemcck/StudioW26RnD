using UnityEngine;

public class Teleporter : MonoBehaviour
{
    [HideInInspector] public Transform destination;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        other.transform.position = destination.position;
    }
}