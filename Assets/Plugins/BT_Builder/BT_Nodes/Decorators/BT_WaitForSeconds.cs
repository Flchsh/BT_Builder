using UnityEngine;
using System.Collections;
using BTBuilder;

/* TODO: Figure out how this should work.
 * If timer is not full, this fails. Should it be running instead?
 * NB! How to handle children that return running?
 * 
 * TODO: Rename! System has this.
 * 
 * */

public class WaitForSeconds : BaseNode {

    float seconds {get; set;}
    bool shootFirst { get; set; }
    float timer;                
    float elapsed;              // To keep time even if the node is not visited. TODO: Make optional?

    protected override void _init()
    {
        if (shootFirst)
            timer = seconds;
    }
    protected override void StartAction()
    {

        timer = Time.time - elapsed;    // This may kill the first timing.
    }
    protected override BT_State Action()
    {
        if (children.Count == 0)
            return BT_State.ERROR;		
        timer += Time.deltaTime;
        if (timer < seconds)
        {
            return BT_State.RUNNING;
        }
        else
        {
            return children[0].ExecuteNode();
        }
    }
    protected override void EndAction()
    {
        timer = 0;              // Timer starts counting immediately when child process has finished.
        elapsed = Time.time;
    }
}
