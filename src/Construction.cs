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
    public static string JOB_ITEM_KEY = MOD_PREFIX + "buildtool";
    public static float EXCAVATION_DELAY = 2.0f;
    public static string ModDirectory;
    public static string AssetsDirectory;
    public static string RelativeTexturesPath;
    public static string RelativeIconsPath;
    private static Recipe buildtoolRecipe;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.construction.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      ModDirectory = Path.GetDirectoryName(path);
      AssetsDirectory = Path.Combine(ModDirectory, "assets");
      ModLocalizationHelper.localize(Path.Combine(AssetsDirectory, "localization"), MOD_PREFIX, false);
      // TODO this is really hacky (maybe better in future ModAPI)
      RelativeTexturesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "materials", "blocks", "albedo", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(AssetsDirectory, "textures"))).OriginalString;
      RelativeIconsPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "icons", "dummyfile")).MakeRelativeUri(new Uri(MultiPath.Combine(AssetsDirectory, "icons"))).OriginalString;
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, "scarabol.construction.registercallbacks")]
    public static void AfterStartup()
    {
      Pipliz.Log.Write("Loaded Construction Mod 2.0 by Scarabol");
      ManagerBlueprints.LoadBlueprints(Path.Combine(ModDirectory, "blueprints"));
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterDefiningNPCTypes, "scarabol.construction.registerjobs")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
    public static void AfterDefiningNPCTypes()
    {
      foreach (string blueprintTypename in ManagerBlueprints.blueprints.Keys) {
        BlockJobManagerTracker.Register<ConstructionJob>(blueprintTypename);
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.construction.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      ItemTypes.AddRawType(JOB_ITEM_KEY,
        new JSONNode(NodeType.Object)
          .SetAs<int>("npcLimit", 1)
          .SetAs("icon", Path.Combine(RelativeIconsPath, "buildtool.png"))
          .SetAs<bool>("isPlaceable", false)
      );
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.construction.loadrecipes")]
    [ModLoader.ModCallbackDependsOn("pipliz.blocknpcs.loadrecipes")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.registerrecipes")]
    public static void AfterItemTypesDefined()
    {
      buildtoolRecipe = new Recipe(new List<InventoryItem>() { new InventoryItem("ironingot", 1), new InventoryItem("planks", 1) }, new InventoryItem(JOB_ITEM_KEY, 1));
      RecipeManager.AddRecipes("pipliz.crafter", new List<Recipe>() { buildtoolRecipe });
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, "scarabol.construction.addplayercrafts")]
    public static void AfterWorldLoad()
    {
      // add recipes here, otherwise they're inserted before vanilla recipes in player crafts
      RecipePlayer.AllRecipes.Add(buildtoolRecipe);
    }
  }

  public class ConstructionJob : BlockJobBase, IBlockJobBase, INPCTypeDefiner
  {
    NPCInventory blockInventory;
    bool shouldTakeItems;
    string fullname;
    List<BlueprintTodoBlock> todoblocks;

    public override string NPCTypeKey { get { return "scarabol.constructor"; } }

    public override float TimeBetweenJobs { get { return 0.5f; } }

    public override bool NeedsItems { get { return shouldTakeItems; } }

    public override InventoryItem RecruitementItem { get { return new InventoryItem(ItemTypes.IndexLookup.GetIndex(ConstructionModEntries.JOB_ITEM_KEY), 1); } }

    public override JSONNode GetJSON()
    {
      JSONNode jsonTodos = new JSONNode(NodeType.Array);
      foreach (BlueprintTodoBlock block in todoblocks) {
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
      List<BlueprintTodoBlock> blocks;
      ManagerBlueprints.blueprints.TryGetValue(blueprintTypename, out blocks);
      todoblocks = new List<BlueprintTodoBlock>(blocks);
      todoblocks.Reverse();
      return this;
    }

    public override ITrackableBlock InitializeFromJSON(Players.Player player, JSONNode node)
    {
      blockInventory = new NPCInventory(node["inventory"]);
      shouldTakeItems = false;
      node.TryGetAs<bool>("shouldTakeItems", out shouldTakeItems);
      fullname = node.GetAs<string>("fullname");
      JSONNode jsonTodos = node["todoblocks"];
      todoblocks = new List<BlueprintTodoBlock>();
      foreach (JSONNode jsonBlock in jsonTodos.LoopArray()) {
        todoblocks.Add(new BlueprintTodoBlock(jsonBlock));
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
        ushort scaffoldType = ItemTypes.IndexLookup.GetIndex(ScaffoldsModEntries.SCAFFOLD_ITEM_TYPE);
        string jobname = fullname.Substring(0, fullname.Length-2);
        for (int i = todoblocks.Count - 1; i >= 0; i--) {
          BlueprintTodoBlock todoblock = todoblocks[i];
          Vector3Int realPosition = todoblock.GetWorldPosition(jobname, position, bluetype);
          ushort newType = ItemTypes.IndexLookup.GetIndex(todoblock.typename);
          ushort actualType;
          if (World.TryGetTypeAt(realPosition, out actualType) && actualType != newType) {
            if (newType == BlockTypes.Builtin.BuiltinBlocks.Air || blockInventory.TryGetOneItem(newType)) {
              todoblocks.RemoveAt(i);
              if (ServerManager.TryChangeBlock(realPosition, newType)) {
                state.JobIsDone = true;
                if (newType == BlockTypes.Builtin.BuiltinBlocks.Air) {
                  OverrideCooldown(ConstructionModEntries.EXCAVATION_DELAY);
                  state.SetIndicator(NPCIndicatorType.MissingItem, ConstructionModEntries.EXCAVATION_DELAY, actualType);
                } else if (!blockInventory.IsEmpty && i > 0) {
                  state.SetIndicator(NPCIndicatorType.Crafted, TimeBetweenJobs, ItemTypes.IndexLookup.GetIndex(todoblocks[i-1].typename));
                }
                if (actualType != BlockTypes.Builtin.BuiltinBlocks.Air && actualType != scaffoldType) {
                  usedNPC.Inventory.Add(ItemTypes.RemovalItems(actualType));
                }
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
        ServerManager.TryChangeBlock(position, BlockTypes.Builtin.BuiltinBlocks.Air);
        return;
      }
      shouldTakeItems = true;
      state.JobIsDone = true;
      for (int i = todoblocks.Count - 1; i >= 0; i--) {
        BlueprintTodoBlock block = todoblocks[i];
        if (!block.typename.Equals("air")) {
          ushort typeindex;
          if (ItemTypes.IndexLookup.TryGetIndex(block.typename, out typeindex)) {
            if (usedNPC.Colony.UsedStockpile.TryRemove(typeindex, 1)) {
              shouldTakeItems = false;
              state.Inventory.Add(typeindex, 1);
              if (state.Inventory.UsedCapacity >= state.Inventory.Capacity) { // workaround for capacity issue
                if (state.Inventory.TryGetOneItem(typeindex)) {
                  usedNPC.Colony.UsedStockpile.Add(typeindex, 1);
                }
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
