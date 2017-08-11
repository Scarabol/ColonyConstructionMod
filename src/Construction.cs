using System;
using System.Collections.Generic;
using System.IO;
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
    public static string ModDirectory;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.construction.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      ModDirectory = Path.GetDirectoryName(path);
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, "scarabol.construction.registercallbacks")]
    public static void AfterStartup()
    {
      Pipliz.Log.Write("Loaded Construction Mod 1.1 by Scarabol");
      BlueprintsManager.LoadBlueprints("gamedata/mods/Scarabol/Construction/blueprints/");
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.construction.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      ItemTypes.AddRawType("buildhammer",
        new JSONNode(NodeType.Object)
          .SetAs<int>("npcLimit", 1)
          .SetAs("icon", Path.Combine(ModDirectory, "assets/icons/buildhammer.png"))
          .SetAs<bool>("isPlaceable", false)
      );
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.construction.loadrecipes")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.registerrecipes")]
    public static void AfterItemTypesDefined()
    {
      Recipe buildhammerRecipe = new Recipe(new JSONNode()
        .SetAs("results", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "buildhammer")))
        .SetAs("requires", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "ironingot")).AddToArray(new JSONNode().SetAs("type", "planks")))
      );
      RecipePlayer.AllRecipes.Add(buildhammerRecipe);
      RecipeManager.AddRecipes("pipliz.crafter", new List<Recipe>() { buildhammerRecipe });
    }
  }

  public class ConstructionJob : BlockJobBase, IBlockJobBase, INPCTypeDefiner
  {
    NPCInventory blockInventory;
    bool shouldTakeItems;
    string fullname;
    List<BlueprintBlock> todoblocks;

    public override string NPCTypeKey { get { return "scarabol.constructor"; } }

    public override float TimeBetweenJobs { get { return 0.5f; } }

    public override bool NeedsItems { get { return shouldTakeItems; } }

    public override InventoryItem RecruitementItem { get { return new InventoryItem(ItemTypes.IndexLookup.GetIndex("buildhammer"), 1); } }

    public override JSONNode GetJSON()
    {
      JSONNode jsonTodos = new JSONNode(NodeType.Array);
      foreach (BlueprintBlock block in todoblocks) {
        jsonTodos.AddToArray(block.GetJSON());
      }
      return base.GetJSON()
        .SetAs("inventory", blockInventory.GetJSON())
        .SetAs("fullname", fullname)
        .SetAs("todoblocks", jsonTodos)
      ;
    }

    public ITrackableBlock InitializeOnAdd(Vector3Int position, ushort type, Players.Player player)
    {
      blockInventory = new NPCInventory(10000000f);
      InitializeJob(player, position, 0);
      fullname = ItemTypes.IndexLookup.GetName(type);
      string name = fullname.Substring(0, fullname.Length-2);
      List<BlueprintBlock> blocks;
      BlueprintsManager.blueprints.TryGetValue(name, out blocks);
      todoblocks = new List<BlueprintBlock>(blocks);
      todoblocks.Reverse();
      return this;
    }

    public override ITrackableBlock InitializeFromJSON(Players.Player player, JSONNode node)
    {
      blockInventory = new NPCInventory(node["inventory"]);
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
      if (blockInventory.IsEmpty) {
        shouldTakeItems = true;
      } else if (todoblocks.Count < 1) {
        blockInventory.Dump(usedNPC.Inventory);
        shouldTakeItems = true;
      } else {
        ushort bluetype = ItemTypes.IndexLookup.GetIndex(fullname);
        for (int i = todoblocks.Count - 1; i >= 0; i--) {
          BlueprintBlock blueblock = todoblocks[i];
          string name = fullname.Substring(0, fullname.Length-2);
          ushort hxm = ItemTypes.IndexLookup.GetIndex(name+"x-");
          ushort hzp = ItemTypes.IndexLookup.GetIndex(name+"z+");
          ushort hzm = ItemTypes.IndexLookup.GetIndex(name+"z-");
          int realx = blueblock.offsetz+1;
          int realz = -blueblock.offsetx;
          if (bluetype == hxm) {
            realx = -blueblock.offsetz-1;
            realz = blueblock.offsetx;
          } else if (bluetype == hzp) {
            realx = blueblock.offsetx;
            realz = blueblock.offsetz+1;
          } else if (bluetype == hzm) {
            realx = -blueblock.offsetx;
            realz = -blueblock.offsetz-1;
          }
          Vector3Int realPosition = position.Add(realx, blueblock.offsety, realz);
          ushort newType = ItemTypes.IndexLookup.GetIndex(blueblock.typename);
          ushort actualType;
          if (World.TryGetTypeAt(realPosition, out actualType) && actualType != newType) {
            if (blockInventory.TryGetOneItem(newType)) {
              todoblocks.RemoveAt(i);
              ServerManager.TryChangeBlock(realPosition, newType);
              state.JobIsDone = true;
              if (!blockInventory.IsEmpty && i > 0) {
                state.SetIndicator(NPCIndicatorType.Crafted, TimeBetweenJobs, ItemTypes.IndexLookup.GetIndex(todoblocks[i-1].typename));
              }
              break;
            }
          } else {
            todoblocks.RemoveAt(i);
          }
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
      shouldTakeItems = false;
      state.JobIsDone = true;
      foreach (BlueprintBlock block in todoblocks) {
        ushort type = ItemTypes.IndexLookup.GetIndex(block.typename);
        if (usedNPC.Colony.UsedStockpile.Remove(type, 1)) {
          state.Inventory.Add(type, 1);
          if (state.Inventory.UsedCapacity >= state.Inventory.Capacity) { // workaround for capacity issue
            //Chat.SendToAll("oh boy too heavy");
            if (state.Inventory.TryGetOneItem(type)) {
              //Chat.SendToAll(string.Format("put one back and now have cap {0} of {1}", state.Inventory.UsedCapacity, state.Inventory.Capacity));
              usedNPC.Colony.UsedStockpile.Add(type, 1);
            }
            //Chat.SendToAll(string.Format("cap now {0} of {1}", state.Inventory.UsedCapacity, state.Inventory.Capacity));
            return;
          }
        }
      }
      if (state.Inventory.IsEmpty) {
        shouldTakeItems = true;
        state.JobIsDone = false;
        state.SetIndicator(NPCIndicatorType.MissingItem, 6f, ItemTypes.IndexLookup.GetIndex(todoblocks[0].typename));
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
