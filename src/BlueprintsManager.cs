using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.JSON;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class BlueprintsManagerModEntries
  {
    public static string AssetsDirectory;
    private static string RelativeTexturesPath;
    private static string RelativeIconsPath;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.blueprints.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      AssetsDirectory = Path.Combine(Path.GetDirectoryName(path), "assets");
      // TODO this is realy hacky (maybe better in future ModAPI)
      RelativeTexturesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "materials", "blocks", "albedo", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(AssetsDirectory, "textures"))).OriginalString;
      RelativeIconsPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "icons", "dummyfile")).MakeRelativeUri(new Uri(MultiPath.Combine(AssetsDirectory, "icons"))).OriginalString;
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      Pipliz.Log.Write(string.Format("Blueprints relative texture path is {0}", RelativeTexturesPath));
      ItemTypesServer.AddTextureMapping("mods.scarabol.blueprints.blueprinttop", new JSONNode()
        .SetAs("albedo", MultiPath.Combine(RelativeTexturesPath, "albedo", "blueprintsTop"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      string iconFilepath = MultiPath.Combine(RelativeIconsPath, "blueprint.png");
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        ItemTypes.AddRawType(blueprintTypename,
          new JSONNode(NodeType.Object)
            .SetAs("onRemoveAudio", "woodDeleteLight")
            .SetAs("onPlaceAudio", "woodPlace")
            .SetAs("icon", iconFilepath)
            .SetAs("sideall", "planks")
            .SetAs("sidey+", "mods.scarabol.blueprints.blueprinttop")
            .SetAs("npcLimit", "0")
            .SetAs("onRemove", new JSONNode(NodeType.Array))
            .SetAs("isRotatable", "true")
            .SetAs("rotatablex+", blueprintTypename + "x+")
            .SetAs("rotatablex-", blueprintTypename + "x-")
            .SetAs("rotatablez+", blueprintTypename + "z+")
            .SetAs("rotatablez-", blueprintTypename + "z-")
        );
        ItemTypes.AddRawType(blueprintTypename + "x+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename + "x-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename + "z+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename + "z-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.blueprints.loadrecipes")]
    [ModLoader.ModCallbackDependsOn("pipliz.apiprovider.registerrecipes")]
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

  public static class BlueprintsManager
  {
    public static string BLUEPRINTS_PREFIX = ConstructionModEntries.MOD_PREFIX + "blueprints.";
    public static Dictionary<string, List<BlueprintBlock>> blueprints = new Dictionary<string, List<BlueprintBlock>>();

    public static void LoadBlueprints(string blueprintsPath)
    {
      Dictionary<string, string> prefixesBlueprints = new Dictionary<string, string>();
      Dictionary<string, string> prefixesClear = new Dictionary<string, string>();
      Dictionary<string, string> prefixesCapsules = new Dictionary<string, string>();
      Dictionary<string, string> prefixesClearCapsules = new Dictionary<string, string>();
      string[] prefixFiles = Directory.GetFiles(Path.Combine(BlueprintsManagerModEntries.AssetsDirectory, "localization"), "prefixes.json", SearchOption.AllDirectories);
      foreach (string filepath in prefixFiles) {
        try {
          JSONNode jsonPrefixes;
          if (Pipliz.JSON.JSON.Deserialize(filepath, out jsonPrefixes, false)) {
            string locName = Directory.GetParent(filepath).Name;
            Pipliz.Log.Write(string.Format("Found prefixes localization file for '{0}' localization", locName));
            string blueprintsPrefix;
            if (jsonPrefixes.TryGetAs<string>("blueprints", out blueprintsPrefix)) {
              prefixesBlueprints[locName] = blueprintsPrefix;
            }
            string clearingPrefix;
            if (jsonPrefixes.TryGetAs<string>("clear", out clearingPrefix)) {
              prefixesClear[locName] = clearingPrefix;
            }
            string capsulesPrefix;
            if (jsonPrefixes.TryGetAs<string>("capsules", out capsulesPrefix)) {
              prefixesCapsules[locName] = capsulesPrefix;
            }
            string capsulesClearPrefix;
            if (jsonPrefixes.TryGetAs<string>("capsules_clear", out capsulesClearPrefix)) {
              prefixesClearCapsules[locName] = capsulesClearPrefix;
            }
          }
        } catch (Exception exception) {
          Pipliz.Log.WriteError(string.Format("Exception reading localization from {0}; {1}", filepath, exception.Message));
        }
      }
      Pipliz.Log.Write(string.Format("Loading blueprints from {0}", blueprintsPath));
      string[] files = Directory.GetFiles(blueprintsPath, "**.json", SearchOption.AllDirectories);
      foreach (string filepath in files) {
        try {
          JSONNode json;
          if (Pipliz.JSON.JSON.Deserialize(filepath, out json, false)) {
            string filename = Path.GetFileName(filepath);
            string blueprintName = Path.GetFileNameWithoutExtension(filepath).Replace(" ", ".").ToLower();
            int offx = 0;
            int offy = 0;
            int offz = 0;
            List<BlueprintBlock> blocks = new List<BlueprintBlock>();
            JSONNode jsonBlocks;
            if (json.NodeType == NodeType.Object) {
              if (!json.TryGetAs<JSONNode>("blocks", out jsonBlocks)) {
                Pipliz.Log.WriteError(string.Format("Expected 'blocks' key in json {0}", filename));
                continue;
              }
              JSONNode jsonOffset;
              if (json.TryGetAs<JSONNode>("offset", out jsonOffset)) {
                offx = -jsonOffset.GetAs<int>("x");
                offy = -jsonOffset.GetAs<int>("y");
                offz = -jsonOffset.GetAs<int>("z");
              }
              JSONNode jsonLocalization;
              if (json.TryGetAs<JSONNode>("localization", out jsonLocalization)) {
                foreach (KeyValuePair<string, JSONNode> locEntry in jsonLocalization.LoopObject()) {
                  string labelPrefix;
                  string capsulePrefix;
                  if (!blueprintName.EndsWith("_clear")) {
                    if (prefixesBlueprints.TryGetValue(locEntry.Key, out labelPrefix)) {
                      labelPrefix = labelPrefix.Trim();
                    } else {
                      labelPrefix = "Blueprint";
                    }
                    if (prefixesCapsules.TryGetValue(locEntry.Key, out capsulePrefix)) {
                      capsulePrefix = capsulePrefix.Trim();
                    } else {
                      capsulePrefix = "Emperor Capsule";
                    }
                  } else {
                    if (prefixesClear.TryGetValue(locEntry.Key, out labelPrefix)) {
                      labelPrefix = labelPrefix.Trim();
                    } else {
                      labelPrefix = "Demolition-Plan";
                    }
                    if (prefixesClearCapsules.TryGetValue(locEntry.Key, out capsulePrefix)) {
                      capsulePrefix = capsulePrefix.Trim();
                    } else {
                      capsulePrefix = "Emperor Demolition Capsule";
                    }
                  }
                  string label = ((string) locEntry.Value.BareObject).Trim();
                  ModLocalizationHelper.localize(locEntry.Key, "types.json", new JSONNode()
                                                 .SetAs(blueprintName, labelPrefix + " " + label)
                                                 .SetAs(blueprintName + CapsulesModEntries.CAPSULE_SUFFIX, capsulePrefix + " " + label)
                                                   , BLUEPRINTS_PREFIX, false);
                }
              }
            } else {
              jsonBlocks = json; // fallback everything is an array
              Pipliz.Log.Write(string.Format("No json object defined in '{0}', using full content as array", filename));
              int maxx = 0, maxy = 0, maxz = 0;
              foreach (JSONNode node in jsonBlocks.LoopArray()) {
                int x = getJSONInt(node, "startx", "x", 0, false);
                if (x < offx) { offx = x; }
                if (x > maxx) { maxx = x; }
                int y = getJSONInt(node, "starty", "y", 0, false);
                if (y < offy) { offy = y; }
                if (y > maxy) { maxy = y; }
                int z = getJSONInt(node, "startz", "z", 0, false);
                if (z < offz) { offz = z; }
                if (z > maxz) { maxz = z; }
              }
              for (int x = 0 ; x <= -offx + maxx ; x++) { // add auto-clear area
                for (int y = 0 ; y <= -offz + maxy ; y++) {
                  for (int z = 0 ; z <= -offz + maxz ; z++) {
                    blocks.Add(new BlueprintBlock(x, y, z, "air"));
                  }
                }
              }
            }
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
              bool isRotatable = false;
              foreach (string xz in new string[] { "x+", "x-", "z+", "z-" }) {
                if (typename.EndsWith(xz)) {
                  isRotatable = true;
                  break;
                }
              }
              if (isRotatable) {
                continue;
              }
              int width = getJSONInt(node, "width", "w", 1, true);
              int height = getJSONInt(node, "height", "h", 1, true);
              int depth = getJSONInt(node, "depth", "d", 1, true);
              int dx = 1, dy = 1, dz = 1;
              if (width < 0) { dx = -1; }
              if (height < 0) { dy = -1; }
              if (depth < 0) { dz = -1; }
              for (int x = startx; x * dx < (startx + width) * dx; x += dx) {
                for (int y = starty; y * dy < (starty + height) * dy; y += dy) {
                  for (int z = startz; z * dz < (startz + depth) * dz; z += dz) {
                    int lx = x - offx, ly = y - offy, lz = z - offz;
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
            BlueprintsManager.blueprints.Add(BLUEPRINTS_PREFIX + blueprintName, blocks);
            Pipliz.Log.Write(string.Format("Added blueprint '{0}' with {1} blocks from {2}", BLUEPRINTS_PREFIX + blueprintName, blocks.Count, filename));
          }
        } catch (Exception exception) {
          Pipliz.Log.WriteError(string.Format("Exception while loading from {0}; {1}", filepath, exception.Message));
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

    public Vector3Int GetWorldPosition(string typeBasename, Vector3Int position, ushort bluetype) {
      ushort hxm = ItemTypes.IndexLookup.GetIndex(typeBasename + "x-");
      ushort hzp = ItemTypes.IndexLookup.GetIndex(typeBasename + "z+");
      ushort hzm = ItemTypes.IndexLookup.GetIndex(typeBasename + "z-");
      int realx = this.offsetz+1;
      int realz = -this.offsetx;
      if (bluetype == hxm) {
        realx = -this.offsetz-1;
        realz = this.offsetx;
      } else if (bluetype == hzp) {
        realx = this.offsetx;
        realz = this.offsetz+1;
      } else if (bluetype == hzm) {
        realx = -this.offsetx;
        realz = -this.offsetz-1;
      }
      return position.Add(realx, this.offsety, realz);
    }
  }
}
