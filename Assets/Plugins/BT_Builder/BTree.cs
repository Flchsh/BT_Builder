using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Reflection;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System;

/* 
 * This reads tree structure from xml and constructs trees. Any node variables set in editor are also read from xml.
 * Different trees will have different BTree instances. To use tree from actor, there must be a behavior component
 * in actor object. This has the tree as variable and a blackboard instance for data storing. 
 * 
 * NB! If tree constructing fails (not all classes are found), BTree component is disabled. Failed nodes are
 * listed in console log.
 * 
 * NB2 BTree also has to have access to visual debugger, which is only available in editor. All related code should be 
 * removed from the build version. Pay attention to preprocessor guards.
 */
namespace BTBuilder
{
    public struct debugNode
    {
        public BT_State returnState;
        public bool isOpen;             // TODO: see if this is redundant.
        public bool visited;
    }

    [RequireComponent(typeof(BT_Behave))]
    [AddComponentMenu("Behavior AI/Behavior Tree")]
    public class BTree : MonoBehaviour
    {

        // Tree structure data for setup
        public TextAsset file;
        public bool isDebugging; // Set from visual debugger. True if debugger window is open.
        TreeData BT_data;

        bool classNotFound = false;
        public bool activated = true;

        // Root node is the tree
        BaseNode root;
        public BaseNode Root() { return root; }
        // Node array is used to force-close interrupted actions.
        BaseNode[] treeArray;

        // Bool arrays to keep track of open nodes.
        public bool[] currentOpen;
        public bool[] lastOpen;
        int nodeCount;
        BT_State last;
        public bool erroredOut = false;

#if UNITY_EDITOR
        public debugNode[] debugData;
        public BT_DebugWindow debugger;
        int dbInd = 0;                  // Basically same as nodecount. Don't kill without checking.
#endif
        public BT_Behave agent;

        void Awake()
        {
            // Custom activation
            if (!activated)
                this.enabled = false;
            else if (!file)
            {
                Debug.LogError("No behavior file assigned in BTree\nGameobject: " + this.gameObject.name, file);
                this.enabled = false;
            }
            else if (!ReadTree())
            {
                Debug.LogError("Failed to read tree file " + file.name);
                this.enabled = false;
            }
            else { }
        }
        // Warning: Horribly complicated recursive initialisation function call hell ahead. Simplify!
        // Agent id is necessary in initialisation, since this sets up values for all different instances
        // of a variable, so that they can be accessed at once in update.
        public void InitialiseTreeVariables(BT_Behave manager)
        {
            agent = manager;
            InitTree(root, this);
        }
        void InitTree(BaseNode root, BTree tree)
        {
            root.Init(tree, agent);
            if (root.children.Count != 0)
            {
                foreach (BaseNode n in root.children)
                    InitTree(n, tree);
            }
        }

        public BT_State BT_Update()
        {
            if (this.enabled)
            {

#if UNITY_EDITOR
                resetDebugData();
#endif

                for (int i = 0; i < nodeCount; i++)
                    currentOpen[i] = false;
                BT_State state = root.ExecuteNode();

#if UNITY_EDITOR
                if (isDebugging)
                    debugger.updateData();
#endif

                if (state == BT_State.ERROR)
                {
                    Debug.LogError("Error occurred in behavior " + file.name);
                    erroredOut = true;
                }
                else if (last == BT_State.RUNNING)
                    ClosePrevious();
                else { }

                last = state;
                SetCurrentToLast_Openlist();
                return state;
            }
            return BT_State.ERROR;
        }

        void SetCurrentToLast_Openlist()
        {
            // bool arrays: Clearing current list is done at the start of the update. Why not all of this?
            currentOpen.CopyTo(lastOpen, 0);
        }
        // Accessors for info arrays
        public void IsOpened(int node_id, bool value)
        {
            currentOpen[node_id] = value;
        }
        // Close nodes that were left running on previous traversal but were not accessed on latest traversal.
        // ForceClose checks if node was already closed normally.
        void ClosePrevious()
        {
            for (int i = 0; i < nodeCount; ++i)
            {
                if (currentOpen[i] == false)
                {
                    if (lastOpen[i] == true)
                    {
                        treeArray[i].ForceClose();
                    }
                }
            }
        }

        // Should be local variable but has to be reached from another method.
        List<BaseNode> temp_tree = new List<BaseNode>();

        bool ReadTree()
        {
            expNode rootData = TreeData.LoadToTreeComp(file);
            root = ReadNode(rootData);

            if (root == null || classNotFound)
                return false;

            // Flatten the tree to an array:
            nodeCount = temp_tree.Count;
            treeArray = new BaseNode[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                treeArray[temp_tree[i].debugIndex] = temp_tree[i];
            }
            // Create corresponding open lists:
            currentOpen = new bool[nodeCount];
            lastOpen = new bool[nodeCount];

            temp_tree.Clear(); // Helper List should be deleted completely, actually. Dang c hashtag.

#if UNITY_EDITOR
            debugData = new debugNode[dbInd];
#endif

            return true;
        }
        // Read data from XML and create a new node according to the data. This is probably going to be very heavy
        // (lots of string comparisons, for one thing).
        // TODO: enumerize types?

        BaseNode ReadNode(expNode root)
        {
            // Create node of desired type according to class name
            Type t = Type.GetType(root.name);

            if (t == null)
            {
                Debug.LogError("Cannot find node class " + root.name);
                classNotFound = true;
                return null;
            }
            BaseNode b = (BaseNode)Activator.CreateInstance(t);

            // Find node variables
            PropertyInfo[] variables = t.GetProperties(BindingFlags.Public |
                                                       BindingFlags.NonPublic |
                                                       BindingFlags.Instance |
                                                       BindingFlags.FlattenHierarchy);
            // Set values stored in xml (needs to be converted)
            BT_Serializer bt = new BT_Serializer();
            foreach (exportedVariable n in root.vars)
            {
                for (int i = 0; i < variables.Length; ++i)
                {
                    if (n.name == variables[i].Name)
                    {
                        // Unity defined non-primitive types must be recognised and converted
                        // TODO: Better system for conversion?
                        if (n.type == "UnityEngine.Color" ||
                            n.type == "UnityEngine.Vector2" ||
                            n.type == "UnityEngine.Vector3" ||
                            n.type == "UnityEngine.Rect")
                        {
                            variables[i].SetValue(b, bt.ConvertToUnityType(n).value, null);
                        }
                        else variables[i].SetValue(b, n.value, null);
                    }
                }
            }
            temp_tree.Add(b);
#if UNITY_EDITOR
            b.debugIndex = dbInd;
            dbInd++;

#endif

            if (root.children.Count != 0)
            {
                b.children = new List<BaseNode>();
                foreach (expNode n in root.children)
                {
                    BaseNode c = ReadNode(n);
                    b.children.Add(c);
                }
            }
            return b;
        }
        public void SetTreeFile(TextAsset newFile)
        {
            file = newFile;
        }

        // debugger.
#if UNITY_EDITOR
        public void UpdateDebugNode(int index, BT_State state, bool leftOpen)
        {
            debugData[index].returnState = state;
            debugData[index].visited = true;
            debugData[index].isOpen = leftOpen;
        }
        public void OpenDebugNode(int index)
        {
            debugData[index].isOpen = true;
        }
        void resetDebugData()
        {
            for (int i = 0; i < debugData.Length; i++)
            {
                debugData[i].visited = false;
            }
        }
#endif

    }
}