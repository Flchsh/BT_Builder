using UnityEngine;
using System.Collections;
using BTBuilder;

public class DebugWriter : BaseNode {

    string line { get; set; }

    protected override BT_State Action()
    {
        if (line == null)
            return BT_State.ERROR;
        Debug.Log(line);
        return BT_State.SUCCESS;
    }
}
