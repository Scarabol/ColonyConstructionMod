using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;
using Pipliz.Threading;
using Permissions;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class CapsulesModEntries
  {
    public static string CAPSULE_SUFFIX = ".capsule";
    public static string ModDirectory;
    private static string RelativeIconsPath;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.capsules.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      ModDirectory = Path.GetDirectoryName(path);
      // TODO this is realy hacky (maybe better in future ModAPI)
      RelativeIconsPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "icons", "dummyfile")).MakeRelativeUri(new Uri(MultiPath.Combine(ModDirectory, "assets", "icons"))).OriginalString;
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.capsules.addrawtypes")]
    [ModLoader.ModCallbackDependsOn("scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      string iconFilepath = Path.Combine(RelativeIconsPath, "capsule.png");
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX,
          new JSONNode(NodeType.Object)
            .SetAs("onPlaceAudio", "woodPlace")
            .SetAs("icon", iconFilepath)
            .SetAs("sideall", "planks")
            .SetAs("sidey+", "mods.scarabol.blueprints.blueprinttop")
            .SetAs("isSolid", "false")
            .SetAs("onRemove", new JSONNode(NodeType.Array))
            .SetAs("isRotatable", "true")
            .SetAs("rotatablex+", blueprintTypename + CAPSULE_SUFFIX + "x+")
            .SetAs("rotatablex-", blueprintTypename + CAPSULE_SUFFIX + "x-")
            .SetAs("rotatablez+", blueprintTypename + CAPSULE_SUFFIX + "z+")
            .SetAs("rotatablez-", blueprintTypename + CAPSULE_SUFFIX + "z-")
            .SetAs("npcLimit", "0")
        );
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX + "x+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename + CAPSULE_SUFFIX)
        );
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX + "x-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename + CAPSULE_SUFFIX)
        );
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX + "z+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename + CAPSULE_SUFFIX)
        );
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX + "z-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename + CAPSULE_SUFFIX)
        );
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.capsules.loadrecipes")]
    [ModLoader.ModCallbackDependsOn("scarabol.blueprints.loadrecipes")]
    public static void AfterItemTypesDefined()
    {
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        RecipePlayer.AllRecipes.Add(new Recipe(new JSONNode()
          .SetAs("results", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", blueprintTypename + CAPSULE_SUFFIX)))
          .SetAs("requires", new JSONNode(NodeType.Array))
        ));
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesServer, "scarabol.capsules.registertypes")]
    public static void AfterItemTypesServer()
    {
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        ItemTypesServer.RegisterOnAdd(blueprintTypename + CAPSULE_SUFFIX, CapsuleBlockCode.OnPlaceCapsule);
      }
    }
  }

  static class CapsuleBlockCode
  {
    public static void OnPlaceCapsule(Vector3Int position, ushort capsuleType, Players.Player causedBy)
    {
      try {
        ThreadManager.InvokeOnMainThread(delegate ()
        {
          ushort realType;
          if (World.TryGetTypeAt(position, out realType) && realType != capsuleType) {
            return;
          }
          ServerManager.TryChangeBlock(position, ItemTypes.IndexLookup.GetIndex("air"));
          bool granted = false;
          JSONNode jsonCapsulePermissionsFile;
          if (Pipliz.JSON.JSON.Deserialize(Path.Combine(CapsulesModEntries.ModDirectory, "capsule_permissions.json"), out jsonCapsulePermissionsFile, false)) {
            JSONNode jsonGrantedArray;
            if (jsonCapsulePermissionsFile.TryGetAs<JSONNode>("granted", out jsonGrantedArray)) {
              if (jsonGrantedArray.NodeType != NodeType.Array) {
                Pipliz.Log.WriteError("permissions object is not an array, please contact the server administrator");
                return;
              }
              foreach (JSONNode jsonGranted in jsonGrantedArray.LoopArray()) {
                string permission = (string) jsonGranted.BareObject;
                if (PermissionsManager.HasPermission(causedBy, permission)) {
                  Pipliz.Log.WriteError(string.Format("Capsule permission granted for {0}", permission));
                  granted = true;
                  break;
                }
              }
            } else {
              Pipliz.Log.WriteError("No permissions array defined in the file, please contact the server administrator");
              return;
            }
          } else {
            Pipliz.Log.WriteError("Could not find permissions file, please contact the server administrator");
            return;
          }
          if (!granted) {
            Chat.Send(causedBy, "You don't have permission to use capsules");
            return;
          }
          string capsuleName = ItemTypes.IndexLookup.GetName(capsuleType);
          string blueprintName = capsuleName.Substring(0, capsuleName.Length - CapsulesModEntries.CAPSULE_SUFFIX.Length - 2);
          Chat.Send(causedBy, string.Format("Starting to build '{0}' at {1}", blueprintName, position));
          List<BlueprintBlock> blocks;
          if (BlueprintsManager.blueprints.TryGetValue(blueprintName, out blocks)) {
            if (blueprintName.EndsWith("_clear")) {
              blocks.Reverse();
            }
            int placed = 0, removed = 0, failed = 0;
            ushort bluetype = ItemTypes.IndexLookup.GetIndex(blueprintName + capsuleName.Substring(capsuleName.Length - 2));
            foreach (BlueprintBlock block in blocks) {
              Vector3Int realPosition = block.GetWorldPosition(blueprintName, position, bluetype);
              if (ServerManager.TryChangeBlock(realPosition, ItemTypes.IndexLookup.GetIndex(block.typename))) {
                if (block.typename.Equals("air")) {
                  removed++;
                } else {
                  placed++;
                }
              } else {
                failed++;
              }
            }
            Chat.Send(causedBy, string.Format("Completed '{0}' at {1} with {2} placed, {3} removed and {4} failed blocks", blueprintName, position, placed, removed, failed));
          } else {
            Chat.Send(causedBy, string.Format("Blueprint '{0}' not found", blueprintName));
          }
        }, 2.0);
      } catch (Exception exception) {
        Pipliz.Log.WriteError(string.Format("Exception in OnPlaceCapsule; {0}", exception.Message));
      }
    }
  }
}
