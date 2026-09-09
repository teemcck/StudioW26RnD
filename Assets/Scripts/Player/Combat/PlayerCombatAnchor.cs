using UnityEngine;

public class PlayerCombatAnchor : MonoBehaviour
{
    [Header("Body (feet = root pivot)")]
    [SerializeField] private Vector2 hitAnchorLocalFromFeet = new Vector2(0f, 0.28f);
    [SerializeField] private Collider2D combatHitVolume;

    public Vector2 WorldHitAnchor
    {
        get
        {
            Vector3 w = transform.TransformPoint(new Vector3(hitAnchorLocalFromFeet.x, hitAnchorLocalFromFeet.y, 0f));
            return new Vector2(w.x, w.y);
        }
    }

    public Vector2 ClosestCombatPoint(Vector2 fromWorld)
    {
        if (combatHitVolume != null && combatHitVolume.enabled)
            return combatHitVolume.ClosestPoint(fromWorld);
        return WorldHitAnchor;
    }

    private void LateUpdate() => SyncHitVolume();

    private void OnValidate() => SyncHitVolume();

    private void SyncHitVolume()
    {
        if (combatHitVolume == null || combatHitVolume.transform.parent != transform)
            return;
        Vector3 p = new Vector3(hitAnchorLocalFromFeet.x, hitAnchorLocalFromFeet.y, 0f);
        if (combatHitVolume.transform.localPosition != p)
            combatHitVolume.transform.localPosition = p;
    }
}
