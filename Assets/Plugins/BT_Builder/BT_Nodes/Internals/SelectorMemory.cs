using UnityEngine;
using System.Collections;
using BTBuilder;

// Memory selector remembers last successful child node and starts execution from that child.
// NB: Should all children still be checked or only the ones after the said child?
// 
public class SelectorMemory : BaseNode {
    
    private int lastRunningChild;
    protected override void _init()
    {
        base._init();        
    }
    protected override BT_State Action()
    {
        if (children.Count == 0)
            return BT_State.ERROR;		
        if (lastRunningChild >= children.Count)
            lastRunningChild = 0;

        for (int i = lastRunningChild; i < children.Count; ++i)
        {
            temp_state = children[i].ExecuteNode();

            if (temp_state != BT_State.FAILURE)
            {
                if (temp_state != BT_State.ERROR)
                    lastRunningChild = i;
                return temp_state;
            }
        }
        return temp_state;
    }
}
