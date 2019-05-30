using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace BTBuilder
{ 
public enum BT_State
{
    SUCCESS,
    FAILURE,
    RUNNING,
    ERROR
};
    public abstract class BaseNode
    {
        public string[] varNames;
        public List<BaseNode> children = new List<BaseNode>();
        public int debugIndex;
        protected BTree treeId;
        protected BT_Behave agent;
        protected BT_State temp_state;

        public bool isOpen = false;

        // placeholder, for now
        // has two functions because blackboard instance has to be set first (base gets called after derived method).
        // In theory this could be done by calling the base first in derived class, but who remembers that? Every time?
        // We need a safer system.
        public void Init(BTree owner, BT_Behave manager)
        {
            agent = manager;
            treeId = owner;
            _init();
        }
        protected virtual void _init() { }

        // Wrapper for node execution. This is the only method that needs to be called outside
        // the node itself.
        // TODO: current state should be stored in blackboard?
        public BT_State ExecuteNode()
        {
            Open();
            temp_state = Action();
            if (temp_state == BT_State.ERROR)
            {
                Debug.LogError("Error occurred in node " + this);
            }
            if (temp_state != BT_State.RUNNING)
                Close();
            return temp_state;
        }
        // If node needs to be closed by tree (another branch has interrupted a running action).
        // Sets last known state to failure, if it would otherwise be still running
        // (EndAction is normally the place to set this).
        public void ForceClose()
        {
            if (isOpen)
            {
                isOpen = false;
                EndAction();
                if (temp_state == BT_State.RUNNING)
                    temp_state = BT_State.FAILURE;
            }
#if UNITY_EDITOR
            // Update final state for debugger
            //if (treeId.isDebugging)			// Also if not debugging. Error has to be remembered.
            treeId.UpdateDebugNode(debugIndex, temp_state, isOpen);
#endif

        }
        // If last open list contains node, BT_State = RUNNING. Add node to current open list.
        // NB! All data (nodestate, open list) should be in data storage, not node.
        void Open()
        {
            isOpen = true;
            if (temp_state != BT_State.RUNNING)
            {
                StartAction();
            }
            else
            {
                // Debug.Log("running: " + instance);
            }

#if UNITY_EDITOR
            // Set debugging to running as default for every node that is opened
            if (treeId.isDebugging)
                treeId.UpdateDebugNode(debugIndex, BT_State.RUNNING, isOpen);
#endif

            treeId.IsOpened(debugIndex, true);
        }

        // Remove node from list.
        void Close()
        {
            if (isOpen)
            {
                isOpen = false;
                EndAction();
            }
            treeId.IsOpened(debugIndex, true);

#if UNITY_EDITOR
            // Update final state for debugger
            //if (treeId.isDebugging)
            treeId.UpdateDebugNode(debugIndex, temp_state, isOpen);
#endif
        }
        // Logic needed in the beginning of the action. Mostly relevant with actions that take more than one
        // traversal to execute. Made virtual and left empty so that its calling logic can be generalized,
        // even if no start logic is implemented. (Is this good?)
        protected virtual void StartAction()
        {

        }
        // Same goes for ending action. This is also called if tree is forcing a branch to close.
        // Should there be a separate reset function too?
        protected virtual void EndAction()
        {

        }
        // The actual implementation of the action goes here. 
        protected abstract BT_State Action();
    }
}
