using UnityEngine;
using System.Collections;
using BTBuilder;

[System.Serializable]
public class RandomSelector : BaseNode {

    // TODO: Design a better way to get random numbers.
    
    protected override BT_State Action()
    {
        if (children.Count == 0)
            return BT_State.ERROR;		
        int curr_child = Random.Range(0, 100) % children.Count;
        temp_state = children[curr_child].ExecuteNode();
        return temp_state;
    }
}
