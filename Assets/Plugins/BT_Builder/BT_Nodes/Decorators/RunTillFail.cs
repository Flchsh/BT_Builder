using UnityEngine;
using System.Collections;
using BTBuilder;

public class RunTillFail : BaseNode {

    protected override BT_State Action()
    {
        if (children.Count == 0)
            return BT_State.ERROR;		
        temp_state = children[0].ExecuteNode();
        if (temp_state == BT_State.ERROR || temp_state == BT_State.FAILURE)
            return temp_state;
        else return BT_State.RUNNING;
    }
}
