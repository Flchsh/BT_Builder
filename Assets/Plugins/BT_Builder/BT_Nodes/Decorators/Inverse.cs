using UnityEngine;
using System.Collections;
using BTBuilder;

public class Inverse : BaseNode {

    protected override BT_State Action()
    {
        if (children.Count == 0)
            return BT_State.ERROR;		
        temp_state = children[0].ExecuteNode();

        if (temp_state == BT_State.FAILURE)
            return BT_State.SUCCESS;

        else if (temp_state == BT_State.SUCCESS)
            return BT_State.FAILURE;

        else return temp_state;
    }
}
