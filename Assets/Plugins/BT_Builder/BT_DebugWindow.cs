#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
/* 
 * Almost like the editor but not at all like that.
 * 
 * TODO: Add functionality for showing subtree only / LOD settings etc.
 * Show branch by clicking a node? + button for getting back to full view.
 * Manual LOD choice in both inspector and window.
 * 
 * Full tree data is saved in BTree(Editor) and can be retrieved from there.
 * 
 * */
namespace BTBuilder
{
    public class BT_DebugWindow : EditorWindow
    {

        public class dbNode
        {
            public string name;
            public Rect place;
            public debugNode currentData;
            public int index;               // This should be the index of currentData in updated debugNode array.
            public List<dbNode> children;   // Only needed to define/update positions.
            public int level;
            public float spaceneeded;       // For layout reasons.
        }
        // debugged tree, flattened. Corresponding array in tree component.
        dbNode[] nodes;

        // Size of drawn node
        const float nodeWidth = 80;
        const float nodeHeight = 50;
        const float nodeSpaceY = 40;
        const float nodeSpaceX = 10;

        // Rectangles for node window elements
        Rect markPos = new Rect((nodeWidth / 2) - 13, 18, 26, 26);

        // Connection line helpers
        Vector2 verticalOffset = new Vector2(0, (nodeHeight + nodeSpaceY) / 2);

        // window scrolling
        Vector2 scrollPosition;
        float maxNodeX, maxNodeY, contractedMaxNodeX;
        float defaultWidth = 1200;
        float defaultHeight = 800;

        // Graphics. Hardcoded frame coordinates, thank you. Order is yes, no, run, error, not visited.
        Texture2D marks;
        Rect[] markFrame =
        {
        new Rect(   0, 0, 0.2f, 1f),
        new Rect(0.2f, 0, 0.2f, 1f),
        new Rect(0.4f, 0, 0.2f, 1f),
        new Rect(0.6f, 0, 0.2f, 1f),
        new Rect(0.8f, 0, 0.2f, 1f)
    };

        // Currently debugged tree.
        BTree currentTree;
        string treename;

        // editor color vars. Set from init to match current skin
        Color controlAreaBackground;
        Color controlAreaSeparator;

        GUISkin editorSkin;
        GUIStyle nodeStyle;
        float labelHeight = 20;

        // Window initialisation; called from BTree component when debugger is opened.
        // Subtree LOD changes are not (yet) implemented.
        public void InitDebugView(BTree tree, bool hideSubTrees, Rect _position)
        {
            currentTree = tree;
            treename = currentTree.name;
            tree.isDebugging = true;
            GetData(tree.Root(), 0);
            PlaceNodes();
            this.position = SetWindowToTreeSize(_position); // Saved position (but not window size!) while game is running. Fairly useless at the moment.
            autoRepaintOnSceneChange = true;
            editorSkin = (GUISkin)EditorGUIUtility.Load("BT_Builder/BT_Editor.guiskin");
            if (EditorGUIUtility.isProSkin)
            {
                marks = (Texture2D)EditorGUIUtility.Load("BT_Builder/debugMarks.png");
                controlAreaBackground = new Color(0.24f, 0.24f, 0.24f, 1);
                controlAreaSeparator = new Color(0.1f, 0.1f, 0.1f, 1);
            }
            else
            {
                marks = (Texture2D)EditorGUIUtility.Load("BT_Builder/debugMarksLight.png");
                controlAreaBackground = new Color(0.85f, 0.85f, 0.85f, 1);
                controlAreaSeparator = new Color(0.55f, 0.55f, 0.55f, 1);
                nodeStyle = editorSkin.FindStyle("light_debug");
            }
        }
        // Sets window size to show the whole tree, unless its size exceeds the default max size.
        // Window can be manually resized afterwards, if necessary.
        Rect SetWindowToTreeSize(Rect currentSize)
        {
            Rect temp = currentSize;
            if (maxNodeX < defaultWidth)
                temp.width = maxNodeX;
            else temp.width = defaultWidth;
            if (maxNodeY < defaultHeight)
                temp.height = maxNodeY;
            else temp.height = defaultHeight;

            return temp;
        }
        public void OnGUI()
        {
            // Pro node style is default window style. Only works in OnGUI.
            if (nodeStyle == null)
                nodeStyle = GUI.skin.window;
            // slightly tinted background. Also darker line to separate control area from tree window.
            EditorGUI.DrawRect(new Rect(0, 0, position.width, labelHeight), controlAreaBackground);
            EditorGUI.DrawRect(new Rect(0, labelHeight - 1, position.width, 1), controlAreaSeparator);
            EditorGUI.LabelField(new Rect(0, 0, position.width, labelHeight), treename);

            scrollPosition = GUI.BeginScrollView(new Rect(0,
                                                          labelHeight + 10,
                                                          position.width,
                                                          position.height - labelHeight - 10),
                                                 scrollPosition,
                                                 new Rect(0, 0, maxNodeX, maxNodeY - labelHeight - 10));
            Handles.BeginGUI();
            DrawConnections();
            Handles.EndGUI();

            BeginWindows();
            foreach (dbNode d in nodes)
            {
                d.place = GUI.Window(d.index, d.place, WindowFunction, d.name, nodeStyle);
            }
            EndWindows();
            GUI.EndScrollView();

        }
        // Close debugger when exiting playmode or when owner is destroyed (like when scene is reloaded).
        public void Update()
        {
            if (!Application.isPlaying || currentTree == null)
            {
                Close();
            }
        }
        public void OnDestroy()
        {
            if (currentTree != null)
            {
                currentTree.isDebugging = false;
                currentTree.debugger = null;
            }
        }

        /// <summary>
        /// Helper functions for window drawing
        /// </summary>
        void DrawConnections()
        {
            foreach (dbNode n in nodes)
            {
                if (n.children != null)
                {
                    Handles.DrawLine(n.place.center,
                                     n.place.center + verticalOffset);
                    foreach (dbNode child in n.children)
                    {
                        Handles.DrawLine(child.place.center,
                                         child.place.center - verticalOffset);
                    }
                    int count = n.children.Count;
                    if (count > 1)
                        Handles.DrawLine(n.children[0].place.center - verticalOffset,
                                         n.children[count - 1].place.center - verticalOffset);
                }
            }
        }
        // Node windows with updated state symbols.
        void WindowFunction(int nodeIndex)
        {
            dbNode n = nodes[nodeIndex];

            if (!n.currentData.visited)
            {
                GUI.DrawTextureWithTexCoords(markPos, marks, markFrame[4]);
            }
            else
            {
                // use frame index to define current state mark in batch
                GUI.DrawTextureWithTexCoords(markPos, marks, getImage(n.currentData.returnState));
            }
        }
        /// <summary>
        /// Update node graphics and data
        /// </summary>

        // call from btree to keep it synced with tree traversal.
        // Copies debugData at index to currentData of the node at index.
        public void updateData()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].currentData.visited = currentTree.debugData[i].visited;
                nodes[i].currentData.isOpen = currentTree.debugData[i].isOpen;
                nodes[i].currentData.returnState = currentTree.debugData[i].returnState;
            }
        }

        Rect getImage(BT_State state)
        {
            switch (state)
            {
                case (BT_State.SUCCESS):
                    {
                        return markFrame[0];
                    }
                case (BT_State.FAILURE):
                    {
                        return markFrame[1];
                    }
                case (BT_State.ERROR):
                    {
                        return markFrame[3];
                    }
                case (BT_State.RUNNING):
                    {
                        return markFrame[2];
                    }
                default:
                    // alarm mark. This should not happen.
                    return markFrame[3];
            }
        }


        /// <summary>
        /// Helper functions for redefining window contents when branch focus is changed.
        /// NB: These should only be called at init or LOD change!
        /// TODO: Arrange everything in array rather than list for cheaper index access. This happens while running the game, after all.
        /// Cost is about double for List (it checks range).
        /// </summary>
        /// 

        // Helper vars to create the debugged node array. 
        int maxlevel = 0;
        List<dbNode> temp_nodes = new List<dbNode>();

        // Fill in debug node data.
        private dbNode GetData(BaseNode bnode, int lvl)
        {
            dbNode temp = new dbNode();
            temp.name = bnode.GetType().ToString();
            temp.level = lvl;
            temp.index = bnode.debugIndex;
            if (bnode.children.Count > 0)
            {
                int nextlvl = lvl + 1;
                if (nextlvl > maxlevel)
                    maxlevel = nextlvl;
                temp.children = new List<dbNode>();
                foreach (BaseNode b in bnode.children)
                    temp.children.Add(GetData(b, nextlvl));
            }
            temp_nodes.Add(temp);
            return temp;
        }

        // Create the updated array. Make sure nodes are ordered correctly.
        private void SetIndices()
        {
            nodes = new dbNode[temp_nodes.Count];

            for (int i = 0; i < temp_nodes.Count; i++)
            {
                nodes[temp_nodes[i].index] = temp_nodes[i];
            }
            temp_nodes.Clear();
        }

        // Arrange nodes in groups by level. Level group info is used in layout adjustment.
        List<List<dbNode>> groups = new List<List<dbNode>>();
        void initGroups()
        {
            groups = new List<List<dbNode>>();
            for (int i = 0; i < maxlevel; i++)
            {
                groups.Add(new List<dbNode>());
            }
        }

        // Defines position rectangles for nodes in the nodes list.
        private void PlaceNodes()
        {
            // save order of nodes list
            SetIndices();
            // set nodes in groups by level
            initGroups();
            levelgroups();
            // Set initial layout
            maxNodeX = 200;
            parentspace(nodes[0]);
            AdjustRootPosition();
            HierarchyLayout(nodes[0]);

            // Remove extra space. Several iterations get better results. 
            for (int i = 0; i < 2; i++)
                RemoveExtraSpace();
        }
        #region Node arranging
        /* TODO: Redo. Arranging nodes so that useless empty space is removed is not trivial,
         * since this is a case of everything depending on everything else.
         * Current version works somewhat but not if the tree is heavy on the right side
         * (lots of shorter branches on the left).
         * 
         * This is only done once when debugger is opened, so performance is not the biggest worry here.
         */
        void RemoveExtraSpace()
        {
            contractedMaxNodeX = 0;
            for (int i = 1; i < maxlevel - 1; i++)
            {
                extraspace(i);
                placeUpper(i);
            }
            if (contractedMaxNodeX > 0)
                maxNodeX = contractedMaxNodeX;
        }
        Rect getPlace(int level, float posX)
        {
            if ((posX + nodeSpaceX + nodeWidth) > maxNodeX)
                maxNodeX = posX + nodeSpaceX + nodeWidth;
            return new Rect(posX, level * (nodeHeight + nodeSpaceY), nodeWidth, nodeHeight);
        }
        // Arrange level groups. Nodes should be in priority order (depth-first).
        void levelgroups()
        {
            foreach (dbNode n in nodes)
            {
                if (n.level > groups.Count - 1)
                    groups.Add(new List<dbNode>());
                groups[n.level].Add(n);
            }
        }
        // Adjusts subtree positioning from given level down. Should be simplified, if at all possible.
        void extraspace(int lvl)
        {
            float space = 0;
            for (int i = 1; i < groups[lvl].Count; i++)
            {
                // distance between two nodes on the level, without offset
                float btw = groups[lvl][i].place.x - groups[lvl][i - 1].place.x - nodeSpaceX - nodeWidth;

                // if distance is bigger than zero, check for overlap in children. If overlap, adjust free space value
                if (btw > 0)
                {
                    if (groups[lvl][i].children != null)
                    {
                        // check extra space between children
                        for (int j = 1; j < groups[lvl][i].children.Count; j++)
                        {
                            float temp = distancetolast(groups[lvl][i].children[j]);
                            // check overlap if extra space is removed and remove extra space inside subtree
                            if (temp > 0)
                            {
                                temp = checkchild(groups[lvl][i].children[j], temp);
                                if (temp > 0)
                                    moveleft(groups[lvl][i].children[j], temp);
                            }

                        }
                    }
                    else space = btw;
                    // move subtree left
                    moveleft(groups[lvl][i], space);
                }
            }
        }
        // Move subtree starting at node n left by the given amount.
        // Also widens scrolling area, if necessary.
        void moveleft(dbNode n, float distance)
        {
            n.place.x -= distance;
            if (n.children != null)
            {
                foreach (dbNode b in n.children)
                {
                    moveleft(b, distance);
                }
            }
            if ((n.place.x + nodeSpaceX + nodeWidth) > maxNodeX)
                maxNodeX = n.place.x + nodeSpaceX + nodeWidth;
        }
        // Check subtree from node n to see if it can be moved by the specified amount.
        // Does not check inner distances.
        // Return value is the adjusted amount.
        float checkchild(dbNode n, float dist)
        {
            float temp = distancetolast(n);

            if (temp > dist)
                temp = dist;
            if (n.children != null)
            {
                return checkchild(n.children[0], temp);
            }
            return temp;
        }
        // Returns distance to node directly to the left of the given node. Negative result means overlapping.
        // TODO: Should return greater value if said nodes are not siblings?
        float distancetolast(dbNode nod)
        {
            int ind = 0;
            for (int i = 0; i < groups[nod.level].Count; i++)
                if (nod.index == groups[nod.level][i].index)
                    ind = i;
            if (ind == 0)
                return groups[nod.level][ind].place.x - nodeSpaceX;
            return groups[nod.level][ind].place.x - groups[nod.level][ind - 1].place.x - nodeSpaceX - nodeWidth;
        }
        // Centers parent nodes on their children, from given level upwards.
        // Also adjusts leaf nodes on each level.
        void placeUpper(int level)
        {
            for (int i = level - 1; i >= 0; i--)
            {
                float tempX = -nodeSpaceX;
                foreach (dbNode n in groups[i])
                {
                    float chWidth;
                    if (n.children != null)
                    {
                        chWidth = n.children.Count * (nodeWidth + nodeSpaceX);
                        tempX = (n.children[0].place.x + n.children[n.children.Count - 1].place.x) / 2;
                        n.place.x = tempX;
                    }
                    else
                    {
                        chWidth = nodeWidth + nodeSpaceX;
                    }
                    float check = distancetolast(n);
                    if (check < 0)
                    {
                        moveleft(n, check);
                        tempX += (chWidth - check);
                    }
                    else tempX += chWidth;
                }
            }
        }

        // Simplified version of editor layout functions.
        void HierarchyLayout(dbNode root)
        {
            float curX = 0;
            for (int i = 0; i < root.children.Count; i++)
            {
                float horizontalPos = root.place.x - root.spaceneeded / 2 + (root.children[i].spaceneeded) / 2 + curX;
                curX += root.children[i].spaceneeded;

                root.children[i].place = getPlace(root.children[i].level, horizontalPos);

                if (root.children[i].children != null)
                    HierarchyLayout(root.children[i]);
                else
                {
                    float y = root.children[i].place.y + nodeSpaceY + nodeHeight;
                    if (y > maxNodeY)
                        maxNodeY = y;
                }
            }
        }
        float parentspace(dbNode parent)
        {
            float ownOffset = 0;
            if (parent.children != null)
            {
                foreach (dbNode n in parent.children)
                {
                    ownOffset += parentspace(n);
                }
            }
            if (ownOffset == 0)
            {
                ownOffset = nodeWidth + nodeSpaceX;
            }
            parent.spaceneeded = ownOffset;
            return ownOffset;
        }
        void AdjustRootPosition()
        {
            dbNode root = nodes[0];
            root.place = getPlace(0, root.spaceneeded / 2 + nodeSpaceX);
        }
        #endregion
    }
}
#endif