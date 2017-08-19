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
  public static class ConstructionModEntries
  {
    public static string MOD_PREFIX = "mods.scarabol.construction.";
    public static string ModDirectory;
    private static string AssetsDirectory;
    private static string RelativeIconsPath;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.construction.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      ModDirectory = Path.GetDirectoryName(path);
      AssetsDirectory = Path.Combine(ModDirectory, "assets");
      ModLocalizationHelper.localize(Path.Combine(AssetsDirectory, "localization"), MOD_PREFIX, false);
      // TODO this is realy hacky (maybe better in future ModAPI)
      RelativeIconsPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "icons", "dummyfile")).MakeRelativeUri(new Uri(MultiPath.Combine(AssetsDirectory, "icons"))).OriginalString;
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, "scarabol.construction.registercallbacks")]
    public static void AfterStartup()
    {
      Pipliz.Log.Write("Loaded Construction Mod 1.5 by Scarabol");
      BlueprintsManager.LoadBlueprints(Path.Combine(ModDirectory, "blueprints"));
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterDefiningNPCTypes, "scarabol.construction.registerjobs")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
    public static void AfterDefiningNPCTypes()
    {
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        BlockJobManagerTracker.Register<ConstructionJob>(blueprintTypename);
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.construction.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      ItemTypes.AddRawType("mods.scarabol.construction.buildtool",
        new JSONNode(NodeType.Object)
          .SetAs<int>("npcLimit", 1)
          .SetAs("icon", Path.Combine(RelativeIconsPath, "buildtool.png"))
          .SetAs<bool>("isPlaceable", false)
      );
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.construction.loadrecipes")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.registerrecipes")]
    public static void AfterItemTypesDefined()
    {
      Recipe buildtoolRecipe = new Recipe(new JSONNode()
        .SetAs("results", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "mods.scarabol.construction.buildtool")))
        .SetAs("requires", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "ironingot")).AddToArray(new JSONNode().SetAs("type", "planks")))
      );
      RecipePlayer.AllRecipes.Add(buildtoolRecipe);
      RecipeManager.AddRecipes("pipliz.crafter", new List<Recipe>() { buildtoolRecipe });
    }
  }

  public class ConstructionJob : BlockJobBase, IBlockJobBase, INPCTypeDefiner
  {
    private static float EXCAVATION_DELAY = 1.5f;
    NPCInventory blockInventory;
    bool shouldTakeItems;
    string fullname;
    List<BlueprintBlock> todoblocks;

    public override string NPCTypeKey { get { return "scarabol.constructor"; } }

    public override float TimeBetweenJobs { get { return 0.5f; } }

    public override bool NeedsItems { get { return shouldTakeItems; } }

    public override InventoryItem RecruitementItem { get { return new InventoryItem(ItemTypes.IndexLookup.GetIndex("mods.scarabol.construction.buildtool"), 1); } }

    public override JSONNode GetJSON()
    {
      JSONNode jsonTodos = new JSONNode(NodeType.Array);
      foreach (BlueprintBlock block in todoblocks) {
        jsonTodos.AddToArray(block.GetJSON());
      }
      return base.GetJSON()
        .SetAs("inventory", blockInventory.GetJSON())
        .SetAs("shouldTakeItems", shouldTakeItems)
        .SetAs("fullname", fullname)
        .SetAs("todoblocks", jsonTodos)
      ;
    }

    public ITrackableBlock InitializeOnAdd(Vector3Int position, ushort type, Players.Player player)
    {
      blockInventory = new NPCInventory(10000000f);
      InitializeJob(player, position, 0);
      fullname = ItemTypes.IndexLookup.GetName(type);
      string blueprintTypename = fullname.Substring(0, fullname.Length-2);
      List<BlueprintBlock> blocks;
      BlueprintsManager.blueprints.TryGetValue(blueprintTypename, out blocks);
      todoblocks = new List<BlueprintBlock>(blocks);
      todoblocks.Reverse();
      return this;
    }

    public override ITrackableBlock InitializeFromJSON(Players.Player player, JSONNode node)
    {
      blockInventory = new NPCInventory(node["inventory"]);
      shouldTakeItems = node.GetAs<bool>("shouldTakeItems");
      fullname = node.GetAs<string>("fullname");
      JSONNode jsonTodos = node["todoblocks"];
      todoblocks = new List<BlueprintBlock>();
      foreach (JSONNode jsonBlock in jsonTodos.LoopArray()) {
        todoblocks.Add(new BlueprintBlock(jsonBlock));
      }
      InitializeJob(player, (Vector3Int)node["position"], node.GetAs<int>("npcID"));
      return this;
    }

    public override void OnNPCDoJob(ref NPCBase.NPCState state)
    {
      state.JobIsDone = true;
      usedNPC.LookAt(position.Vector);
      if (!state.Inventory.IsEmpty) {
        state.Inventory.Dump(blockInventory);
      }
      if (todoblocks.Count < 1) {
        blockInventory.Dump(usedNPC.Inventory);
        shouldTakeItems = true;
      } else {
        bool placed = false;
        ushort bluetype = ItemTypes.IndexLookup.GetIndex(fullname);
        for (int i = todoblocks.Count - 1; i >= 0; i--) {
          BlueprintBlock blueblock = todoblocks[i];
          string jobname = fullname.Substring(0, fullname.Length-2);
          Vector3Int realPosition = blueblock.GetWorldPosition(jobname, position, bluetype);
          ushort newType = ItemTypes.IndexLookup.GetIndex(blueblock.typename);
          ushort actualType;
          if (World.TryGetTypeAt(realPosition, out actualType) && actualType != newType) {
            if (newType == ItemTypes.IndexLookup.GetIndex("air") || blockInventory.TryGetOneItem(newType)) {
              todoblocks.RemoveAt(i);
              if (ServerManager.TryChangeBlock(realPosition, newType)) {
                state.JobIsDone = true;
                if (newType == ItemTypes.IndexLookup.GetIndex("air")) {
                  OverrideCooldown(EXCAVATION_DELAY);
                  state.SetIndicator(NPCIndicatorType.MissingItem, EXCAVATION_DELAY, actualType);
                } else if (!blockInventory.IsEmpty && i > 0) {
                  state.SetIndicator(NPCIndicatorType.Crafted, TimeBetweenJobs, ItemTypes.IndexLookup.GetIndex(todoblocks[i-1].typename));
                }
                usedNPC.Inventory.Add(new InventoryItem(actualType, 1));
                placed = true;
                break;
              }
            }
          } else {
            todoblocks.RemoveAt(i);
          }
        }
        if (!placed) {
          blockInventory.Dump(usedNPC.Inventory);
          shouldTakeItems = true;
        }
      }
    }

    public override void OnNPCDoStockpile(ref NPCBase.NPCState state)
    {
      state.Inventory.TryDump(usedNPC.Colony.UsedStockpile);
      if (todoblocks.Count < 1) {
        ServerManager.TryChangeBlock(position, ItemTypes.IndexLookup.GetIndex("air"));
        return;
      }
      shouldTakeItems = true;
      state.JobIsDone = true;
      for (int i = todoblocks.Count - 1; i >= 0; i--) {
        BlueprintBlock block = todoblocks[i];
        if (!block.typename.Equals("air")) {
          ushort typeindex;
          if (ItemTypes.IndexLookup.TryGetIndex(block.typename, out typeindex)) {
            if (usedNPC.Colony.UsedStockpile.Remove(typeindex, 1)) {
              shouldTakeItems = false;
              state.Inventory.Add(typeindex, 1);
              if (state.Inventory.UsedCapacity >= state.Inventory.Capacity) { // workaround for capacity issue
                //Chat.SendToAll("oh boy too heavy");
                if (state.Inventory.TryGetOneItem(typeindex)) {
                  //Chat.SendToAll(string.Format("put one back and now have cap {0} of {1}", state.Inventory.UsedCapacity, state.Inventory.Capacity));
                  usedNPC.Colony.UsedStockpile.Add(typeindex, 1);
                }
                //Chat.SendToAll(string.Format("cap now {0} of {1}", state.Inventory.UsedCapacity, state.Inventory.Capacity));
                return;
              }
            }
          } else {
            Chat.Send(usedNPC.Colony.Owner, string.Format("Bob here from site at {0}, the item type '{1}' does not exist. Ignoring it...", position, block.typename));
            todoblocks.RemoveAt(i);
          }
        } else {
          shouldTakeItems = false;
        }
      }
      if (shouldTakeItems && todoblocks.Count > 0) {
        state.JobIsDone = false;
        state.SetIndicator(NPCIndicatorType.MissingItem, 6f, ItemTypes.IndexLookup.GetIndex(todoblocks[todoblocks.Count-1].typename));
      }
    }

    NPCTypeSettings INPCTypeDefiner.GetNPCTypeDefinition()
    {
      NPCTypeSettings def = NPCTypeSettings.Default;
      def.keyName = NPCTypeKey;
      def.printName = "Constructor";
      def.maskColor1 = new UnityEngine.Color32(75, 100, 140, 255);
      def.type = NPCTypeID.GetNextID();
      return def;
    }
  }
}
