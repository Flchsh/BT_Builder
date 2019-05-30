using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/* This is the behavior component to be added to gameobjects. Trees are references to
 * this object's tree components and updated whenever this component is active, 
 * according to their update settings. 
 * 
 * Gameobject's behavior data is kept on blackboard. The Blackboard class has typed 
 * dictionaries for common data types and also an object type dictionary for custom types.
 * 
 * Typed dictionaries cause less gargabe, which may be useful for mobile applications. 
 * */
namespace BTBuilder
{
    public class BT_Behave : MonoBehaviour
    {

        public BTree[] trees;

        public BT_Blackboard blackBoard = new BT_Blackboard();

        void Awake()
        {
            trees = this.gameObject.GetComponents<BTree>();
            if (trees.Length == 0)
                Debug.LogError("No behavior trees found in " + this.gameObject.name);

        }
        /* Note: This is problematic. Blackboard can be accessed fairly freely, which is a good thing,
         * but initialisation order is important. Tree init should always be called, so leaving that to
         * user is counterintuitive. Three-step initialisation is required:
         * 1. Create BT_Behave component
         * 2. Set custom updater vars to blackboard (initial values etc.)
         * 3. Call tree variable init
         * 
         * Solution: Create abstract Updater class (or interface!), which contains all necessary functionality.
         */
        void Start()
        {
            if (trees.Length == 0)
                Debug.LogError("No behavior trees found for initialisation in " + this.gameObject.name);
            foreach (BTree t in trees)
            {
                if (t.enabled)
                {
                    t.InitialiseTreeVariables(this);
                }
            }
        }
        public bool show;

        // TODO: Better handling of individual tree execution. Not every tree should be checked on every frame.

        // Note: User should have full control of when to call different trees. Reactionary logic should be in Update;
        // event-specific decision logic should be accessed independently for each tree; non-critical logic run at 
        // intervals or divided over several frames...
        void Update()
        {
            foreach (BTree t in trees)
            {
                if (!t.erroredOut && t.activated)
                    t.BT_Update();
            }
        }
        BT_State RunTree(string treename)
        {
            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i].file.name == treename) // slow.
                    return trees[i].BT_Update();
            }
            Debug.LogError("Behavior tree " + treename + " not found");
            return BT_State.ERROR;
        }
    }
}
