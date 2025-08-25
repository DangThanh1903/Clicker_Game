using UnityEngine;

public class StatsManager : StatsManagerBase
{
    public static StatsManager Ins { get; private set; }

    protected override void Awake()
    {
        if (Ins == null)
        {
            Ins = this;
            base.Awake();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
