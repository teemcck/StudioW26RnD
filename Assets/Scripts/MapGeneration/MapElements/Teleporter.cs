using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

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

    private bool _transitioning;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_transitioning) return;

        if (destination != null)
        {
            _transitioning = true;
            StartCoroutine(PerformChunkTransition(other.transform, destination.position));
        }
        else
        {
            AudioManager.Instance?.PlayTeleporterEntered();
            EventBus<PlayerReachedEndpointEvent>.Raise(new PlayerReachedEndpointEvent());
        }
    }

    private IEnumerator PerformChunkTransition(Transform player, Vector3 targetPos)
    {
        AudioManager.Instance?.PlayChunkTransition();

        var overlay = ChunkTransitionOverlay.Instance;
        if (overlay != null)
        {
            yield return overlay.Play(() => MovePlayerWithCameraSnap(player, targetPos));
        }
        else
        {
            MovePlayerWithCameraSnap(player, targetPos);
        }

        _transitioning = false;
    }

    private static void MovePlayerWithCameraSnap(Transform player, Vector3 targetPos)
    {
        var cam = Object.FindFirstObjectByType<CinemachineCamera>();
        CinemachineFollow follow = cam != null ? cam.GetComponent<CinemachineFollow>() : null;
        Vector3 savedDamping = Vector3.zero;
        bool hadDamping = follow != null;
        if (follow != null)
        {
            savedDamping = follow.TrackerSettings.PositionDamping;
            var s = follow.TrackerSettings;
            s.PositionDamping = Vector3.zero;
            follow.TrackerSettings = s;
        }

        player.position = targetPos;

        if (hadDamping)
        {
            var runner = cam.gameObject.AddComponent<RestoreDampingNextFrame>();
            runner.Init(follow, savedDamping);
        }
    }
}

internal sealed class RestoreDampingNextFrame : MonoBehaviour
{
    private CinemachineFollow _follow;
    private Vector3 _saved;

    public void Init(CinemachineFollow follow, Vector3 saved)
    {
        _follow = follow;
        _saved = saved;
    }

    private IEnumerator Start()
    {
        yield return null;
        if (_follow != null)
        {
            var s = _follow.TrackerSettings;
            s.PositionDamping = _saved;
            _follow.TrackerSettings = s;
        }
        Destroy(this);
    }
}
