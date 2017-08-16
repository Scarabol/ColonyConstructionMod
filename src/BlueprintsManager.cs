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
    private static string AssetsDirectory;
    private static string RelativeTexturesPath;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.blueprints.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      AssetsDirectory = Path.Combine(Path.GetDirectoryName(path), "assets");
      // TODO this is realy hacky (maybe better in future ModAPI)
      RelativeTexturesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "materials", "blocks", "albedo", "dummyfile")).MakeRelativeUri(new Uri(MultiPath.Combine(Path.GetDirectoryName(path), "assets", "textures"))).OriginalString;
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      Pipliz.Log.Write(string.Format("Blueprints relative texture path is {0}", RelativeTexturesPath));
      ItemTypesServer.AddTextureMapping("blueprintstop", new JSONNode()
        .SetAs("albedo", MultiPath.Combine(RelativeTexturesPath, "albedo", "blueprintsTop"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        ItemTypes.AddRawType(blueprintTypename,
          new JSONNode(NodeType.Object)
            .SetAs("onRemoveAudio", "woodDeleteLight")
            .SetAs("onPlaceAudio", "woodPlace")
            .SetAs("icon", MultiPath.Combine(AssetsDirectory, "icons", "blueprint.png"))
            .SetAs("sideall", "planks")
            .SetAs("sidey+", "blueprintstop")
            .SetAs("npcLimit", "0")
            .SetAs("onRemove", new JSONNode(NodeType.Array))
            .SetAs("isRotatable", "true")
            .SetAs("rotatablex+", blueprintTypename+"x+")
            .SetAs("rotatablex-", blueprintTypename+"x-")
            .SetAs("rotatablez+", blueprintTypename+"z+")
            .SetAs("rotatablez-", blueprintTypename+"z-")
            .SetAs("npcLimit", "0")
        );
        ItemTypes.AddRawType(blueprintTypename+"x+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename+"x-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename+"z+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename+"z-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.blueprints.loadrecipes")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.registerrecipes")]
    public static void AfterItemTypesDefined()
    {
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        RecipePlayer.AllRecipes.Add(new Recipe(new JSONNode()
          .SetAs("results", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", blueprintTypename)))
          .SetAs("requires", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "planks")))
        ));
      }
    }
  }

  public class BlueprintsManager
  {
    public static Dictionary<string, List<BlueprintBlock>> blueprints = new Dictionary<string, List<BlueprintBlock>>();

    public static void LoadBlueprints(string blueprintsPath)
    {
      Pipliz.Log.Write(string.Format("Loading blueprints from {0}", blueprintsPath));
      string[] files = Directory.GetFiles(blueprintsPath, "**.json", SearchOption.AllDirectories);
      foreach (string filepath in files) {
        try {
          JSONNode json;
          if (Pipliz.JSON.JSON.Deserialize(filepath, out json, false)) {
            string blueprintName = null;
            if (json.NodeType == NodeType.Object) {
              json.TryGetAs<string>("name", out blueprintName);
            }
            string filename = Path.GetFileName(filepath);
            if (blueprintName == null || blueprintName.Length < 1) {
              blueprintName = Path.GetFileNameWithoutExtension(filepath).Replace(" ", ".").ToLower();
              Pipliz.Log.Write(string.Format("No name defined in '{0}', using '{1}' extracted from filename", filename, blueprintName));
            }
            blueprintName = ConstructionModEntries.MOD_PREFIX + "blueprints." + blueprintName;
            Pipliz.Log.Write(string.Format("Reading blueprint named '{0}' from '{1}'", blueprintName, filename));
            int offx = 0;
            int offy = 0;
            int offz = 0;
            JSONNode jsonBlocks;
            if (json.NodeType == NodeType.Object) {
              if (!json.TryGetAs<JSONNode>("blocks", out jsonBlocks)) {
                Pipliz.Log.WriteError(string.Format("Expected 'blocks' key in json {0}", filename));
                continue;
              }
              JSONNode jsonOffset;
              if (json.TryGetAs<JSONNode>("offset", out jsonOffset)) {
                offx = jsonOffset.GetAs<int>("x");
                offy = jsonOffset.GetAs<int>("y");
                offz = jsonOffset.GetAs<int>("z");
              }
            } else {
              jsonBlocks = json; // fallback everything is an array
              Pipliz.Log.Write(string.Format("No object defined in '{0}', using full content as array", filename));
              foreach (JSONNode node in jsonBlocks.LoopArray()) {
                int x = getJSONInt(node, "startx", "x", 0, false);
                if (x < -offx) { offx = -x; }
                int y = getJSONInt(node, "starty", "y", 0, false);
                if (y < -offy) { offy = -y; }
                int z = getJSONInt(node, "startz", "z", 0, false);
                if (z < -offz) { offz = -z; }
              }
            }
            List<BlueprintBlock> blocks = new List<BlueprintBlock>();
            BlueprintBlock originBlock = null;
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
                    int lx = x + offx, ly = y + offy, lz = z + offz;
                    BlueprintBlock b = new BlueprintBlock(lx, ly, lz, typename);
                    if (lx == 0 && ly == 0 && lz == -1) { // do not replace the blueprint box itself (yet)
                      originBlock = b;
                    } else {
                      blocks.Add(b);
                    }
                  }
                }
              }
            }
            if (originBlock != null) {
              blocks.Add(originBlock);
            }
            BlueprintsManager.blueprints.Add(blueprintName, blocks);
            Pipliz.Log.Write(string.Format("Added blueprint '{0}' with {1} blocks", blueprintName, blocks.Count));
          }
        } catch (Exception exception) {
          Pipliz.Log.Write(string.Format("Exception while loading from {0}; {1}", filepath, exception.Message));
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
