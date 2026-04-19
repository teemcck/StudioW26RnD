using UnityEngine;

public class SplittingEnemy : MeleeEnemy
{
    [Header("Split on death")]
    [SerializeField] private GameObject splitEnemyPrefab;
    [SerializeField] private int splitCount = 2;
    [SerializeField] private float splitHealthMultiplier = 0.45f;
    [SerializeField] private float splitSizeMultiplier = 0.7f;
    [SerializeField] private float splitSpawnRadius = 0.35f;
    [SerializeField] private int maxSplitDepth = 2;

    private int _splitDepth;

    public void SetSplitDepth(int depth) => _splitDepth = depth;

    protected override void Die(DamageContext context = default)
    {
        if (_splitDepth < maxSplitDepth && splitEnemyPrefab)
        {
            float step = 360f / Mathf.Max(1, splitCount);
            for (int i = 0; i < splitCount; i++)
            {
                float rad = step * i * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * splitSpawnRadius;
                var go = Instantiate(splitEnemyPrefab, (Vector2)transform.position + offset, Quaternion.identity);
                if (go.TryGetComponent<EnemyBase>(out var eb))
                    eb.ApplyRuntimeScaling(splitHealthMultiplier, splitSizeMultiplier);
                if (go.TryGetComponent<SplittingEnemy>(out var childSplit))
                    childSplit.SetSplitDepth(_splitDepth + 1);
            }
        }

        base.Die(context);
    }
}
