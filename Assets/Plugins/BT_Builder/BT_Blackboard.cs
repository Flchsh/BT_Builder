using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/* A Blackboard instance exists on the BT_Behave component. All data on blackboard is
 * specific to agent (tree specific are on tree component and node specific on nodes).
 * Updating the blackboard (other than from nodes) can be done anywhere, and anything
 * can read the blackboard.
 * 
 * Items are stored in a dictionary, with variable name as key. To catch spelling errors
 * and inconsistent placing of variables, all contents of the blackboard are shown in
 * inspector when the BT_Behave component is expanded. Colors saved in the type specific
 * color container are also displayed as a colored area, along with numeric value.
 * 
 * To avoid boxing as much as possible, typed dictionaries can be used for most common
 * types of data. Boxed (object) dictionary can be used for types that have no specific
 * container.
 * 
 * Each container has its own set-, get- and key exists -methods.
 * Typed methods have type name added to the method name (e.g. SetInt).
 */
namespace BTBuilder
{
    public class BT_Blackboard
    {
        // You CAN use this for everything, if boxing cost is acceptable:
        Dictionary<string, object> memory = new Dictionary<string, object>();

        // But you can use these for the most common types when you need better performance.
        Dictionary<string, int> intMemory = new Dictionary<string, int>();
        Dictionary<string, float> floatMemory = new Dictionary<string, float>();
        Dictionary<string, string> stringMemory = new Dictionary<string, string>();
        Dictionary<string, double> doubleMemory = new Dictionary<string, double>();
        Dictionary<string, bool> boolMemory = new Dictionary<string, bool>();
        Dictionary<string, Vector2> vector2Memory = new Dictionary<string, Vector2>();
        Dictionary<string, Vector3> vector3Memory = new Dictionary<string, Vector3>();
        Dictionary<string, Color> colorMemory = new Dictionary<string, Color>();
        Dictionary<string, Quaternion> quatMemory = new Dictionary<string, Quaternion>();

        // This is for drawing the custom inspector view.
        public void DrawContents()
        {
            ReadGeneral();
            ReadInts();
            ReadFloats();
            ReadBools();
            ReadDoubles();
            ReadStrings();
            ReadVector2s();
            ReadVector3s();
            ReadQuaternions();
            ReadColors();
        }

        // Dictionary accessors:

        /// <summary>
        /// generic dictionary
        /// </summary> 
        public void Set<T>(T value, string variableName)
        {
            if (!memory.ContainsKey(variableName))
            {
                memory.Add(variableName, value);
            }
            else memory[variableName] = value;
        }
        public T Get<T>(string variableName)
        {
            try { return (T)memory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");

            }
            return default(T);
        }
        public bool KeyExists(string variableName)
        {
            if (memory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadGeneral()
        {
            foreach (KeyValuePair<string, object> item in memory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key);
                if (item.Value != null)
                    GUILayout.Label(item.Value.ToString());
                else GUILayout.Label("-no value-");
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// integer handling
        /// </summary>
        public void SetInt(int value, string varname)
        {
            if (!intMemory.ContainsKey(varname)) { intMemory.Add(varname, value); }
            else intMemory[varname] = value;
        }
        public int GetInt(string variableName)
        {
            try { return intMemory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return 0;
        }
        public bool KeyExistsInt(string variableName)
        {
            if (intMemory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadInts()
        {
            foreach (KeyValuePair<string, int> item in intMemory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key + " (int)");
                GUILayout.Label(item.Value.ToString());
                GUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// float handling
        /// </summary>
        public void SetFloat(float value, string varname)
        {
            if (!floatMemory.ContainsKey(varname)) { floatMemory.Add(varname, value); }
            else floatMemory[varname] = value;
        }
        public float GetFloat(string variableName)
        {
            try { return floatMemory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return 0;
        }
        public bool KeyExistsFloat(string variableName)
        {
            if (floatMemory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadFloats()
        {
            foreach (KeyValuePair<string, float> item in floatMemory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key + " (float)");
                GUILayout.Label(item.Value.ToString());
                GUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// string handling
        /// </summary>
        public void SetString(string value, string varname)
        {
            if (!stringMemory.ContainsKey(varname)) { stringMemory.Add(varname, value); }
            else stringMemory[varname] = value;
        }
        public string GetString(string variableName)
        {
            try { return stringMemory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return null;
        }
        public bool KeyExistsString(string variableName)
        {
            if (stringMemory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadStrings()
        {
            foreach (KeyValuePair<string, string> item in stringMemory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key + " (string)");
                if (item.Value != null)
                    GUILayout.Label(item.Value);
                else GUILayout.Label("-no value-");
                GUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// double handling
        /// </summary>
        public void SetDouble(double value, string varname)
        {
            if (!doubleMemory.ContainsKey(varname)) { doubleMemory.Add(varname, value); }
            else doubleMemory[varname] = value;
        }
        public double GetDouble(string variableName)
        {
            try { return doubleMemory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return 0;
        }
        public bool KeyExistsDouble(string variableName)
        {
            if (doubleMemory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadDoubles()
        {
            foreach (KeyValuePair<string, double> item in doubleMemory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key + " (double)");
                GUILayout.Label(item.Value.ToString());
                GUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// bool handling
        /// </summary>
        public void SetBool(bool value, string varname)
        {
            if (!boolMemory.ContainsKey(varname)) { boolMemory.Add(varname, value); }
            else boolMemory[varname] = value;
        }
        public bool GetBool(string variableName)
        {
            try { return boolMemory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return false;
        }
        public bool KeyExistsBool(string variableName)
        {
            if (boolMemory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadBools()
        {
            foreach (KeyValuePair<string, bool> item in boolMemory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key + " (bool)");
                GUILayout.Label(item.Value.ToString());
                GUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// Vector2 handling
        /// </summary>
        public void SetVector2(Vector2 value, string varname)
        {
            if (!vector2Memory.ContainsKey(varname)) { vector2Memory.Add(varname, value); }
            else vector2Memory[varname] = value;
        }
        public Vector2 GetVector2(string variableName)
        {
            try { return vector2Memory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return default(Vector2);
        }
        public bool KeyExistsVector2(string variableName)
        {
            if (vector2Memory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadVector2s()
        {
            foreach (KeyValuePair<string, Vector2> item in vector2Memory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key + " (Vector2)");
                GUILayout.Label(item.Value.ToString());
                GUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// Vector3 handling
        /// </summary>
        public void SetVector3(Vector3 value, string varname)
        {
            if (!vector3Memory.ContainsKey(varname)) { vector3Memory.Add(varname, value); }
            else vector3Memory[varname] = value;
        }
        public Vector3 GetVector3(string variableName)
        {
            try { return vector3Memory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return default(Vector3);
        }
        public bool KeyExistsVector3(string variableName)
        {
            if (vector3Memory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadVector3s()
        {

            foreach (KeyValuePair<string, Vector3> item in vector3Memory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key + " (Vector3)");
                GUILayout.Label(item.Value.ToString());
                GUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// Quaternion handling
        /// </summary>
        public void SetQuaternion(Quaternion value, string varname)
        {
            if (!quatMemory.ContainsKey(varname)) { quatMemory.Add(varname, value); }
            else quatMemory[varname] = value;
        }
        public Quaternion GetQuaternion(string variableName)
        {
            try { return quatMemory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return default(Quaternion);
        }
        public bool KeyExistsQuaternion(string variableName)
        {
            if (quatMemory.ContainsKey(variableName))
                return true;
            return false;
        }
        private void ReadQuaternions()
        {
            foreach (KeyValuePair<string, Quaternion> item in quatMemory)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key + " (Quaternion)");
                GUILayout.Label(item.Value.ToString());
                GUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// Color handling. Name? May be misleading.
        /// </summary>
        public void SetColor(Color value, string varname)
        {
            if (!colorMemory.ContainsKey(varname)) { colorMemory.Add(varname, value); }
            else colorMemory[varname] = value;
        }
        public Color GetColor(string variableName)
        {
            try { return colorMemory[variableName]; }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError("Variable " + variableName + " not found in blackboard");
            }
            return default(Color);
        }
        public bool KeyExistsColor(string variableName)
        {
            if (colorMemory.ContainsKey(variableName))
                return true;
            return false;
        }
        // Saving colors on blackboard may not be very common. Let's show them on Inspector anyway.
        // Initialise variables needed for creating a colored box: this is called when BT_Behave
        // component is enabled.
        public void color_initer()
        {
            tex = new Texture2D(1, 1);
            style = new GUIStyle();
        }
        Texture2D tex;
        GUIStyle style;
        private void ReadColors()
        {
            foreach (KeyValuePair<string, Color> item in colorMemory)
            {
                tex.SetPixel(0, 0, item.Value);
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.Apply();
                style.normal.background = tex;
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.Key);
                GUILayout.Label("", style, GUILayout.MinWidth(20));
                GUILayout.Label(item.Value.ToString());
                GUILayout.EndHorizontal();
            }
        }
    }
}