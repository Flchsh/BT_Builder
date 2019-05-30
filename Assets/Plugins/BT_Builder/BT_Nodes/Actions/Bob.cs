using UnityEngine;
using System.Collections;
using BTBuilder;

// :D
public class Bob : BaseNode {

    Vector3 temp = new Vector3(0,0,0);
    Vector2 tester { get; set; }
    float igi = 8;
    protected override BT_State Action()
    {
        igi -= igi/3 * Time.deltaTime;
        agent.blackBoard.SetFloat(igi, "igi");
        temp = agent.blackBoard.Get<Vector3>("newpos");
        temp.y = Mathf.Abs(Mathf.Sin(Time.timeSinceLevelLoad / igi) * igi);
        agent.blackBoard.Set<Vector3>(temp, "newpos");
        return BT_State.RUNNING;
    }
}
