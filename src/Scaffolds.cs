using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;
using Pipliz.Threading;
using Pipliz.APIProvider.Recipes;
using Pipliz.APIProvider.Jobs;
using NPC;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class ScaffoldsModEntries
  {
    public static string SCAFFOLD_ITEM_TYPE = ConstructionModEntries.MOD_PREFIX + "scaffold";
    public static int PREVIEW_BLOCKS_THRESHOLD = 1000;
    private static string AssetsDirectory;
    private static string RelativeTexturesPath;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.scaffolds.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      AssetsDirectory = Path.Combine(Path.GetDirectoryName(path), "assets");
      // TODO this is realy hacky (maybe better in future ModAPI)
      RelativeTexturesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "materials", "blocks", "albedo", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(AssetsDirectory, "textures"))).OriginalString;
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.scaffolds.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      ItemTypesServer.AddTextureMapping(SCAFFOLD_ITEM_TYPE, new JSONNode()
        .SetAs("albedo", MultiPath.Combine(RelativeTexturesPath, "albedo", "scaffold"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      ItemTypes.AddRawType(SCAFFOLD_ITEM_TYPE, new JSONNode(NodeType.Object)
                           .SetAs("sideall", SCAFFOLD_ITEM_TYPE)
                           .SetAs("onRemove", new JSONNode(NodeType.Array))
                           .SetAs("isSolid", false)
                           .SetAs("destructionTime", 100)
      );
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesServer, "scarabol.scaffolds.registertypes")]
    public static void AfterItemTypesServer()
    {
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        ItemTypesServer.RegisterOnAdd(blueprintTypename, ScaffoldBlockCode.AddScaffolds);
        ItemTypesServer.RegisterOnAdd(blueprintTypename + CapsulesModEntries.CAPSULE_SUFFIX, ScaffoldBlockCode.AddScaffolds);
        ItemTypesServer.RegisterOnRemove(blueprintTypename, ScaffoldBlockCode.RemoveScaffolds);
        ItemTypesServer.RegisterOnRemove(blueprintTypename + CapsulesModEntries.CAPSULE_SUFFIX, ScaffoldBlockCode.RemoveScaffolds);
      }
    }
  }

  public static class ScaffoldBlockCode
  {
    public static void AddScaffolds(Vector3Int position, ushort newtype, Players.Player causedBy)
    {
      string itemtypeFullname = ItemTypes.IndexLookup.GetName(newtype);
      string blueprintBasename = itemtypeFullname.Substring(0, itemtypeFullname.Length-2);
      ushort bluetype = newtype;
      if (blueprintBasename.EndsWith(CapsulesModEntries.CAPSULE_SUFFIX)) {
        blueprintBasename = blueprintBasename.Substring(0, blueprintBasename.Length - CapsulesModEntries.CAPSULE_SUFFIX.Length);
        bluetype = ItemTypes.IndexLookup.GetIndex(blueprintBasename + itemtypeFullname.Substring(itemtypeFullname.Length-2));
      }
      List<BlueprintBlock> blocks;
      if (BlueprintsManager.blueprints.TryGetValue(blueprintBasename, out blocks)) {
        if (blocks.Count > ScaffoldsModEntries.PREVIEW_BLOCKS_THRESHOLD) {
          Chat.Send(causedBy, "Blueprint contains too many blocks for preview");
          return;
        }
        ushort airtype = ItemTypes.IndexLookup.GetIndex("air");
        ushort scaffoldType = ItemTypes.IndexLookup.GetIndex(ScaffoldsModEntries.SCAFFOLD_ITEM_TYPE);
        foreach (BlueprintBlock block in blocks) {
          Vector3Int realPos = block.GetWorldPosition(blueprintBasename, position, bluetype);
          if (!position.Equals(realPos)) {
            ushort wasType;
            if (World.TryGetTypeAt(realPos, out wasType) && wasType == airtype) {
              ServerManager.TryChangeBlock(realPos, scaffoldType);
            }
          }
        }
        ThreadManager.InvokeOnMainThread(delegate ()
        {
          ushort actualType;
          if (World.TryGetTypeAt(position, out actualType) && actualType == newtype) {
            RemoveScaffolds(position, bluetype, causedBy);
          }
        }, 8.0f);
      }
    }

    public static void RemoveScaffolds(Vector3Int position, ushort wastype, Players.Player causedBy)
    {
      string itemtypeFullname = ItemTypes.IndexLookup.GetName(wastype);
      string blueprintBasename = itemtypeFullname.Substring(0, itemtypeFullname.Length-2);
      ushort bluetype = wastype;
      if (blueprintBasename.EndsWith(CapsulesModEntries.CAPSULE_SUFFIX)) {
        blueprintBasename = blueprintBasename.Substring(0, blueprintBasename.Length - CapsulesModEntries.CAPSULE_SUFFIX.Length);
        bluetype = ItemTypes.IndexLookup.GetIndex(blueprintBasename + itemtypeFullname.Substring(itemtypeFullname.Length-2));
      }
      List<BlueprintBlock> blocks;
      if (BlueprintsManager.blueprints.TryGetValue(blueprintBasename, out blocks)) {
        if (blocks.Count > ScaffoldsModEntries.PREVIEW_BLOCKS_THRESHOLD) {
          return;
        }
        ushort airtype = ItemTypes.IndexLookup.GetIndex("air");
        ushort scaffoldType = ItemTypes.IndexLookup.GetIndex(ScaffoldsModEntries.SCAFFOLD_ITEM_TYPE);
        foreach (BlueprintBlock block in blocks) {
          Vector3Int realPos = block.GetWorldPosition(blueprintBasename, position, bluetype);
          if (!position.Equals(realPos)) {
            ushort wasType;
            if (World.TryGetTypeAt(realPos, out wasType) && wasType == scaffoldType) {
              ServerManager.TryChangeBlock(realPos, airtype);
            }
          }
        }
      }
    }
  }
}
