using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

namespace BTBuilder
{

    // Custom inspector view for the BT_Behave component.
    // Blackboard contents can be shown for testing and debugging (not editing).
    [CustomEditor(typeof(BT_Behave))]
    public class BT_BehaveEditor : Editor
    {
        // This is only necessary for showing colors as actual colors 
        // in addition to a series of numbers.
        public void OnEnable()
        {
            BT_Behave manager = (BT_Behave)target;
            manager.blackBoard.color_initer();
        }
        // Manager component inspector will show blackboard contents when expanded.
        public override void OnInspectorGUI()
        {
            BT_Behave manager = (BT_Behave)target;

            manager.blackBoard.DrawContents();

            Repaint();
        }
    }
}