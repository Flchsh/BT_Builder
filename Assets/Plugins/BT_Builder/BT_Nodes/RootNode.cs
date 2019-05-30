using UnityEngine;
using System.Collections;
using BTBuilder;

public class RootNode : BaseNode
{
    protected override BT_State Action()
    {
        return children[0].ExecuteNode();
    }
}
