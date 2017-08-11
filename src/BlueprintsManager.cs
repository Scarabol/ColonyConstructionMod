using System;
using System.IO;
using System.Collections.Generic;
using Pipliz.JSON;
using Pipliz.APIProvider.Jobs;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class BlueprintsManagerModEntries
  {
    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      ItemTypesServer.AddTextureMapping("blueprintstop", new JSONNode()
        .SetAs("albedo", "blueprintsTop")
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      foreach (string key in BlueprintsManager.blueprints.Keys) {
        ItemTypes.AddRawType(key,
          new JSONNode(NodeType.Object)
            .SetAs("sideall", "planks")
            .SetAs("sidey+", "blueprintstop")
            .SetAs("isRotatable", "true")
            .SetAs("rotatablex+", key+"x+")
            .SetAs("rotatablex-", key+"x-")
            .SetAs("rotatablez+", key+"z+")
            .SetAs("rotatablez-", key+"z-")
            .SetAs("npcLimit", "0")
        );
        ItemTypes.AddRawType(key+"x+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", key)
        );
        ItemTypes.AddRawType(key+"x-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", key)
        );
        ItemTypes.AddRawType(key+"z+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", key)
        );
        ItemTypes.AddRawType(key+"z-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", key)
        );
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.blueprints.loadrecipes")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.registerrecipes")]
    public static void AfterItemTypesDefined()
    {
      foreach (string key in BlueprintsManager.blueprints.Keys) {
        RecipePlayer.AllRecipes.Add(new Recipe(new JSONNode()
          .SetAs("results", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", key)))
          .SetAs("requires", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "planks")))
        ));
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterDefiningNPCTypes, "scarabol.blueprints.registerjobs")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
    public static void AfterDefiningNPCTypes()
    {
      foreach (string key in BlueprintsManager.blueprints.Keys) {
        BlockJobManagerTracker.Register<ConstructionJob>(key);
      }
    }
  }

  public class BlueprintsManager
  {
    public static Dictionary<string, List<BlueprintBlock>> blueprints = new Dictionary<string, List<BlueprintBlock>>();

    public static void LoadBlueprints(string blueprintsDirectory)
    {
      string[] files = Directory.GetFiles(blueprintsDirectory, "**.json", SearchOption.AllDirectories);
      foreach (string relPathFilename in files) {
        JSONNode json;
        if (Pipliz.JSON.JSON.Deserialize(relPathFilename, out json, false)) {
          if (json != null) {
            try {
              string name;
              json.TryGetAs<string>("name", out name);
              if (name == null || name.Length < 1) {
                string filename  = Path.GetFileNameWithoutExtension(relPathFilename);
                name = filename.Replace(" ", "").ToLower();
                Pipliz.Log.Write(string.Format("No name defined using part {0} as fallback from relPathFilename {1}", name, filename));
              }
              List<BlueprintBlock> blocks = new List<BlueprintBlock>();
              Pipliz.Log.Write(string.Format("Reading blueprint named {0} from {1}", name, relPathFilename));
              JSONNode jsonBlocks;
              json.TryGetAs<JSONNode>("blocks", out jsonBlocks);
              if (jsonBlocks == null) {
                jsonBlocks = json; // fallback everything is an array
                Pipliz.Log.Write(string.Format("No blocks defined using full content as array"));
              }
              foreach (JSONNode node in jsonBlocks.LoopArray()) {
                int startx = getJSONInt(node, "startx", "x", 0, false);
                int starty = getJSONInt(node, "starty", "y", 0, false);
                int startz = getJSONInt(node, "startz", "z", 0, false);
                string typename;
                try {
                  typename = node["typename"].GetAs<string>();
                } catch (Exception) {
                  try {
                    typename = node["t"].GetAs<string>();
                  } catch (Exception) {
                    throw new Exception(string.Format("typename not defined or not a string"));
                  }
                }
                int width = getJSONInt(node, "width", "w", 1, true);
                int height = getJSONInt(node, "height", "h", 1, true);
                int depth = getJSONInt(node, "depth", "d", 1, true);
                for (int x = startx; x < startx + width; x++) {
                  for (int y = starty; y < starty + height; y++) {
                    for (int z = startz; z < startz + depth; z++) {
                      blocks.Add(new BlueprintBlock(x, y, z, typename));
                    }
                  }
                }
              }
              BlueprintsManager.blueprints.Add(name, blocks);
            } catch (Exception exception) {
              Pipliz.Log.Write(string.Format("Exception loading from {0}; {1}", relPathFilename, exception.Message));
            }
          }
        }
      }
    }

    private static int getJSONInt(JSONNode node, string name, string alternativeName, int defaultValue, bool optional)
    {
      try {
        return node[name].GetAs<int>();
      } catch (Exception) {
        try {
          return node[alternativeName].GetAs<int>();
        } catch (Exception) {
          if (optional) {
            return defaultValue;
          } else {
            throw new Exception(string.Format("Neither {0} nor {1} defined or not an integer", name, alternativeName));
          }
        }
      }
    }
  }

  public class BlueprintBlock
  {
    public int offsetx;
    public int offsety;
    public int offsetz;
    public string typename;

    public BlueprintBlock(int offsetx, int offsety, int offsetz, string typename) {
      this.offsetx = offsetx;
      this.offsety = offsety;
      this.offsetz = offsetz;
      this.typename = typename;
    }

    public BlueprintBlock(JSONNode node)
      : this(node.GetAs<int>("offsetx"), node.GetAs<int>("offsety"), node.GetAs<int>("offsetz"), node.GetAs<string>("typename")) {
    }

    public JSONNode GetJSON() {
      return new JSONNode()
        .SetAs("offsetx", offsetx)
        .SetAs("offsety", offsety)
        .SetAs("offsetz", offsetz)
        .SetAs("typename", typename);
    }
  }
}
