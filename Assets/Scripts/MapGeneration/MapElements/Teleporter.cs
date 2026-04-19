using UnityEngine;

/// <summary>
/// Placed on every chunk.
/// - If destination is set: teleports the player to the next chunk's TeleportEntry.
/// - If destination is null (last chunk): raises PlayerReachedEndpointEvent to end the level.
/// MapSpawner.LinkTeleporters() sets destinations for all but the last chunk,
/// leaving the last one null so it acts as the level endpoint.
/// </summary>
public class Teleporter : MonoBehaviour
{
    [HideInInspector] public Transform destination;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Build on this logic later, null destination is not the best idea for
        // denoting the end of a level but good enough for now.
        if (destination != null)
        {
            AudioManager.Instance?.PlayTeleporterEntered();
            other.transform.position = destination.position;
        }
        else
        {
            AudioManager.Instance?.PlayTeleporterEntered();
            EventBus<PlayerReachedEndpointEvent>.Raise(new PlayerReachedEndpointEvent());
        }
    }
}
