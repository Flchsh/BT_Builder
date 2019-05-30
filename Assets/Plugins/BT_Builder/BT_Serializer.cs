using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

/* DISCLAIMER: Since we want to use Unity's own data structs and show them correctly in the editor but also
 * serialize them to xml, we need a way to convert them to a serializable form. This may not be pretty.
 * Saved data types, however, are the ones that can be set in the editor and must have editable representations.
 * This system is far from foolproof, 
 * 
 * "UnityEngine.AnimationCurve" is possible to draw in editor (and may be very useful too) and will be added
 * as soon as I figure out how.
 * 
 * Custom PropertyDrawers may be added as soon as the system gets more generalised. So far this is more like a hack.
 * */
namespace BTBuilder
{
    public class BT_Serializer
    {
        string[] types = new string[] { "UnityEngine.Color", "UnityEngine.Vector2", "UnityEngine.Vector3", "UnityEngine.Rect" };
        public exportedVariable ConvertToSerializable(exportedVariable var)
        {
            exportedVariable ex = new exportedVariable();
            ex.name = var.name;
            ex.type = var.type;
            int index = GetTypeIndex(var.type);
            ex.wvalue = ConvertToSerial(index, var.value);
            return ex;
        }
        public exportedVariable ConvertToUnityType(exportedVariable var)
        {
            exportedVariable ex = new exportedVariable();
            ex.name = var.name;
            ex.type = var.type;
            int index = GetTypeIndex(var.type);
            ex.value = ConvertToUnity(index, var.wvalue);
            return ex;
        }
        int GetTypeIndex(string typeName)
        {
            for (int i = 0; i < types.Length; ++i)
            {
                if (typeName == types[i])
                    return i;
            }
            return -1;
        }
        valueWrapper ConvertToSerial(int index, object value)
        {
            valueWrapper wrap = new valueWrapper();
            switch (index)
            {
                case (0):
                    {
                        Color c = (Color)value;
                        wrap.value = new object[4] { c.r, c.g, c.b, c.a };
                        return wrap;
                    }
                case (1):
                    {
                        Vector2 v2 = (Vector2)value;
                        wrap.value = new object[2] { v2.x, v2.y };
                        return wrap;
                    }
                case (2):
                    {
                        Vector3 v3 = (Vector3)value;
                        wrap.value = new object[3] { v3.x, v3.y, v3.z };
                        return wrap;
                    }
                case (3):
                    {
                        Rect r = (Rect)value;
                        wrap.value = new object[4] { r.x, r.y, r.width, r.height };
                        return wrap;
                    }
                default:
                    return null;
            }
        }
        object ConvertToUnity(int index, valueWrapper val)
        {
            switch (index)
            {
                case (0):
                    {
                        Color c = new Color((float)val.value[0], (float)val.value[1], (float)val.value[2], (float)val.value[3]);
                        return (object)c;
                    }
                case (1):
                    {
                        Vector2 v = new Vector2((float)val.value[0], (float)val.value[1]);
                        return (object)v;
                    }
                case (2):
                    {
                        Vector3 v3 = new Vector3((float)val.value[0], (float)val.value[1], (float)val.value[2]);
                        return (object)v3;
                    }
                case (3):
                    {
                        Rect r = new Rect((float)val.value[0], (float)val.value[1], (float)val.value[2], (float)val.value[3]);
                        return (object)r;
                    }
                default:
                    return null;
            }
        }
    }

    [XmlRoot("BehaviorTree")]
    public class TreeData
    {
        public expNode RootNode;
        public static readonly string package_path = "Plugins/BT_Builder"; // Change this to change package part of file paths in BT tool.

        // Workaround for finding class types that only exist in the main project.
        // System.Type only searches the caller project, so editor classes cannot find main project classes directly.
        public static System.Type getType(string name)
        {
            return System.Type.GetType(name);
        }
        // As above.
        public static System.Type getBaseType(string name)
        {
            System.Type typ = System.Type.GetType(name);
            if (typ != null)
                return typ.BaseType;
            return null;
        }
        // saves expNodes to file at the path defined as param.
        // TODO: define path settings in some consistent way for easy usage. Name of the tree is user-defined.
        public void Save(string path)
        {
            //FileStream fs = new FileStream(path, FileMode.Create);
            //TextWriter str = new StreamWriter(fs, System.Text.Encoding.GetEncoding("UTF8"));
            //((TextWriter stream = new StreamWriter(path, false, System.Text.Encoding.GetEncoding("UTF8"));
            var seri = new XmlSerializer(typeof(TreeData));
            using (TextWriter stream = new StreamWriter(path, false, System.Text.Encoding.GetEncoding("UTF-8")))
            {
                seri.Serialize(stream, this);
            }
        }

        // Get expNodes from existing xml file.
        public static TreeData Load(string path)
        {
            var seri = new XmlSerializer(typeof(TreeData));
            using (var stream = new FileStream(path, FileMode.Open))
            {
                return seri.Deserialize(stream) as TreeData;
            }
        }
        // Read file to find out if it is a TreeData file. Clumsy way of doing this, but what the *, it works.
        // This defines which files are shown in the editor's behavior file dropdown menu.
        // Has to be separated from loading, since only valid files should be shown in loading menu.
        public static bool isValidBehavior(string path)
        {
            var seri = new XmlSerializer(typeof(TreeData));
            using (var stream = new FileStream(path, FileMode.Open))
            {
                XmlReader rd = XmlReader.Create(stream);
                if (seri.CanDeserialize(rd))
                    return true;
            }
            return false;
        }
        // This is used in the BTree class to construct the tree.
        public static expNode LoadToTreeComp(TextAsset treefile)
        {
            TreeData data;
            var seri = new XmlSerializer(typeof(TreeData));

            using (var stream = new MemoryStream(treefile.bytes))
            {
                var str = new StreamReader(stream, System.Text.Encoding.GetEncoding("UTF-8"));
                data = seri.Deserialize(str) as TreeData;
            }
            return data.RootNode;
        }
    }
    // Data class for exported node info (only name of the derived node class needs to be known, to define tree structure)
    public class expNode
    {
        [XmlAttribute("name")]
        public string name;
        [XmlAttribute("notes")]
        public string notes;
        [XmlAttribute("expanded")]
        public bool isExpanded;
        [XmlArray("vars")]
        public List<exportedVariable> vars = new List<exportedVariable>(); // node specific variables. This must come from class definition! Do we need this? Yes! Also casting!
        public List<expNode> children = new List<expNode>();
    }
    public class exportedVariable
    {
        public string name;
        public string type;
        public object value;
        public valueWrapper wvalue;
    }

    // A wrapper class for all non-primitive type values to be saved in XML.
    // NB: Each type has to be implemented explicitly. Only types that can be shown in Unity Editor are relevant?
    // Custom PropertyDrawers are currently not supported.
    public class valueWrapper
    {
        [XmlArray("valuestructure")]
        public object[] value;
    }
}
