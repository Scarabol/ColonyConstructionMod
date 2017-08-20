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
    public static int PREVIEW_THRESHOLD = 1000;
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
      );
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesServer, "scarabol.scaffolds.registertypes")]
    public static void AfterItemTypesServer()
    {
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        ItemTypesServer.RegisterOnAdd(blueprintTypename, ScaffoldBlockCode.AddScaffolds);
        ItemTypesServer.RegisterOnRemove(blueprintTypename, ScaffoldBlockCode.RemoveScaffolds);
      }
    }
  }

  public static class ScaffoldBlockCode
  {
    public static void AddScaffolds(Vector3Int position, ushort bluetype, Players.Player causedBy)
    {
      string blueprintFullname = ItemTypes.IndexLookup.GetName(bluetype);
      string blueprintBasename = blueprintFullname.Substring(0, blueprintFullname.Length-2);
      List<BlueprintBlock> blocks;
      if (BlueprintsManager.blueprints.TryGetValue(blueprintBasename, out blocks)) {
        if (blocks.Count > ScaffoldsModEntries.PREVIEW_THRESHOLD) {
          Chat.Send(causedBy, "Blueprint contains too many blocks for preview");
          return;
        }
        ushort airIndex = ItemTypes.IndexLookup.GetIndex("air");
        ushort scaffoldIndex = ItemTypes.IndexLookup.GetIndex(ScaffoldsModEntries.SCAFFOLD_ITEM_TYPE);
        foreach (BlueprintBlock block in blocks) {
          Vector3Int realPos = block.GetWorldPosition(blueprintBasename, position, bluetype);
          if (!position.Equals(realPos)) {
            ushort wasType;
            if (World.TryGetTypeAt(realPos, out wasType) && wasType == airIndex) {
              ServerManager.TryChangeBlock(realPos, scaffoldIndex);
            }
          }
        }
        ThreadManager.InvokeOnMainThread(delegate ()
        {
          RemoveScaffolds(position, bluetype, causedBy);
        }, 8.0f);
      }
    }

    public static void RemoveScaffolds(Vector3Int position, ushort bluetype, Players.Player causedBy)
    {
      string blueprintFullname = ItemTypes.IndexLookup.GetName(bluetype);
      string blueprintBasename = blueprintFullname.Substring(0, blueprintFullname.Length-2);
      List<BlueprintBlock> blocks;
      if (BlueprintsManager.blueprints.TryGetValue(blueprintBasename, out blocks)) {
        if (blocks.Count > ScaffoldsModEntries.PREVIEW_THRESHOLD) {
          return;
        }
        ushort airIndex = ItemTypes.IndexLookup.GetIndex("air");
        ushort scaffoldIndex = ItemTypes.IndexLookup.GetIndex(ScaffoldsModEntries.SCAFFOLD_ITEM_TYPE);
        foreach (BlueprintBlock block in blocks) {
          Vector3Int realPos = block.GetWorldPosition(blueprintBasename, position, bluetype);
          if (!position.Equals(realPos)) {
            ushort wasType;
            if (World.TryGetTypeAt(realPos, out wasType) && wasType == scaffoldIndex) {
              ServerManager.TryChangeBlock(realPos, airIndex);
            }
          }
        }
      }
    }
  }
}
