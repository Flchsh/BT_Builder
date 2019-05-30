using UnityEngine;
using System.Collections;
using BTBuilder;

public class RandomCond : BaseNode {
    // Probability condition check. Randomized success, rate based on given percentage.

    float successPercentage { get; set; }
    protected override void _init()
    {
        base._init();
    }
    protected override BT_State Action()
    {
        if (Random.Range(0f, 10000f) / 100 <= successPercentage)
            return BT_State.SUCCESS;
        return BT_State.FAILURE;
    }
}
