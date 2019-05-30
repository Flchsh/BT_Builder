using UnityEngine;
using System.Collections;
using BTBuilder;

public class RepeatTimes : BaseNode {

    int times { get; set; }

    protected override BT_State Action()
    {   
        if (children.Count == 0)
            return BT_State.ERROR;	
        for (int i = 0; i < times; ++i)
        {
            temp_state = children[0].ExecuteNode();
            if (temp_state == BT_State.ERROR)
                return temp_state;
        }
        return temp_state;
    }

}
