using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Pipliz;
using Pipliz.JSON;
using Server.Localization;

namespace ScarabolMods
{

    public static class ModLocalizationHelper
    {
        public static void Localize(string localePath, string typesprefix)
        {
            try
            {
                string[] files = Directory.GetFiles(localePath, "translation.json", SearchOption.AllDirectories);
                foreach(string filepath in files)
                {
                    try
                    {
                        JSONNode jsonFromMod;
                        if(JSON.Deserialize(filepath, out jsonFromMod, false))
                        {
                            string locName = Directory.GetParent(filepath).Name;
                            Localize(locName, jsonFromMod, typesprefix);
                        }
                    }
                    catch(Exception exception)
                    {
                        Log.WriteError($"Exception reading localization from {filepath}; {exception.Message}");
                    }
                }
            }
            catch(DirectoryNotFoundException)
            {
                Log.WriteError($"Localization directory not found at {localePath}");
            }
        }

        // Thanks to ZUN for the help with the localization
        public static void Localize(string locName, JSONNode jsonFromMod, string typesprefix)
        {
            try
            {
                if(jsonFromMod.HasChild("types"))
                {
                    JSONNode arr = jsonFromMod["types"];
                    JSONNode result = new JSONNode();
                    foreach(var pair in arr.LoopObject())
                    {
                        result[typesprefix + pair.Key] = pair.Value;
                    }
                    jsonFromMod["types"] = result;
                }
                if(jsonFromMod.HasChild("typeuses"))
                {
                    JSONNode arr = jsonFromMod["typeuses"];
                    JSONNode result = new JSONNode();
                    foreach(var pair in arr.LoopObject())
                    {
                        result[typesprefix + pair.Key] = pair.Value;
                    }
                    jsonFromMod["typeuses"] = result;
                }
                Localization.QueueLocalePatch(new Localization.LocalePatch(0, jsonFromMod, locName));
            }
            catch(Exception exception)
            {
                Log.WriteError($"Exception while localization of {locName}; {exception.Message}");
            }
        }

        class NodePair
        {
            public string Parent { get; private set; }

            public JSONNode First { get; private set; }

            public JSONNode Second { get; private set; }

            public NodePair(string parent, JSONNode first, JSONNode second)
            {
                Parent = parent;
                First = first;
                Second = second;
            }
        }
    }

    public static class MultiPath
    {
        public static string Combine(params string[] pathParts)
        {
            StringBuilder result = new StringBuilder();
            foreach(string part in pathParts)
            {
                result.Append(part.TrimEnd('/', '\\')).Append(Path.DirectorySeparatorChar);
            }
            return result.ToString().TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    public static class TypeHelper
    {
        public static string RotatableToBasetype(string typename)
        {
            if(typename.EndsWith("x+") || typename.EndsWith("x-") || typename.EndsWith("z+") || typename.EndsWith("z-"))
            {
                return typename.Substring(0, typename.Length - 2);
            }
            return typename;
        }

        public static string GetXZFromTypename(string typename)
        {
            if(typename.EndsWith("x+") || typename.EndsWith("x-") || typename.EndsWith("z+") || typename.EndsWith("z-"))
            {
                return typename.Substring(typename.Length - 2);
            }
            return "";
        }

        public static Vector3Int RotatableToVector(string typename)
        {
            string xz = GetXZFromTypename(typename);
            if(xz.Equals("x+"))
            {
                return new Vector3Int(1, 0, 0);
            }
            if(xz.Equals("x-"))
            {
                return new Vector3Int(-1, 0, 0);
            }
            if(xz.Equals("y+"))
            {
                return new Vector3Int(0, 1, 0);
            }
            if(xz.Equals("y-"))
            {
                return new Vector3Int(0, -1, 0);
            }
            if(xz.Equals("z+"))
            {
                return new Vector3Int(0, 0, 1);
            }
            if(xz.Equals("z-"))
            {
                return new Vector3Int(0, 0, -1);
            }
            return new Vector3Int(0, 0, 0);
        }

        public static string VectorToXZ(Vector3Int vec)
        {
            if(vec.x == 1)
            {
                return "x+";
            }
            if(vec.x == -1)
            {
                return "x-";
            }
            if(vec.y == 1)
            {
                return "y+";
            }
            if(vec.y == -1)
            {
                return "y-";
            }
            if(vec.z == 1)
            {
                return "z+";
            }
            if(vec.z == -1)
            {
                return "z-";
            }
            Log.WriteError($"Malformed vector {vec}");
            return "x+";
        }
    }

    public static class JsonNodeExtension
    {
        public static T getAsOrElse<T>(this JSONNode node, string identifier, string otherIdentifier)
        {
            if(!node.TryGetAs(identifier, out T result))
            {
                result = node.GetAs<T>(otherIdentifier);
            }
            return result;
        }
    }
}
