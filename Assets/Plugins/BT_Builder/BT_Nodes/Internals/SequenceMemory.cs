using UnityEngine;
using System.Collections;
using BTBuilder;

public class SequenceMemory : BaseNode {

    private int lastRunningChild;

    protected override void StartAction()
    {
        temp_state = BT_State.RUNNING;
    }
    protected override void EndAction()
    {
        lastRunningChild = 0;
    }

    // NB! This checks only one child at each traversal.
    protected override BT_State Action()
    {
		if (children.Count == 0)
            return BT_State.ERROR;
        if (lastRunningChild >= children.Count)
            temp_state = BT_State.SUCCESS;
        else
        {
            BT_State childState = children[lastRunningChild].ExecuteNode();
            if (childState == BT_State.SUCCESS)
                ++lastRunningChild;
            if (childState == BT_State.FAILURE || childState == BT_State.ERROR)
                temp_state = childState;            
        }
        return temp_state;
    }
}
