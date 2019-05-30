using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace BTBuilder
{
    /* Node type mainly controls how a node can be connected to other nodes.
     * Also used in choosing node type icon (type Internal has several options).
     * Invalid nodes have no actual class implementation or class reference is unclear.
     * Editor must be able to show them anyway.
     */
    public enum NODETYPES
    {
        ROOT,
        INTERNAL,
        DECORATOR,
        ACTIONLEAF,
        CONDITIONLEAF,
        INVALID
    }
    // Different stages of zoom have different settings.
    // Note window size does not change, so it remains readable.
    public enum TREE_LOD
    {
        LARGEST,    // Show everything
        MEDIUM,     // No variable editing or node destroying buttons
        SMALL,      // No connection or expand buttons
        TINY        // No label text or buttons, icon + name as tooltip is all
    }
    ////////////////////////////////////////////////////////////////////////
    /*
     * TODO:
     * Check compatibility!
     * 
     * Wrapping & scrolling for notes (if several lines are visible)
     *      Also minimize-button in the note window itself (-), (i) -button is not clear enough
     *  
     * NB! Check correct folder structure: 
     * (http://forum.unity3d.com/threads/where-to-install-your-assets-for-scripting-packages-editor-extensions.292674/)
     * This affects compile order. Since node scripts have to be placed inside BT_Builder folders,
     * they have to be written in C#, but after that the behaviors can be added to a project written in UnityScript or Boo
     * (that's the general idea of Plugins folder; untested with this tool). TODO: Test.
     */

    ////////////////////////////////////////////////////////////////////////

    #region node info.
    public class node
    {
        public string name;
        public Rect windowRect;
        public int uniqueID;        // index may change. This is used for drawing! Each window needs ID.
        public bool isDead;
        public int treeLevel;
        public List<node> children;
        public float spaceNeeded;     // For automatic editor layout (snapping)
        public NODETYPES nodetype;
        public bool isVisible;       // For LOD modifications
        public bool hidden;          // Are subtrees / children visible?
        public string notes;           // For user custom notes
        public bool showNotes;       // For showing said notes
        public Rect noteWindow;
        public Rect noteRect = new Rect(2, 17, 116, 41);
        public int noteID;          // For note window drawing.
        public string subtree_name;    // Used when node is root of imported subtree. Saved as notes (added).
        public int typeIcon_index;

        public List<exportedVariable> vars;

        // Note window size. Cannot be changed from editor.
        private float notewindow_width = 120;
        private float notewindow_height = 60;
        private float note_offset_x = 10;
        private float note_offset_y = 50;

        public node() { }
        // Copy constructor is needed for undo / redo. Values must be copied!
        public node(node node)
        {
            name = node.name;
            windowRect = new Rect(node.windowRect);
            uniqueID = node.uniqueID;
            isDead = node.isDead;
            treeLevel = node.treeLevel;
            children = new List<node>();

            spaceNeeded = node.spaceNeeded;
            nodetype = node.nodetype;
            isVisible = node.isVisible;
            hidden = node.hidden;
            notes = node.notes;
            showNotes = node.showNotes;
            noteWindow = node.noteWindow;
            noteID = node.noteID;
            subtree_name = node.subtree_name;
            typeIcon_index = node.typeIcon_index;
            vars = new List<exportedVariable>();
            foreach (exportedVariable p in node.vars)
            {
                exportedVariable addthis = new exportedVariable();
                addthis.name = p.name;
                addthis.type = p.type;
                addthis.value = p.value;
                addthis.wvalue = p.wvalue;
                vars.Add(addthis);
            }
        }
        public node(NODETYPES type, string _name, int id, int noteId, float posX, float posY, float width, float height)
        {
            name = _name;
            windowRect = new Rect(posX, posY, width, height);
            uniqueID = id;
            isDead = false;
            treeLevel = 0;
            children = new List<node>();
            spaceNeeded = 70;
            nodetype = type;
            isVisible = true;
            notes = "";
            showNotes = false;
            noteWindow = new Rect(windowRect.x + note_offset_x,
                                   windowRect.y + note_offset_y,
                                   notewindow_width,
                                   notewindow_height);
            noteID = noteId;
            vars = new List<exportedVariable>();

            // Find all class variables (properties!) and set them into vars list. Not used for invalid types.
            if (nodetype != NODETYPES.INVALID)
            {
                // Set index for icon. Last index is empty.
                if (nodetype == NODETYPES.ACTIONLEAF)
                    typeIcon_index = 0;
                else if (nodetype == NODETYPES.CONDITIONLEAF)
                    typeIcon_index = 1;
                else if (nodetype == NODETYPES.INTERNAL)
                {
                    // Each internal has its own icon (except decorators)
                    if (name == "Parallel")
                        typeIcon_index = 4;
                    else if (name == "Sequence" || name == "SequenceMemory")
                        typeIcon_index = 2;
                    else if (name == "Selector" || name == "RandomSelector" || name == "SelectorMemory")
                        typeIcon_index = 3;
                    else typeIcon_index = 5;
                }
                else typeIcon_index = 5;

                System.Type t = TreeData.getType(_name); // Editor project cannot access main project classes directly.
                                                         // Double-check that class exists.
                if (t == null)
                {
                    nodetype = NODETYPES.INVALID;
                    typeIcon_index = 5;
                }
                // Find class properties for editing.
                else
                {
                    PropertyInfo[] variables = t.GetProperties(BindingFlags.Public |
                                                               BindingFlags.NonPublic |
                                                               BindingFlags.Instance |
                                                               BindingFlags.FlattenHierarchy);
                    if (variables.Length != 0)
                    {
                        foreach (PropertyInfo p in variables)
                        {
                            {
                                exportedVariable addthis = new exportedVariable();
                                addthis.name = p.Name;
                                System.Type m = p.PropertyType;
                                addthis.type = m.ToString();
                                if (m.IsValueType)
                                    addthis.value = System.Activator.CreateInstance(m);
                                vars.Add(addthis);
                            }
                        }
                    }
                }
            }
        }
    }
    #endregion
    /// <summary>
    /// Custom editor class starts here.
    /// </summary>
    public class BTeditor : EditorWindow
    {
        #region Variables
        // Counter for unique node IDs. TODO: Replace with more secure system if this is too error-prone.
        public int idi = 0;

        // Node type lists. Contents are retrieved from corresponding folders. File name must be the same as class name,
        // since it is used in tree initialisation.
        string[] internaltypes;
        string[] actiontypes;
        string[] conditiontypes;
        string[] decoratortypes;

        // Paths to main folders of each selection button. These may or may not change.
        string xmlfolderpath = "Assets/" + TreeData.package_path + "/BT_Files";
        string internalsfolderpath = "Assets/" + TreeData.package_path + "/BT_Nodes/Internals";
        string actionsfolderpath = "Assets/" + TreeData.package_path + "/BT_Nodes/Actions";
        string conditionsfolderpath = "Assets/" + TreeData.package_path + "/BT_Nodes/Conditions";
        string decoratorsfolderpath = "Assets/" + TreeData.package_path + "/BT_Nodes/Decorators";

        /* Asset path handling for XML files is fairly complicated. Different portions of (absolute) file path are needed
         * in opening, saving, and assigning behavior files and showing file and folder lists in selection menus.
         * File paths consist of the following parts:
         * 
         * 1. Assets -folder (absolute path can be found with Application.datapath)
         * 2. Main package folder (this part is defined in TreeData.package_path; slashes have to be added separately)
         * 3. xml file folder, subfolders included (this can be found in tree_subfolders list)
         * 4. file name (name of the asset, without extensions).
         * 5. .xml extension (this gets checked when opening files).
         * 
         * In addition to this, there is a separate list that is shown in Import dropdown menu.
         * This list (treeFiles) contains paths with parts 3. and 4., but without the main BT_Files folder.
         * 
         * Path to currently open behavior file is saved at import as currentpath, and includes parts 3. and 4.
         */

        // Saved XML files list.
        string[] treeFiles;
        string[] tree_subfolders;

        // Variables for different pieces of the path of currently open tree file.
        string filename = null;                     // Name of xml file to save (no folders)
        int currentfolder = 0;                        // Index of folder to save the xml in (gets path of current
                                                      // folder relative to package, main xml folder included)
        string currentpath = null;                     // Full path to current file, relative to package folder

        // Rectangles for editor controls.
        // Basic field dimensions:
        const float bW = 150;    // ButtonWidth
        const float bH = 30;     // ButtonHeight
        const float lH = 20;     // LabelHeight
        const float rH = 70;     // RowHeight     for short.

        // First row
        Rect SnapToHierarchy_Button = new Rect(bW * 0, 0, bW, bH);
        Rect TreeDepth_Field = new Rect(bW * 0, bH, bW, bH);
        Rect Undo_Button = new Rect(bW * 1, 0, bW / 2, bH);
        Rect Redo_Button = new Rect(bW * 1.5f, 0, bW / 2, bH);
        Rect UndoWarning_TextField = new Rect(bW * 1, bH, bW, lH);
        Rect Save_Button = new Rect(bW * 2, 0, bW, bH);
        Rect SaveName_TextField = new Rect(bW * 2, bH, bW, lH);
        Rect SaveByName_Button = new Rect(bW * 3, 0, bW, bH);
        Rect ImportTree_Button = new Rect(bW * 4, 0, bW, bH);
        Rect ImportSubTree_Toggle = new Rect(bW * 4, bH, bW, lH);
        Rect TreeValidity_Field = new Rect(bW * 5, bH, bW, bH);
        Rect Clear_Button = new Rect(bW * 5, 0, bW, bH);

        // Second row
        Rect CreateNode_Button = new Rect(bW * 0, rH, bW, bH);
        Rect CreateCondition_Button = new Rect(bW * 1, rH, bW, bH);
        Rect CreateAction_Button = new Rect(bW * 2, rH, bW, bH);
        Rect CreateDecorator_Button = new Rect(bW * 3, rH, bW, bH);
        Rect ZoomSlider_Button = new Rect(bW * 4, rH, bW, bH);
        Rect TreeValidity_Field2 = new Rect(bW * 5, rH, bW, bH);

        // Undo / Redo stacks. Basically we just record changes to node list.
        changeStack<node[]> undoStack = new changeStack<node[]>();
        changeStack<node[]> redoStack = new changeStack<node[]>();

        // General node specifics. Used as default position and size for new node.
        const float nodePosX = 0;
        const float nodePosY = 80;
        const float defaultNodeWidth = 130;
        const float defaultNodeHeight = 80;
        const float defaultNodeOffsetX = 190;
        const float defaultNodeOffsetY = 90;

        // Adjustable node size. These are used in actual drawing and can be zoomed.
        float nodeWidth = defaultNodeWidth;
        float nodeHeight = defaultNodeHeight;

        // zooming variables.
        float zoomedTo = 1;     // Range is 0.3 ... 1.
        TREE_LOD lod;           // Save current level here. Lod limits are (currently) set in ZoomSlider function.
        float oldZoom;          // Save previous zoom level to define zoom delta value.

        // Node graphics layout rectangles. 
        Rect Close_button;
        Rect AddConnection_button_left;
        Rect AddConnection_button_right;
        Rect ShowNotes_Button;
        Rect Expand_Button;
        Rect ShowVars_Button;
        Rect Nodetype_Icon;
        Rect Subtree_Label;
        Rect NodeTitle_Label;

        // Separate style for stacked nodes (to show they are stacked)
        GUIStyle stacked, basic;
        GUISkin editorSkin;     // custom skin where these styles can be found.

        // Bezier tangent points
        Vector2 rightOffset = new Vector2(defaultNodeWidth / 2, 0);
        Vector2 leftOffset = new Vector2(-defaultNodeWidth / 2, 0);

        // editor control vars
        bool importAsSubtree = false;
        bool createclick = false;                    // if a node selecting menu should be open.
        float messageTimer = 2;                        // How long the "saved tree" message should be shown.
        float messageCreated = -5;                       // Creating time for "saved" message.    
        int treeDepth = 0;                        // Root is level 0.
        int controlArea_height = 110;                      // Toolbar area height.
        float nodeOffsetX = defaultNodeOffsetX;       // Automatic layout offsets
        float nodeOffsetY = defaultNodeOffsetY;
        BTree activeTree = null;                     // Reference to currently active BTree component. 
                                                     // If tree editor is opened from the component, file is assigned to it on save.

        // General content instance. This is used throughout the editor, for tooltips etc.
        GUIContent currentContent = new GUIContent();

        // editor color vars. Set from init to match current skin.
        Color controlAreaBackground;
        Color controlAreaSeparator;

        // window scrolling
        Vector2 scrollPosition;
        Vector2 lastmouse;          // Saved mouse position, to calculate delta.
        bool isDragging;            // Is view being dragged? Independent from MouseDrag event.
        int draggedId;              // Negative value indicates the view itself, rather than one of the nodes.
        float maxNodeX, maxNodeY;   // Extremes of used area.

        // helpers for WindowFunction
        node currNode;                          // temp variable to store currently drawn node in
        bool connectingRight, connectingLeft;   // Mark connecting nodes in progress
        node startpoint;                        // temp variable for connecting nodes

        // Graphics for WindowFunction
        Texture2D invalidNode, nodeIcons;

        // Texture coordinates for node icons. Dang ugly numbers.
        Rect[] iconFrame =
        {
        new Rect(      0, 0, 0.1667f, 1f),
        new Rect(0.1667f, 0, 0.1667f, 1f),
        new Rect(0.3333f, 0, 0.1667f, 1f),
        new Rect(0.5f,    0, 0.1667f, 1f),
        new Rect(0.6667f, 0, 0.1667f, 1f),
        new Rect(0.8333f, 0, 0.1667f, 1f)
    };

        // Highlight color for currently active connecting button
        Color highlight = new Color(0.7f, 0.7f, 1, 1);

        // container for tree structure
        List<node> nodes = new List<node>();
        #endregion

        #region Initialisation

        [MenuItem("Tools/BT Builder")]
        static void Init()
        {
            BTeditor e = (BTeditor)EditorWindow.GetWindow(typeof(BTeditor));
            e.InitBT(null);
        }
        public void InitBT(BTree tree)
        {
            // Start unique id system from 0 (very simple system)
            idi = 0;
            // Set node size to default zoom level (this defines node graphics rects)
            UpdateNodeRects();
            // Window title is the name of the behavior
            titleContent = new GUIContent("new behavior");
            // Add root node as a starting point (updater functions require it)
            nodes.Add(new node(NODETYPES.ROOT, "RootNode", GetUniqueId(), GetUniqueId(), 0, 200, nodeWidth, nodeHeight));
            // Create a tree file folder, if there isn't one
            if (!AssetDatabase.IsValidFolder(xmlfolderpath))
            {
                Debug.LogWarning("Cannot find BT_Files folder. Creating new folder at path " + xmlfolderpath);
                AssetDatabase.CreateFolder("Assets/" + TreeData.package_path, "BT_Files");
            }
            // Get custom skin. If skin is not found, default skin will be used.
            editorSkin = (GUISkin)EditorGUIUtility.Load("BT_Builder/BT_Editor.guiskin");
            if (editorSkin == null)
            {
                Debug.LogError("Cannot find GUIskin BT_Editor at path Assets/Editor Default Resources/BT_Builder");
                Debug.LogWarning("Using default window style. Some graphics may not be displayed correctly.");
            }
            // Populate file and node menus
            GetXMLFiles(xmlfolderpath, ref treeFiles, ref tree_subfolders);
            GetFiles(internalsfolderpath, ref internaltypes);
            GetFiles(actionsfolderpath, ref actiontypes);
            GetFiles(conditionsfolderpath, ref conditiontypes);
            GetFiles(decoratorsfolderpath, ref decoratortypes);
            // Save reference to the component the editor was opened from, if any
            activeTree = tree;
            // Set graphics depending on the general theme
            if (EditorGUIUtility.isProSkin)
            {
                controlAreaBackground = new Color(0.24f, 0.24f, 0.24f, 1);
                controlAreaSeparator = new Color(0.1f, 0.1f, 0.1f, 1);
                invalidNode = (Texture2D)EditorGUIUtility.Load("BT_Builder/invalidNode.png");
                nodeIcons = (Texture2D)EditorGUIUtility.Load("BT_Builder/nodeIconsDark.png");
                if (editorSkin != null)
                    stacked = editorSkin.FindStyle("stacked");
            }
            else
            {
                controlAreaBackground = new Color(0.85f, 0.85f, 0.85f, 1);
                controlAreaSeparator = new Color(0.55f, 0.55f, 0.55f, 1);
                invalidNode = (Texture2D)EditorGUIUtility.Load("BT_Builder/invalidNodeLight.png");
                nodeIcons = (Texture2D)EditorGUIUtility.Load("BT_Builder/nodeIconsLight.png");
                if (editorSkin != null)
                {
                    stacked = editorSkin.FindStyle("stacked_light");
                    basic = editorSkin.FindStyle("light");
                }
            }
            // Editor can be used without node type graphics. Not finding them is still an error.
            if (!invalidNode)
                Debug.LogError("Cannot find invalid node texture at path Assets/Editor Default Resources/BT_Builder");
            if (!nodeIcons)
                Debug.LogError("Cannot find node icons texture at path Assets/Editor Default Resources/BT_Builder");
        }
        #endregion

        #region GUI functions
        /* TODO: Remove this comment as it's not really about this particular OnGUI implementation.
         * 
         * Notes on Unity's gui updating and input event handling:
         * OnGUI gets called several times a frame. By default (no input) one frame's call are Layout and Repaint.
         * For every input event Layout gets called first and then the said input event. The call order of a frame
         * would then be e.g. Layout - Repaint - Layout - MouseDrag - Layout - KeyDown (There was a mouse dragging
         * event and a key press happening at the same time). Layout - Repaint seems to get called only when there
         * is a reason to Repaint. 
         * Also seems like an event can register several time during one frame (especially KeyDown event). Different
         * threads? Documentation on the subject is vague.
         */
        private void OnGUI()
        {
            // Graphics setup ------------------------------------------

            // If custom skin was not found, use default skin instead.
            if (editorSkin == null)
                editorSkin = GUI.skin;
            // Default GUI skin can only be called inside OnGUI, so Pro skin basic style cannot be set in Init.
            else if (basic == null)
            {
                basic = GUI.skin.window;
            }
            // slightly tinted background for tools area. Also darker line to separate control area from tree window.
            EditorGUI.DrawRect(new Rect(0, 0, position.width, controlArea_height), controlAreaBackground);
            EditorGUI.DrawRect(new Rect(0, controlArea_height - 1, position.width, 1), controlAreaSeparator);

            // Editor controls -----------------------------------------

            // NB! Checking events seems to mess up mouse position info, so that generic menus may appear
            // in strange places. Not able to detect why this happens.
            CreateInternalButton();
            CreateActionButton();
            CreateConditionButton();
            CreateDecoratorButton();
            HierarchyButton();
            UndoButton();
            RedoButton();
            SaveToXMLButton();
            SaveByNameButton();
            ImportButton();
            SubtreeToggle();
            ClearButton();
            ZoomSlider();

            // Mouse events --------------------------------------------

            // (other than button clicks)

            // Update dragging status. Separated from MouseDrag event to allow drawing while view is dragged.
            if (Event.current.type == EventType.MouseDown)
            {
                // Define valid area to exclude scrollbars and tools area.
                if (new Rect(0,
                             controlArea_height,
                             position.width - 20,
                             position.height - controlArea_height - 20).Contains(Event.current.mousePosition))
                    isDragging = true;
            }
            // rawType also finds events ignored by default (such as mouseUp outside editor window).
            if (Event.current.rawType == EventType.MouseUp)
            {
                draggedId = -1;
                isDragging = false;
            }

            // Import subtree on context click. Should it say somewhere what is going on?
            if (Event.current.type == EventType.ContextClick)
            {
                GenericMenu gen = new GenericMenu();
                foreach (string s in treeFiles)
                {
                    gen.AddItem(new GUIContent(s), false, ImportSubtreeFunc, s);
                }
                gen.DropDown(new Rect(Event.current.mousePosition, new Vector2()));
            }

            // NB! control is key event.
            // Zoom with mouse wheel when ctrl is down:
            if (Event.current.control && Event.current.type == EventType.ScrollWheel)
            {
                zoomedTo -= Event.current.delta.y / 100;
                UpdateZoom();
                Event.current.Use();    // stop window scrolling.
            }

            // Move editor view by dragging.
            // draggedId is used to determine if there's a node being dragged or not.
            // Negative draggedId means there's no node being dragged and background should be moved.
            // Defaulted to -1 when drag exited, set to window id when node dragging starts.
            if (isDragging && draggedId < 0)
            {
                scrollPosition += lastmouse - Event.current.mousePosition;
                Repaint();
            }
            // Update last mouse position also when not dragged, to avoid jumping.
            lastmouse = Event.current.mousePosition;

            // Keyboard events -----------------------------------------

            // Currently only keyUps; if other types are added, group all under isKey condition
            if (Event.current.type == EventType.KeyUp)
            {
                // Save with ctrl+s: This also saves current scene, since shortcuts can't be intercepted.
                if (Event.current.control && Event.current.keyCode == KeyCode.S)
                {
                    SaveTree(false);
                }
                // Snap to grid with spacebar:
                if (Event.current.control && Event.current.keyCode == KeyCode.Space)
                {
                    Focus();
                    SnapToGrid();
                }
                // Undo / redo shortcuts affect main editor and cannot be intercepted.
                // To prevent accidental undos, counter effect while bt editor has focus.
                if (Event.current.control && Event.current.keyCode == KeyCode.Z)
                {
                    Undo.PerformRedo();
                    if (undoStack.Count > 0)
                    {
                        redoStack.Push(CopyNodes(nodes));
                        CopyBack(undoStack.Pop());
                        Repaint();
                    }
                }
                if (Event.current.control && Event.current.keyCode == KeyCode.Y)
                {
                    Undo.PerformUndo();
                    if (redoStack.Count > 0)
                    {
                        undoStack.Push(CopyNodes(nodes));
                        CopyBack(redoStack.Pop());
                        Repaint();
                    }
                }
            }

            // Tool row info panels ------------------------------------

            // Check if initialization has happened (there is at least one node in the editor).
            if (nodes.Count > 0)
            {
                TreeLevelMessage();
                TreeValidityMessage(); // NB! This has two messages (connects to root and ends with leaf).
                TreeSavedMessage();
            }
            // Put back root node if node list is empty.
            else nodes.Add(new node(NODETYPES.ROOT,
                                    "RootNode",
                                    GetUniqueId(),
                                    GetUniqueId(),
                                    0,
                                    200,
                                    nodeWidth,
                                    nodeHeight));

            // Tree window drawing -------------------------------------

            // scrolling tree area begins here.
            scrollPosition = GUI.BeginScrollView(new Rect(0,
                                                          controlArea_height,
                                                          position.width,
                                                          position.height - controlArea_height),
                                                 scrollPosition,
                                                 new Rect(0,
                                                          controlArea_height,
                                                          maxNodeX,
                                                          maxNodeY - controlArea_height));

            // Draw connecting bezier lines between nodes
            Handles.BeginGUI();
            DrawConnections();
            Handles.EndGUI();

            // Window is sized down only when dragging is not happening.
            if (!isDragging)
            {
                maxNodeX = 200;
                maxNodeY = 200;
            }

            // Draw nodes
            BeginWindows();
            foreach (node n in nodes)
            {
                if (n.isVisible)
                {
                    if (n.showNotes)
                        n.noteWindow = GUI.Window(n.noteID, n.noteWindow, NoteWindowFunction, "notes");
                    if (n.hidden)
                        n.windowRect = GUI.Window(n.uniqueID, n.windowRect, WindowFunction, "", stacked);
                    else n.windowRect = GUI.Window(n.uniqueID, n.windowRect, WindowFunction, "", basic);

                }
            }
            EndWindows();

            GUI.EndScrollView();
            //------------------------------------ End of window drawing

            // Destroy destroyed nodes
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i].isDead)
                    nodes.RemoveAt(i);
            }
        }

        // Node window functionality.
        void WindowFunction(int windowID)
        {
            int index = FindNodeWithId(windowID);
            // If corresponding node is not found, index is -1.
            if (index >= 0)
            {
                currNode = nodes[index];

                // Does node have invisible children?
                currNode.hidden = (currNode.children.Count != 0 && !currNode.children[0].isVisible);

                // Open context menu by rightclicking
                if (Event.current.GetTypeForControl(windowID) == EventType.MouseDown && Event.current.button == 1)
                {
                    GenericMenu gen = new GenericMenu();
                    if (currNode.children.Count != 0)
                        gen.AddItem(new GUIContent("Hide children"), currNode.hidden, NB_Expand_callback, index);
                    gen.AddItem(new GUIContent("Show notes"), currNode.showNotes, NB_noteToggle_callback, index);
                    gen.DropDown(new Rect(Event.current.mousePosition, new Vector2()));
                }
                // Kill node by pressing delete key. Don't kill root.
                if (currNode.nodetype != NODETYPES.ROOT
                    && Event.current.type == EventType.KeyDown
                    && Event.current.keyCode == KeyCode.Delete)
                {
                    undoStack.Push(CopyNodes(nodes));
                    KillNode(currNode);
                    KillConnections(currNode);
                    treeDepth = GetUpdatedTreeDepth(FindRoot(), 0);
                    Repaint();
                }

                // Check window boundaries: area cannot be expanded to left / top
                if (currNode.windowRect.x < 0)
                    currNode.windowRect.x = 0;
                if (currNode.windowRect.y < controlArea_height)
                    currNode.windowRect.y = controlArea_height;

                // Update extremes of currently used area
                int extraSpace = 10;
                if (currNode.showNotes) extraSpace += 50;
                if (currNode.windowRect.xMax > maxNodeX)
                    maxNodeX = currNode.windowRect.xMax + 10;
                if (currNode.windowRect.yMax > maxNodeY)
                    maxNodeY = currNode.windowRect.yMax + extraSpace;

                // Update notes position
                currNode.noteWindow.x = currNode.windowRect.x + (10 * zoomedTo);
                currNode.noteWindow.y = currNode.windowRect.y + (65 * zoomedTo);


                // Actual drawing: Title and type graphics are always drawn

                // Since giving a title content to GUI.Window doesn't seem to result in working tooltips,
                // we have this wacky thing instead. The actual window title is empty.
                GUIContent nodeTitle = new GUIContent(currNode.name, currNode.notes);

                // Show name as tooltip if zoomed too small.
                if (lod != TREE_LOD.LARGEST)
                {
                    nodeTitle.tooltip = currNode.name + "\n" + currNode.notes;
                    if (lod == TREE_LOD.TINY)
                        nodeTitle.text = "";
                }
                // Draw nodeTitle where window title would be.
                GUI.Label(NodeTitle_Label, nodeTitle);

                // Draw invalid node texture if invalid; draw type icon if not
                // Missing graphics only affect view, not functionality
                if (currNode.nodetype == NODETYPES.INVALID && invalidNode != null)
                {
                    if (lod == TREE_LOD.TINY)
                        GUI.DrawTexture(new Rect(3, 3, nodeWidth - 6, nodeHeight - 6), invalidNode, ScaleMode.StretchToFill);
                    else GUI.DrawTexture(new Rect(3, 17, nodeWidth - 6, nodeHeight - 20), invalidNode, ScaleMode.StretchToFill);
                }
                else if (currNode.nodetype != NODETYPES.ROOT && nodeIcons != null)
                    GUI.DrawTextureWithTexCoords(Nodetype_Icon, nodeIcons, iconFrame[currNode.typeIcon_index]);

                // LOD groups: what gets drawn and when. Default is largest.
                switch (lod)
                {
                    case TREE_LOD.LARGEST:
                        {
                            NB_noteToggle(currNode);
                            NB_delete(currNode);
                            NB_leftConnect(currNode);
                            NB_rightConnect(currNode);
                            NB_editVars(currNode);
                            NB_subtreeLabel(currNode);
                            NB_Expand(currNode);
                            break;
                        }
                    case TREE_LOD.MEDIUM:
                        {
                            NB_noteToggle(currNode);
                            NB_delete(currNode);
                            NB_leftConnect(currNode);
                            NB_rightConnect(currNode);
                            NB_subtreeLabel(currNode);
                            NB_Expand(currNode);
                            break;
                        }
                    case TREE_LOD.SMALL:
                        {
                            NB_noteToggle(currNode);
                            break;
                        }
                    case TREE_LOD.TINY:
                        {
                            break;
                        }
                    default:
                        {
                            NB_noteToggle(currNode);
                            NB_delete(currNode);
                            NB_leftConnect(currNode);
                            NB_rightConnect(currNode);
                            NB_editVars(currNode);
                            NB_subtreeLabel(currNode);
                            NB_Expand(currNode);
                            break;
                        }
                }
            }

            // Move scroll view when node is dragged.
            if (Event.current.type == EventType.MouseDrag)
            {
                draggedId = windowID;
                if (currNode.windowRect.xMax > scrollPosition.x + position.width)
                    scrollPosition.x = currNode.windowRect.xMax - position.width;
                if (currNode.windowRect.x >= 0 && currNode.windowRect.x < scrollPosition.x)
                    scrollPosition.x = currNode.windowRect.x;
                if (currNode.windowRect.yMax > scrollPosition.y + position.height)
                    scrollPosition.y = currNode.windowRect.yMax - position.height;
                if (currNode.windowRect.y >= controlArea_height && currNode.windowRect.y < scrollPosition.y + controlArea_height)
                    scrollPosition.y = currNode.windowRect.y - controlArea_height;
            }

            GUI.DragWindow();
        }

        #endregion

        #region Node window elements / controls
        // These get called depending on zoom level.

        // Left connecting button (connect to parent)
        void NB_leftConnect(node cuurNode)
        {
            if (cuurNode.nodetype != NODETYPES.ROOT)
            {
                GUIContent label = new GUIContent("<", "connect to parent");
                if (connectingLeft && startpoint == cuurNode)
                    GUI.color = highlight;
                if (GUI.Button(AddConnection_button_left, label))
                {
                    if (connectingRight)
                    {
                        connectingRight = false;
                        ConnectNodes(startpoint, cuurNode);
                    }
                    else if (connectingLeft)
                    {
                        connectingLeft = false;
                    }
                    else if (!connectingLeft)
                    {
                        connectingLeft = true;
                        startpoint = cuurNode;
                    }
                    else { }
                }
                GUI.color = Color.white;
            }
        }
        // Right connecting button (connect to child)
        void NB_rightConnect(node cuurNode)
        {
            if (cuurNode.nodetype != NODETYPES.ACTIONLEAF && cuurNode.nodetype != NODETYPES.CONDITIONLEAF && !cuurNode.hidden)
            {
                GUIContent label = new GUIContent(">", "connect to child");
                if (connectingRight && startpoint == cuurNode)
                    GUI.color = highlight;
                if (GUI.Button(AddConnection_button_right, label))
                {
                    if (connectingLeft)
                    {
                        connectingLeft = false;
                        ConnectNodes(cuurNode, startpoint);
                    }
                    else if (connectingRight)
                    {
                        connectingRight = false;
                    }
                    else if (!connectingRight)
                    {
                        connectingRight = true;
                        startpoint = cuurNode;
                    }
                    else { }
                }
                GUI.color = Color.white;
            }
        }
        // Show/hide notes toggle button
        void NB_noteToggle(node cuurNode)
        {
            GUIContent label = new GUIContent("i", "show / hide notes");
            if (GUI.Button(ShowNotes_Button, label))
            {
                cuurNode.showNotes = !cuurNode.showNotes;
                SnapToGrid();
            }
        }
        // Show/hide notes callback for context clicking node
        void NB_noteToggle_callback(object node_index)
        {
            int i = (int)node_index;
            nodes[i].showNotes = !nodes[i].showNotes;
            SnapToGrid();
        }
        // Show variable pop up button
        void NB_editVars(node cuurNode)
        {
            if (cuurNode.nodetype != NODETYPES.ROOT
                && cuurNode.nodetype != NODETYPES.INVALID
                && cuurNode.vars.Count > 0)
            {
                GUIContent label = new GUIContent("Edit", "Edit node properties");
                if (GUI.Button(ShowVars_Button, label))
                {
                    ValueEditor.Init(ref cuurNode.vars, cuurNode.name);
                }
            }
        }
        // Show/hide subtree toggle button. Only visible if node is connected to root.
        // This is because expanding subtree rearranges tree, which ignores nodes not connected to root.
        void NB_Expand(node cuurNode)
        {
            if (cuurNode.children.Count != 0 && cuurNode.nodetype != NODETYPES.ROOT)
            {
                GUIContent label = new GUIContent("E", "show / hide children");
                if (!CheckChildren(FindRoot(), cuurNode))
                    GUI.enabled = false;
                if (GUI.Button(Expand_Button, label))
                {
                    undoStack.Push(CopyNodes(nodes));
                    SetVisibilityOfChildren(cuurNode, cuurNode.hidden);
                    if (cuurNode.hidden)
                    {
                        SnapToGrid();
                    }
                }
                GUI.enabled = true;
            }
        }
        // callback for expanding toggle by context click.
        void NB_Expand_callback(object node_index)
        {
            int i = (int)node_index;
            SetVisibilityOfChildren(nodes[i], nodes[i].hidden);
            SnapToGrid();
        }
        // Imported subtree name label
        void NB_subtreeLabel(node cuurNode)
        {
            if (cuurNode.subtree_name != null)
            {
                currentContent.text = cuurNode.subtree_name;
                currentContent.tooltip = cuurNode.subtree_name;
                GUI.Label(Subtree_Label, currentContent);
            }
        }
        // Node deleting button
        void NB_delete(node cuurNode)
        {
            if (cuurNode.nodetype != NODETYPES.ROOT)
            {
                GUIContent label = new GUIContent("X", "delete");
                if (GUI.Button(Close_button, label))
                {
                    undoStack.Push(CopyNodes(nodes));

                    KillNode(cuurNode);
                    KillConnections(cuurNode);
                    treeDepth = GetUpdatedTreeDepth(FindRoot(), 0);
                }
            }
        }
        // Message window function.
        void NoteWindowFunction(int windowID)
        {
            int index = FindNodeWithNoteId(windowID);
            if (index >= 0)
            {
                currNode = nodes[index];
                string note = currNode.notes;
                note = EditorGUI.TextArea(currNode.noteRect, note);
                if (note != currNode.notes)
                {
                    undoStack.Push(CopyNodes(nodes));
                    currNode.notes = note;
                }
            }
        }
        #endregion
        //////////////////////////////////////////////////
        //// Helpers
        //////////////////////////////////////////////////
        #region Node / XML organisers

        // Organisers for finding files in project and arranging them to correct lists.
        // Used in editor initialisation.

        // Read node file names to given arrays.
        void GetFiles(string folderPath, ref string[] listToFill)
        {
            string[] folder = new string[] { folderPath };
            string[] temp = AssetDatabase.FindAssets("", folder);

            List<string> templist = new List<string>();
            // Populate.
            for (int i = 0; i < temp.Length; i++)
            {
                string fname = GetFullPath(temp[i], folderPath.Length);
                if (checkExtension(fname) != null)
                {
                    // Check if name represents a class that inherits from BaseNode
                    string strippedName = removeExtension(fname);
                    if (CheckIfNode(strippedName))
                    {
                        templist.Add(strippedName);
                    }
                }
            }
            listToFill = new string[templist.Count];
            templist.CopyTo(listToFill);
        }
        // different system for xml files. Also saves folders for saving.
        // Folderpath is main BT_Files folder path
        void GetXMLFiles(string folderPath, ref string[] listToFill, ref string[] subfolderlist)
        {
            string[] folder = new string[] { folderPath };
            string[] tempguids = AssetDatabase.FindAssets("", folder);

            List<string> templist = new List<string>();
            List<string> tempfolderlist = new List<string>();

            // Folder list has paths to all existing folders that are found by the editor.
            // BT_Files is the default folder. Add this to all paths.
            tempfolderlist.Add("BT_Files");

            // Populate lists:
            for (int i = 0; i < tempguids.Length; i++)
            {
                // path inside main BT_Files
                string fname = GetFullPath(tempguids[i], folderPath.Length);
                string ext = checkExtension(fname);
                if (ext == "xml")
                {
                    // Only files containing TreeData type information are included. Slow, but works.
                    if (TreeData.isValidBehavior(Path.Combine(Application.dataPath, TreeData.package_path + "/BT_Files/" + fname)))
                        templist.Add(removeExtension(fname));
                }
                // Add folder to available folders list. Add BT_Files to name?
                else if (ext == null)
                {
                    string subfolderPath = "BT_Files/" + fname;
                    if (!tempfolderlist.Contains(subfolderPath))
                        tempfolderlist.Add(subfolderPath);
                }
            }
            // set lists to arrays used by the editor:
            listToFill = new string[templist.Count];
            templist.CopyTo(listToFill);
            subfolderlist = new string[tempfolderlist.Count];
            tempfolderlist.CopyTo(subfolderlist);
        }
        #endregion

        #region Editor control buttons

        // (And bezier drawing.)
        void DrawConnections()
        {
            foreach (node n in nodes)
            {
                if (n.children.Count > 0 && n.children[0].isVisible)
                {
                    foreach (node child in n.children)
                    {
                        Handles.DrawBezier(n.windowRect.center + rightOffset,
                                           child.windowRect.center + leftOffset,
                                           new Vector2(n.windowRect.xMax + 20f, n.windowRect.center.y),
                                           new Vector2(child.windowRect.x - 20f, child.windowRect.center.y),
                                           Color.blue,
                                           null,
                                           5f);
                    }
                }
            }
        }

        void HierarchyButton()
        {
            if (GUI.Button(SnapToHierarchy_Button, "Snap to grid"))
            {
                SnapToGrid();
            }
        }

        void UndoButton()
        {
            if (undoStack.Count <= 0)
                GUI.enabled = false;
            if (GUI.Button(Undo_Button, "Undo"))
            {
                redoStack.Push(CopyNodes(nodes));
                CopyBack(undoStack.Pop());
                Repaint();
            }
            GUI.enabled = true;
        }
        void RedoButton()
        {
            if (redoStack.Count <= 0)
                GUI.enabled = false;
            if (GUI.Button(Redo_Button, "Redo"))
            {
                undoStack.Push(CopyNodes(nodes));
                CopyBack(redoStack.Pop());
                //nodes.Clear();
                //foreach (node n in redoStack.Pop())
                //    nodes.Add(n);
                Repaint();
            }
            GUI.enabled = true;
        }
        void TreeValidityMessage()
        {
            if (!EndsInLeaf())
            {
                currentContent.text = "Warning: Not all \n branches end in leaf";
                currentContent.tooltip = "All branches must have at least one action or condition as the last node, otherwise the behavior cannot run";
                EditorGUI.LabelField(TreeValidity_Field, currentContent);
            }

            if (!ConnectsToRoot())
            {
                currentContent.text = "Warning: Not all nodes \nare connected to root";
                currentContent.tooltip = "Only nodes that are connected to root will be saved.";
                EditorGUI.LabelField(TreeValidity_Field2, currentContent);
            }
        }
        // Some feedback telling user that saving has actually happened.
        void TreeSavedMessage()
        {
            if (Time.realtimeSinceStartup < messageCreated + messageTimer)
            {
                EditorGUI.LabelField(SaveName_TextField, "Behavior saved.");
            }
        }
        void TreeLevelMessage()
        {
            currentContent.text = "Tree Depth: " + treeDepth.ToString();
            currentContent.tooltip = "Very deep trees may cause stack overflow.";
            EditorGUI.LabelField(TreeDepth_Field, currentContent);
        }
        void CreateInternalButton()
        {
            if (GUI.Button(CreateNode_Button, "Create new internal..."))
            {
                createclick = !createclick;
            }
            if (createclick)
            {
                GenericMenu gen = new GenericMenu();
                foreach (string s in internaltypes)
                {
                    gen.AddItem(new GUIContent(s), false, CreateNodeFunc, s);
                }
                gen.DropDown(CreateNode_Button);

                if (!CreateNode_Button.Contains(Input.mousePosition))
                    createclick = false;
            }
        }
        void CreateActionButton()
        {
            if (GUI.Button(CreateAction_Button, "Create new action..."))
            {
                createclick = !createclick;
            }
            if (createclick)
            {
                GenericMenu gen = new GenericMenu();
                foreach (string s in actiontypes)
                {
                    gen.AddItem(new GUIContent(s), false, CreateNodeFunc, s);
                }
                gen.DropDown(CreateAction_Button);

                if (!CreateAction_Button.Contains(Input.mousePosition))
                    createclick = false;
            }
        }
        void CreateConditionButton()
        {
            if (GUI.Button(CreateCondition_Button, "Create new condition..."))
            {
                createclick = !createclick;
            }
            if (createclick)
            {
                GenericMenu gen = new GenericMenu();
                foreach (string s in conditiontypes)
                {
                    gen.AddItem(new GUIContent(s), false, CreateNodeFunc, s);
                }
                gen.DropDown(CreateCondition_Button);

                if (!CreateCondition_Button.Contains(Input.mousePosition))
                    createclick = false;
            }
        }
        void CreateDecoratorButton()
        {
            if (GUI.Button(CreateDecorator_Button, "Create new decorator..."))
            {
                createclick = !createclick;
            }
            if (createclick)
            {
                GenericMenu gen = new GenericMenu();
                foreach (string s in decoratortypes)
                {
                    gen.AddItem(new GUIContent(s), false, CreateNodeFunc, s);
                }
                gen.DropDown(CreateDecorator_Button);

                if (!CreateDecorator_Button.Contains(Input.mousePosition))
                    createclick = false;
            }
        }
        // Callback for node creating dropdown menus
        void CreateNodeFunc(object name)
        {
            string _name = (string)name;
            CreateNode(_name, GetNodeType(_name));
            createclick = false;
        }
        void ClearButton()
        {
            if (GUI.Button(Clear_Button, "New Behavior"))
            {
                nodes.Clear();
                node root = new node(NODETYPES.ROOT, "RootNode", GetUniqueId(), GetUniqueId(), 0, 200, nodeWidth, nodeHeight);
                nodes.Add(root);
                treeDepth = GetUpdatedTreeDepth(root, 0);
                filename = null;
                this.titleContent.text = "new behavior";
            }
        }
        void SaveToXMLButton()
        {
            if (activeTree != null)
            {
                currentContent.tooltip = "Save and assign this behavior to currently active BTree component.";
            }
            else currentContent.tooltip = null;
            currentContent.text = "Save Tree";
            if (GUI.Button(Save_Button, currentContent))
            {
                SaveTree(false);
            }
        }
        void SaveByNameButton()
        {
            if (activeTree != null)
            {
                currentContent.tooltip = "Save and assign this behavior to currently active BTree component.";
            }
            else currentContent.tooltip = null;
            currentContent.text = "Save Tree as...";
            if (GUI.Button(SaveByName_Button, currentContent))
            {
                SaveTree(true);
            }
        }
        // General tree saving function.
        void SaveTree(bool saveByName)
        {
            // Update priority by visual positioning
            foreach (node n in nodes)
            {
                if (n.children.Count > 1)
                {
                    updatePriority(n);
                }
            }
            // Open dialog only if new name is desired or no name is given
            if (saveByName || filename == null || filename == "")
            {
                DialogEditor.Init(filename, this, ref tree_subfolders, currentfolder);
            }
            else SaveFile(currentpath);
        }
        void ImportButton()
        {
            if (GUI.Button(ImportTree_Button, "Open existing..."))
            {
                createclick = !createclick;
            }
            if (createclick)
            {
                GenericMenu gen = new GenericMenu();
                foreach (string s in treeFiles)
                {
                    gen.AddItem(new GUIContent(s), false, ImportXMLFunc, s);
                }
                gen.DropDown(ImportTree_Button);

                if (!ImportTree_Button.Contains(Input.mousePosition))
                    createclick = false;
            }
        }
        // Import button menu callback.
        void ImportXMLFunc(object file)
        {
            string name = (string)file;
            OpenTreeForEditing(name, importAsSubtree);
        }
        // Import context menu callback. Ignores subtree toggle and forces subtree import.
        void ImportSubtreeFunc(object file)
        {
            string name = (string)file;
            OpenTreeForEditing(name, true);
        }
        void SubtreeToggle()
        {
            currentContent.text = "Import as subtree";
            currentContent.tooltip = "Keep the current behavior open, so the opened file can be added to it";
            importAsSubtree = GUI.Toggle(ImportSubTree_Toggle, importAsSubtree, currentContent);
        }

        // zooming.
        void ZoomSlider()
        {
            oldZoom = zoomedTo;
            zoomedTo = EditorGUI.Slider(ZoomSlider_Button, zoomedTo, 0.3f, 1);
            // Recalculate if value is changed
            if (oldZoom != zoomedTo)
            {
                UpdateZoom();
            }
        }
        // Calculate new values for zoomable elements
        void UpdateZoom()
        {
            // define LOD level
            if (zoomedTo < 0.45f)
                lod = TREE_LOD.TINY;
            else if (zoomedTo < 0.57f)
                lod = TREE_LOD.SMALL;
            else if (zoomedTo < 0.8f)
                lod = TREE_LOD.MEDIUM;
            else lod = TREE_LOD.LARGEST;

            // Set new sizes for nodes
            nodeWidth = defaultNodeWidth * zoomedTo;
            nodeHeight = defaultNodeHeight * zoomedTo;
            nodeOffsetX = defaultNodeOffsetX * zoomedTo;
            nodeOffsetY = defaultNodeOffsetY * zoomedTo;

            // Beziers too.
            rightOffset.x = nodeWidth / 2;
            leftOffset.x = -nodeWidth / 2;

            // Adjust nodes' gui style. Stacked border should scale but stay in place.
            stacked.overflow.bottom = 16 - (int)lod;
            stacked.overflow.right = 16 - (int)lod;
            stacked.border.bottom = 10 + (int)lod;
            stacked.border.right = 10 + (int)lod;

            // scale node window rect (these are set when node is created)
            foreach (node n in nodes)
            {
                n.windowRect.width = nodeWidth;
                n.windowRect.height = nodeHeight;
            }

            // Keep center of current tree view centered
            float deltazoom = zoomedTo / oldZoom;
            float newX = (scrollPosition.x + (position.width / 2)) * deltazoom - (position.width / 2);
            float newY = (scrollPosition.y + (position.height / 2)) * deltazoom - (position.height / 2);
            if (newX >= 0)
                scrollPosition.x = newX;
            if (newY >= 0)
                scrollPosition.y = newY;

            // scale node controls and other node window content
            UpdateNodeRects();

            // Rearrange
            SnapToGrid();
        }
        #endregion

        #region Saving and opening files

        // full path means the part that starts "BT_Files/..." and ends in file name, no extension.
        public void SaveFile(string fullPathFromPackage)
        {
            // Set full path to currentpath, in case the parameter came from save by name -dialog
            currentpath = fullPathFromPackage;
            TreeData data = new TreeData();
            data.RootNode = Convert(FindRoot());
            string assetPath = fullPathFromPackage + ".xml";
            filename = GetNameWithoutFolders(fullPathFromPackage);

            data.Save(Path.Combine(Application.dataPath, TreeData.package_path + "/" + assetPath));

            AssetDatabase.Refresh();

            // Update file list
            GetXMLFiles(xmlfolderpath, ref treeFiles, ref tree_subfolders);

            if (activeTree != null)
                AssignToCurrentTree(fullPathFromPackage);
            // Behavior saved -message
            messageCreated = Time.realtimeSinceStartup;
            this.titleContent.text = filename;
            // Lost focus kills saving dialog.
            this.Focus();
        }
        // Assign reference to saved file to currently active BTree component.
        // Only called if editor was opened from component's "edit file" button.
        void AssignToCurrentTree(string fullPathFromPackage)
        {
            // if BTree is active, assign saved file to it
            if (activeTree != null)
            {
                string completeAssetPath = "Assets/" + TreeData.package_path + "/" + fullPathFromPackage + ".xml";
                TextAsset te = (TextAsset)AssetDatabase.LoadAssetAtPath(completeAssetPath, typeof(TextAsset));
                activeTree.SetTreeFile(te);
            }
        }

        // filepath is path relative to BT_Files folder (to find files in subfolders when accessed
        // from BTree inspector
        public void OpenTreeForEditing(string fullPathFromBT_Files, bool isSubtree)
        {
            // filename should have no folder path attached.
            string treeName = GetNameWithoutFolders(fullPathFromBT_Files);
            // Save the full path relative to Assets folder
            string tempPath = "BT_Files/" + fullPathFromBT_Files;
            TreeData data;
            {
                data = TreeData.Load(Path.Combine(Application.dataPath, TreeData.package_path + "/" + tempPath + ".xml"));
                // Subtree is added as stacked node, editor is not cleared
                if (isSubtree)
                {
                    node n = ConvertBack(data.RootNode.children[0]);
                    SetVisibilityOfChildren(n, false);
                    n.subtree_name = treeName;
                }
                // Full tree is opened as new tree.
                else
                {
                    nodes.Clear();
                    ConvertBack(data.RootNode);
                    treeDepth = GetUpdatedTreeDepth(FindRoot(), 0);
                    SnapToGrid();
                    filename = treeName;
                    currentpath = tempPath;
                    this.titleContent.text = treeName;
                    // HACK-like way to extract folder name. File name and folder should be defined separately.
                    string path = currentpath.Substring(0, currentpath.Length - (treeName.Length + 1));
                    currentfolder = FindFolderIndex(path);
                }
            }
        }
        // Check if proposed folder is present in existing folders list, and returns its index.
        int FindFolderIndex(string folderPathFromPackage)
        {
            for (int i = 0; i < tree_subfolders.Length; ++i)
            {
                if (tree_subfolders[i].Contains(folderPathFromPackage))
                    return i;
            }
            // Use main folder as default. TODO: See if this is wrong.
            return 0;
        }
        #endregion

        #region Importing / exporting data conversions
        /// NB! These are recursive and return root node of the tree.
        /// Root type should be RootNode.

        // Convert to exportable type (only some of the data is necessary)
        expNode Convert(node root)
        {
            expNode node = new expNode();
            node.name = root.name;
            // Add subtree title to notes. NB! This may (still) cause extra lines and possible duplicates.
            if (root.subtree_name != null)
                node.notes = root.subtree_name + "\n" + root.notes;
            else node.notes = root.notes;
            node.isExpanded = root.isVisible;
            if (root.vars.Count != 0)
            {
                foreach (exportedVariable v in root.vars)
                {
                    if (v.type != "UnityEngine.Color" &&
                        v.type != "UnityEngine.Vector2" &&
                        v.type != "UnityEngine.Vector3" &&
                        v.type != "UnityEngine.Rect")

                        node.vars.Add(v);
                    else
                    {
                        BT_Serializer bt = new BT_Serializer();
                        node.vars.Add(bt.ConvertToSerializable(v));
                    }
                }
            }
            if (root.children.Count != 0)
            {
                foreach (node n in root.children)
                    node.children.Add(Convert(n));
            }
            return node;
        }
        // Create editor nodes from saved data. Returns root node of the tree.
        node ConvertBack(expNode root)
        {
            node node = new node(GetNodeType(root.name),
                                 root.name,
                                 GetUniqueId(),
                                 GetUniqueId(),
                                 scrollPosition.x,
                                 scrollPosition.y + controlArea_height,
                                 nodeWidth,
                                 nodeHeight);
            node.notes = root.notes;
            node.isVisible = root.isExpanded;
            nodes.Add(node);
            if (root.vars.Count != 0)
            {
                foreach (exportedVariable n in root.vars)
                {
                    for (int i = 0; i < node.vars.Count; ++i)
                    {
                        if (n.name == node.vars[i].name)
                        {
                            if (n.type != "UnityEngine.Color" &&
                                n.type != "UnityEngine.Vector2" &&
                                n.type != "UnityEngine.Vector3" &&
                                n.type != "UnityEngine.Rect")
                            {
                                node.vars[i].value = n.value;
                            }
                            else
                            {
                                BT_Serializer bt = new BT_Serializer();
                                node.vars[i].value = bt.ConvertToUnityType(n).value;
                            }
                            node.vars[i].type = n.type;
                        }
                    }
                }
            }
            if (root.children.Count != 0)
            {
                foreach (expNode n in root.children)
                    node.children.Add(ConvertBack(n));
            }

            return node;
        }
        #endregion

        #region Helpers for finding and organising files in folders

        // Convert asset guid to path relative to the appropriate main folder. Extension included.
        string GetFullPath(string guid, int pathlength)
        {
            string name = AssetDatabase.GUIDToAssetPath(guid);
            // include the '/' to main path length:
            int length = pathlength + 1;
            name = name.Substring(length, name.Length - length);
            return name;
        }
        // Fairly HACK-like way to find out what we are dealing with here.
        // Check and return extension of the file. If there's no extension,
        // path refers to a folder and return value is null.
        string checkExtension(string fullPath)
        {
            string[] n = fullPath.Split('.');
            if (n.Length == 1)
                return null;
            return n[n.Length - 1];
        }
        // Remove extension from filepath
        string removeExtension(string fullPath)
        {
            string[] n = fullPath.Split('.');
            return n[0];
        }
        // Get asset name without folders:
        string GetNameWithoutFolders(string anySortOfPath)
        {
            string[] n = anySortOfPath.Split('/');
            return (n[n.Length - 1]);
        }
        // Check if given file name represents an existing class that inherits BaseNode
        // (and can be used in BT)
        bool CheckIfNode(string name)
        {
            if (TreeData.getBaseType(GetNameWithoutFolders(name)) == typeof(BaseNode))
                return true;
            return false;
        }
        #endregion

        #region Node creation/destruction functions.

        // Adds new node with chosen type.
        void CreateNode(string name, NODETYPES type)
        {
            nodes.Add(new node(type,
                               GetNameWithoutFolders(name),
                               GetUniqueId(),
                               GetUniqueId(),
                               scrollPosition.x + nodePosX,
                               scrollPosition.y + controlArea_height,
                               nodeWidth,
                               nodeHeight));
        }
        // Adds node at specified position. Used (formerly) in creating node right below creating button.
        void CreateNode(string name, NODETYPES type, float posX, float posY)
        {
            nodes.Add(new node(type,
                               GetNameWithoutFolders(name),
                               GetUniqueId(),
                               GetUniqueId(),
                               scrollPosition.x + posX,
                               scrollPosition.y + posY,
                               nodeWidth,
                               nodeHeight));
        }
        // Flag node for deleting.
        void KillNode(node node)
        {
            node.isDead = true;
            // also kill children, if invisible:
            if (node.children.Count != 0)
            {
                foreach (node n in node.children)
                {
                    if (!n.isVisible)
                        KillNode(n);
                }
            }
        }
        // Remove (dying) node from its parent's children list.
        void KillConnections(node node)
        {
            foreach (node n in nodes)
            {
                if (n.children.Contains(node))
                {

                    n.children.Remove(node);
                    n.spaceNeeded = parentspace(n);
                }
            }
        }
        // TODO: Better system for defining unique ID for everything?
        int GetUniqueId()
        {
            int id = idi;
            idi++;
            return id;
        }
        // Returns index of said node. Returns -1 if node is not found.
        int FindNodeWithId(int id)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].uniqueID == id)
                    return i;
            }

            return -1;
        }
        // Returns index of node with said note ID to link notes to right node.
        // This is necessary because of window drawing.
        int FindNodeWithNoteId(int noteId)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].noteID == noteId)
                    return i;
            }
            return -1;
        }
        // Get node type by name.
        NODETYPES GetNodeType(string name)
        {
            string plain_name = GetNameWithoutFolders(name);
            if (plain_name == "RootNode")
                return NODETYPES.ROOT;
            foreach (string s in internaltypes)
            {
                if (GetNameWithoutFolders(s) == plain_name)
                    return NODETYPES.INTERNAL;
            }
            foreach (string s in actiontypes)
            {
                if (GetNameWithoutFolders(s) == plain_name)
                    return NODETYPES.ACTIONLEAF;
            }
            foreach (string s in conditiontypes)
            {
                if (GetNameWithoutFolders(s) == plain_name)
                    return NODETYPES.CONDITIONLEAF;
            }
            foreach (string s in decoratortypes)
            {
                if (GetNameWithoutFolders(s) == plain_name)
                    return NODETYPES.DECORATOR;
            }
            return NODETYPES.INVALID;  /////// Node name does not exist. Can still be shown in editor.
        }
        // Copy nodes to undo / redo stack
        node[] CopyNodes(List<node> nodelist)
        {
            node[] copy = new node[nodelist.Count];
            for (int i = 0; i < nodelist.Count; ++i)
            {
                copy[i] = new node(nodelist[i]);
            }
            for (int i = 0; i < nodelist.Count; ++i)
            {
                foreach (node h in nodes[i].children)
                    foreach (node r in copy)
                    {
                        if (r.uniqueID == h.uniqueID)
                            copy[i].children.Add(r);
                    }
            }
            //foreach (node n in copy) Debug.Log(n.children.Count);
            return copy;
        }
        // copy nodes to current nodes list
        void CopyBack(node[] nodearray)
        {        //foreach (node n in nodearray) Debug.Log(n.children.Count);
            nodes.Clear();
            foreach (node n in nodearray)
            {
                nodes.Add(new node(n));
            }
            for (int i = 0; i < nodearray.Length; ++i)
            {
                nodes[i].children.Clear();
                foreach (node h in nodearray[i].children)
                    nodes[i].children.Add(nodes[FindNodeWithId(h.uniqueID)]);
            }

        }
        #endregion

        #region BT structure functions

        // Size adjusting for zooming tree view. Call when zoom level is changed.
        void UpdateNodeRects()
        {
            // Buttons that keep size
            AddConnection_button_left = new Rect(0, nodeHeight / 2 - 10, 20, 20);
            AddConnection_button_right = new Rect(nodeWidth - 20, nodeHeight / 2 - 10, 20, 20);
            Close_button = new Rect(nodeWidth - 16, 0, 16, 16);

            // Nodetype icon size depends on LOD level
            if (lod == TREE_LOD.LARGEST)
                Nodetype_Icon = new Rect(nodeWidth / 2 - 23,
                                         nodeHeight / 4,
                                         46,
                                         46);
            else if (lod == TREE_LOD.SMALL || lod == TREE_LOD.MEDIUM)
                Nodetype_Icon = new Rect(nodeWidth / 2 - nodeWidth / 5.2f,
                                         nodeHeight / 4,
                                         nodeHeight - nodeHeight / 4,
                                         nodeHeight - nodeHeight / 4);
            else Nodetype_Icon = new Rect(nodeWidth / 2 - nodeHeight / 2,
                                          0,
                                          nodeHeight,
                                          nodeHeight);

            // Buttons that scale
            ShowNotes_Button = new Rect(0, 0, nodeWidth / 6.5f, nodeHeight / 4);
            Expand_Button = new Rect(nodeWidth - nodeWidth / 6.5f,
                                        nodeHeight - nodeHeight / 4,
                                        nodeWidth / 6.5f,
                                        nodeHeight / 4);
            ShowVars_Button = new Rect(0,
                                        nodeHeight - nodeHeight / 4,
                                        nodeWidth / 3.5f,
                                        nodeHeight / 4);

            // Text labels. NodeTitle also detects mouse to show tooltip.
            Subtree_Label = new Rect(nodeWidth / 3.5f, 3 * nodeHeight / 4, nodeWidth / 1.8f, nodeHeight / 4);
            if (lod != TREE_LOD.TINY)
                NodeTitle_Label = new Rect(nodeWidth / 6.5f,
                                           0,
                                           nodeWidth - (2 * nodeWidth / 6.5f),
                                           17);
            else NodeTitle_Label = new Rect(0, 0, nodeWidth, nodeHeight);
        }

        // Keep track of tree depth. TODO: set warning value, which can be adjusted
        int GetUpdatedTreeDepth(node current, int curLevel)
        {
            if (current.treeLevel < curLevel)
                current.treeLevel = curLevel;
            if (current.children.Count == 0)
                return curLevel;
            else
            {
                int nextlevel = curLevel + 1;
                foreach (node n in current.children)
                {

                    int temp = GetUpdatedTreeDepth(n, curLevel + 1);
                    if (nextlevel < temp)
                        nextlevel = temp;
                }
                return nextlevel;
            }
        }
        // Show/Hide children toggle
        void SetVisibilityOfChildren(node root, bool value)
        {
            foreach (node n in root.children)
            {
                n.isVisible = value;
                SetVisibilityOfChildren(n, value);
            }
        }
        void ConnectNodes(node parent, node child)
        {
            if (parent.children.Contains(child))
            {
                undoStack.Push(CopyNodes(nodes));
                parent.children.Remove(child);
            }
            else if (IsLegalConnection(child, parent))
            {
                undoStack.Push(CopyNodes(nodes));
                parent.children.Add(child);
                parent.spaceNeeded = parentspace(parent);
            }

            foreach (node n in nodes)
                n.treeLevel = 0;
            updatePriority(parent);
            treeDepth = GetUpdatedTreeDepth(FindRoot(), 0);
        }
        bool IsLegalConnection(node child, node proposed_parent)
        {
            // Can't connect to self!
            if (child == proposed_parent)
                return false;
            // Root and decorator are allowed only one child
            if ((proposed_parent.nodetype == NODETYPES.ROOT ||
                 proposed_parent.nodetype == NODETYPES.DECORATOR)
                 && proposed_parent.children.Count > 0)
                return false;
            // To avoid looping descendant can't be parent
            if (CheckChildren(child, proposed_parent))
                return false;
            return true;
        }
        // Returns true if node with given ID exists somewhere in parent's descendants.
        bool CheckChildren(node parent, node nodeToFind)
        {
            if (parent.children.Count > 0)
            {
                // Check immediate
                foreach (node n in parent.children)
                {
                    if (n.uniqueID == nodeToFind.uniqueID)
                        return true;
                }
                // If not found, move to next gen
                for (int i = 0; i < parent.children.Count; i++)
                {
                    node next = parent.children[i];
                    if (CheckChildren(next, nodeToFind))
                        return true;
                }

            }
            return false;
        }

        // Define priority order of children by y coordinate: order children around!
        void updatePriority(node parent)
        {
            for (int j = 0; j < parent.children.Count - 1; j++)
            {
                for (int i = 0; i < parent.children.Count - 1; i++)
                {
                    if (parent.children[i].windowRect.y > parent.children[i + 1].windowRect.y)
                    {
                        node temp = parent.children[i];
                        parent.children[i] = parent.children[i + 1];
                        parent.children[i + 1] = temp;
                    }
                }
            }
        }
        // Check all nodes in editor, detect childless internals
        bool EndsInLeaf()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].children.Count == 0)
                {
                    if (nodes[i].nodetype != NODETYPES.ACTIONLEAF && nodes[i].nodetype != NODETYPES.CONDITIONLEAF)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        // Check that all nodes in editor are connected to root.
        // What this does, however, is check if the amount of root's descendants (plus root) matches node list length.
        // NB! This currently happens every frame and DOES NOT WORK with multiparenting!
        // TODO: Change to use bool var / check each node ID in root descendants (multiparenting).
        bool ConnectsToRoot()
        {
            int counter = 1;
            childCount(FindRoot(), ref counter);
            if (nodes.Count > counter)
                return false;
            return true;
        }
        // Count all descendants of node parent.
        void childCount(node parent, ref int sofar)
        {

            sofar += parent.children.Count;
            for (int i = 0; i < parent.children.Count; i++)
            {
                if (parent.children[i].children.Count != 0)
                    childCount(parent.children[i], ref sofar);
            }
        }
        //Returns root node, null if no root is found. TODO: Error check! All trees must have root.
        node FindRoot()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].nodetype == NODETYPES.ROOT)
                    return nodes[i];
            }
            return null;
        }
        #endregion

        #region Automatic layout functions.

        // Update order and arrange. Call this as a wrapper.
        void SnapToGrid()
        {
            // priority by visual positioning
            foreach (node n in nodes)
            {
                if (n.children.Count > 1)
                {
                    updatePriority(n);
                }
            }
            // snapping
            parentspace(FindRoot());
            AdjustRootPosition();
            HierarchyLayout(FindRoot());
        }

        void HierarchyLayout(node root)
        {
            float curY = 0;
            for (int i = 0; i < root.children.Count; i++)
            {
                root.children[i].windowRect.x = root.children[i].treeLevel * nodeOffsetX;
                // TODO: Find out what the * this actually does. Works, though.
                float height = root.windowRect.y - root.spaceNeeded / 2 + (root.children[i].spaceNeeded) / 2 + curY;
                curY += root.children[i].spaceNeeded;
                root.children[i].windowRect.y = height;

                if (root.children[i].children.Count != 0)
                    HierarchyLayout(root.children[i]);
                // Adjust position so that notes don't hide other nodes. Not very reliable.
                if (root.children[i].showNotes)
                    root.children[i].windowRect.y -= 25;
            }
        }

        float parentspace(node parent)
        {
            float ownOffset = 0;
            foreach (node n in parent.children)
            {
                if (n.isVisible)
                    ownOffset += parentspace(n);
                else ownOffset = 0;
            }
            if (ownOffset == 0)
            {
                ownOffset = nodeOffsetY;
                if (parent.showNotes)
                    ownOffset += 50;
            }
            parent.spaceNeeded = ownOffset;
            return ownOffset;
        }
        void AdjustRootPosition()
        {
            node root = FindRoot();
            root.windowRect.x = 0;
            root.windowRect.y = root.spaceNeeded / 2 + controlArea_height;
        }
    }
    #endregion

    // Stack class to record undo/redo actions.
    // TODO: remove this when overhaul to Unity's own undo system is done.
    public class changeStack<T> : LinkedList<T>
    {
        //    // limit to recorded amount
        private const int maxsize = 10;

        public T Peek()
        {
            return this.Last.Value;
        }

        public T Pop()
        {
            LinkedListNode<T> node = this.Last;

            if (node != null)
            {
                this.RemoveLast();
                return node.Value;
            }
            return default(T);
        }

        public void Push(T value)
        {
            LinkedListNode<T> node = new LinkedListNode<T>(value);
            this.AddLast(node);
            if (this.Count > maxsize)
                this.RemoveFirst();
        }
    }
}