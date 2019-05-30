using UnityEngine;
using System.Collections;
using BTBuilder;

public class Parallel : BaseNode {

    int MaxFails { get; set; }

    protected override BT_State Action()
    {   
	    if (children.Count == 0)
			return BT_State.ERROR;
        int failures = 0;
        BT_State childState;
        temp_state = BT_State.SUCCESS;
        for (int i = 0; i < children.Count; ++i)
        {
            childState = children[i].ExecuteNode();
            // Parallel node is running if at least one of its children is running
            if (childState == BT_State.RUNNING)
                temp_state = childState;
            // Error is returned immediately, since correct execution of other nodes cannot be guaranteed.
            if (childState == BT_State.ERROR)
                return childState;
            else if (childState == BT_State.FAILURE)
                failures++;
        }        
        if (failures > MaxFails)
            return BT_State.FAILURE;
        return temp_state;
    }
}
