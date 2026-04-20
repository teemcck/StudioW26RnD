public sealed class SlimeKillGroup
{
    private int _alive = 1;

    public void NotifyReplacedBySplits(int childCount)
    {
        _alive += childCount - 1;
    }

    public void NotifyLeafDied(EnemyBase instigator, DamageContext context)
    {
        if (_alive <= 0)
            return;

        _alive--;
        if (_alive != 0)
            return;

        EventBus<EnemyKilledEvent>.Raise(new EnemyKilledEvent
        {
            Enemy = instigator,
            EnemyType = instigator.GetType().Name,
            Position = instigator.transform.position,
            Context = context
        });
    }
}
