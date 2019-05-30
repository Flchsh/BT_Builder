using UnityEngine;
using UnityEditor;
using System.Collections;

namespace BTBuilder
{
    public class DialogEditor : EditorWindow
    {

        string filename;
        string savepath;
        int folder;
        string[] folderList;
        BTeditor ed;

        bool emptyFilename = false;
        public static void Init(string filename, BTeditor editor, ref string[] subfolders, int currentFolder)
        {
            DialogEditor t = (DialogEditor)ScriptableObject.CreateInstance("DialogEditor");
            t.ShowUtility();
            t.filename = filename;
            t.folderList = subfolders;
            t.ed = editor;
            t.folder = currentFolder;
            t.titleContent = new GUIContent("Save file...");
        }
        void OnGUI()
        {
            if (emptyFilename)
                GUILayout.Label("File name cannot be empty! ");
            filename = EditorGUILayout.TextField("File name: ", filename);
            folder = EditorGUILayout.Popup("Folder: ", folder, folderList);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                if (filename != null && filename != "")
                {
                    savepath = folderList[folder] + "/" + filename;
                    ed.SaveFile(savepath);
                }
                else emptyFilename = true;
            }
            if (GUILayout.Button("Cancel"))
                this.Close();
            GUILayout.EndHorizontal();
        }
        void OnLostFocus()
        {
            this.Close();
        }
    }
}
