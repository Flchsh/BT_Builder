using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

/* Separate editor for editing variable values in class. All editable variables have to be properties;
 * simple fields cannot be detected in classes. Only variables of types that Unity can show as inspector fields
 * can be edited with this.
 * */
namespace BTBuilder
{
    public class ValueEditor : EditorWindow
    {

        string[] types = new string[] { "System.Int32",
                                    "System.Single",
                                    "System.String",
                                    "System.Boolean",
                                    "UnityEngine.Vector3",
                                    "UnityEngine.Vector2" };

        Rect varName_Field = new Rect(0, 0, 120, 20);
        Rect varValue_Field = new Rect(120, 0, 200, 20);

        List<exportedVariable> vars = new List<exportedVariable>();
        public static void Init(ref List<exportedVariable> varlist, string nodeName)
        {
            ValueEditor t = (ValueEditor)EditorWindow.GetWindow(typeof(ValueEditor), true, nodeName);
            t.Show();
            t.vars = varlist;
        }
        // Draw value editor here
        void OnGUI()
        {
            for (int i = 0; i < vars.Count; ++i)
            {
                // Show only specified data types
                int typeIndex = GetTypeIndex(vars[i].type);
                // Semiautomatic layout:
                if (typeIndex >= 0)
                {
                    Rect nextname = new Rect(varName_Field.x,
                                             varName_Field.y + (i * 30),
                                             varName_Field.width,
                                             varName_Field.height);
                    Rect nextvalue = new Rect(varValue_Field.x,
                                              varValue_Field.y + (i * 30),
                                              varValue_Field.width,
                                              varValue_Field.height);
                    SetField(typeIndex, ref vars[i].value, nextvalue);
                    GUI.Label(nextname, vars[i].name);
                }
            }
        }
        // closes when clicks elsewhere.
        void OnLostFocus()
        {
            this.Close();
        }
        // Data type disambiguation: convert type name to listed index for correct editing field
        int GetTypeIndex(string typeName)
        {
            for (int i = 0; i < types.Length; ++i)
            {
                if (typeName == types[i])
                    return i;
            }
            return -1;
        }

        // Helper:
        // Extract variable info here (type, name, current value) 
        void SetField(int type, ref object val, Rect place)
        {
            switch (type)
            {
                case (0):
                    {
                        val = EditorGUI.IntField(place, (int)val);
                        break;
                    }
                case (1):
                    {
                        val = EditorGUI.FloatField(place, (float)val);
                        break;
                    }
                case (2):
                    {
                        val = EditorGUI.TextField(place, (string)val);
                        break;
                    }
                case (3):
                    {
                        val = EditorGUI.Toggle(place, (bool)val);
                        break;
                    }
                case (4):
                    {
                        val = EditorGUI.Vector3Field(place, "", (Vector3)val);
                        break;
                    }
                case (5):
                    {
                        val = EditorGUI.Vector2Field(place, "", (Vector2)val);
                        break;
                    }
                default:
                    {

                    }
                    break;
            }
        }
    }
}