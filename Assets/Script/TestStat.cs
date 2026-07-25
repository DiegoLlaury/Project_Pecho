using UnityEngine;

public class TestStat : MonoBehaviour
{
    public StatType baseStats;

    private RuntimeStats stats;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        stats = new RuntimeStats(baseStats);

        stats.SetStat("level", 5);
    }

}
