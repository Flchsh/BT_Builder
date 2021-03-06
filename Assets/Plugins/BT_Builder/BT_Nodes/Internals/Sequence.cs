﻿using UnityEngine;
using System.Collections;
using BTBuilder;

public class Sequence : BaseNode {

    protected override BT_State Action()
    {
        if (children.Count == 0)
            return BT_State.ERROR;		
        for (int i = 0; i < children.Count; ++i)
        {
            temp_state = children[i].ExecuteNode();

            if (temp_state != BT_State.SUCCESS)
            {
                return temp_state;
            }
        }
        return temp_state;
    }
}
