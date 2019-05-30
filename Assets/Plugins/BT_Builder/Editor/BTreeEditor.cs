using UnityEngine;
using UnityEditor;
using System.Collections;


namespace BTBuilder
{
    [CustomEditor(typeof(BTree))]
    public class BTreeEditor : Editor
    {

        // Save window generation data when first opened in debugger? (Saves time when switching windows, no extra work when not needed)
        BT_DebugWindow d;
        GUIContent d_name;                              // Use file name as title of the window.
        Rect last_size = new Rect(100, 100, 400, 400);     // default size of the debug window. Resets to this when exiting playmode.
        BTree tree;
        void OnEnable()
        {
            tree = (BTree)target;
        }
        public override void OnInspectorGUI()
        {
            //DrawDefaultInspector();     // We don't draw default inspector. It has script object field which is useless.
            // Custom active-toggle:

            // Custom file picking field:
            EditorGUI.BeginChangeCheck();
            bool tempactive = EditorGUILayout.ToggleLeft("enabled", tree.activated);
            if (EditorGUI.EndChangeCheck())
            {
                // Toggle undo message also:
                string message;
                if (tempactive)
                    message = "Enable behavior tree";
                else message = "Disable behavior tree";

                Undo.RecordObject(tree, message);
                tree.activated = tempactive;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("BT File: ");
            EditorGUI.BeginChangeCheck();
            TextAsset tempfile = (EditorGUILayout.ObjectField(tree.file, typeof(TextAsset), false) as TextAsset);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tree, "Assign behavior file");
                tree.file = tempfile;
            }
            GUILayout.EndHorizontal();

            // Tree editing button. Can be used to add an existing file, create new file, edit default values...
            // Not enabled in play mode; reason being, it has no effect whatsoever on running game. Saves confusion.
            GUI.enabled = !Application.isPlaying;
            if (GUILayout.Button("Edit tree file"))
            {
                BTeditor e = (BTeditor)EditorWindow.GetWindow(typeof(BTeditor));
                e.InitBT(tree);
                if (tree.file != null)
                {
                    string filePath = AssetDatabase.GetAssetPath(tree.file);
                    filePath = getpathtosubfolder(filePath);
                    e.OpenTreeForEditing(filePath, false);
                }
            }
            GUILayout.BeginHorizontal();
            // Debugger button. Multiple trees can have their debug windows open simultaneously. Works as a toggle.
            GUI.enabled = Application.isPlaying;
            if (d == null)
            {
                if (GUILayout.Button("Show Debugger"))
                {
                    d = (BT_DebugWindow)EditorWindow.CreateInstance(typeof(BT_DebugWindow));
                    d_name = new GUIContent(tree.file.name, tree.gameObject.name);
                    d.titleContent = d_name;
                    d.Show();
                    d.InitDebugView(tree, false, last_size);
                    tree.debugger = d;
                }
            }
            else
            {
                if (GUILayout.Button("Hide Debugger"))
                {
                    last_size = d.position;
                    d.Close();
                    tree.debugger = null;
                }
            }
            // Show last traversal info if error occurs. Only works if still in play mode!
            // TODO: A serious saving system to keep track of the situation. Do only if really necessary.
            if (tree.erroredOut)
            {
                GUI.enabled = true;
                if (GUILayout.Button("Show last run"))
                {
                    if (d != null)
                    {
                        d.updateData();
                    }
                    else
                    {
                        d = (BT_DebugWindow)EditorWindow.CreateInstance(typeof(BT_DebugWindow));
                        d_name = new GUIContent(tree.file.name, tree.gameObject.name);
                        d.titleContent = d_name;
                        d.Show();
                        d.InitDebugView(tree, false, last_size);
                        tree.debugger = d;
                        d.updateData();
                    }
                }
            }
            GUILayout.EndHorizontal();
        }
        // Helper for extracting right portion of asset path. So much HACK, thank you.
        string getpathtosubfolder(string full)
        {
            full = full.Split('.')[0];
            return full.Substring(("Assets/" + TreeData.package_path + "/BT_Files/").Length);
        }
    }
}