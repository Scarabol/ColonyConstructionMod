using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;
using Pipliz.Threading;
using Pipliz.Mods.APIProvider.Jobs;
using NPC;
using Server.NPCs;
using BlockTypes.Builtin;

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
    private static Recipe buildtoolRecipe;

    [ModLoader.ModCallback (ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.construction.assemblyload")]
    public static void OnAssemblyLoaded (string path)
    {
      ModDirectory = Path.GetDirectoryName (path);
      AssetsDirectory = Path.Combine (ModDirectory, "assets");
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterStartup, "scarabol.construction.registercallbacks")]
    public static void AfterStartup ()
    {
      Pipliz.Log.Write ("Loaded Construction Mod 5.0.1 by Scarabol");
      ManagerBlueprints.LoadBlueprints (Path.Combine (ModDirectory, "blueprints"));
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.construction.registerjobs")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.apiprovider.jobs.resolvetypes")]
    public static void RegisterJobs ()
    {
      foreach (string blueprintTypename in ManagerBlueprints.blueprints.Keys) {
        BlockJobManagerTracker.Register<ConstructionJob> (blueprintTypename);
      }
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.construction.addrawtypes")]
    public static void AfterAddingBaseTypes (Dictionary<string, ItemTypesServer.ItemTypeRaw> itemTypes)
    {
      itemTypes.Add (JOB_ITEM_KEY, new ItemTypesServer.ItemTypeRaw (JOB_ITEM_KEY, new JSONNode ()
        .SetAs ("npcLimit", 1)
        .SetAs ("icon", MultiPath.Combine (AssetsDirectory, "icons", "buildtool.png"))
        .SetAs ("isPlaceable", false)
      ));
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.construction.loadrecipes")]
    [ModLoader.ModCallbackDependsOn ("pipliz.server.loadresearchables")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.server.loadsortorder")]
    public static void LoadRecipes ()
    {
      buildtoolRecipe = new Recipe (JOB_ITEM_KEY + ".recipe", new List<InventoryItem> () {
        new InventoryItem (BuiltinBlocks.IronIngot, 1),
        new InventoryItem (BuiltinBlocks.Planks, 1)
      }, new InventoryItem (JOB_ITEM_KEY, 1), 0);
      RecipeStorage.AddDefaultLimitTypeRecipe ("pipliz.crafter", buildtoolRecipe);
      RecipePlayer.AddDefaultRecipe (buildtoolRecipe);
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterWorldLoad, "scarabol.construction.afterworldload")]
    [ModLoader.ModCallbackDependsOn ("pipliz.server.localization.waitforloading")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.server.localization.convert")]
    public static void AfterWorldLoad ()
    {
      ModLocalizationHelper.localize (Path.Combine (AssetsDirectory, "localization"), ConstructionModEntries.MOD_PREFIX);
      foreach (KeyValuePair<string, JSONNode> locEntry in ManagerBlueprints.blueprintsLocalizations) {
        try {
          ModLocalizationHelper.localize (locEntry.Key, locEntry.Value, ManagerBlueprints.BLUEPRINTS_PREFIX);
        } catch (Exception exception) {
          Pipliz.Log.WriteError (string.Format ("Exception while localization of {0}; {1}", locEntry.Key, exception.Message));
        }
      }
    }
  }

  public class ConstructionJob : BlockJobBase, IBlockJobBase, INPCTypeDefiner
  {
    NPCInventory blockInventory;
    bool shouldTakeItems;
    string fullname;
    List<BlueprintTodoBlock> todoblocks;

    public override string NPCTypeKey { get { return "scarabol.constructor"; } }

    public override bool NeedsItems { get { return shouldTakeItems; } }

    public override InventoryItem RecruitementItem { get { return new InventoryItem (ItemTypes.IndexLookup.GetIndex (ConstructionModEntries.JOB_ITEM_KEY), 1); } }

    public override JSONNode GetJSON ()
    {
      JSONNode jsonTodos = new JSONNode (NodeType.Array);
      foreach (BlueprintTodoBlock block in todoblocks) {
        jsonTodos.AddToArray (block.GetJSON ());
      }
      return base.GetJSON ()
        .SetAs ("inventory", blockInventory.GetJSON ())
        .SetAs ("shouldTakeItems", shouldTakeItems)
        .SetAs ("fullname", fullname)
        .SetAs ("todoblocks", jsonTodos);
    }

    public ITrackableBlock InitializeOnAdd (Vector3Int position, ushort type, Players.Player player)
    {
      blockInventory = new NPCInventory (10000000f);
      InitializeJob (player, position, 0);
      fullname = ItemTypes.IndexLookup.GetName (type);
      string blueprintTypename = fullname.Substring (0, fullname.Length - 2);
      List<BlueprintTodoBlock> blocks;
      ManagerBlueprints.blueprints.TryGetValue (blueprintTypename, out blocks);
      todoblocks = new List<BlueprintTodoBlock> (blocks);
      todoblocks.Reverse ();
      return this;
    }

    public override ITrackableBlock InitializeFromJSON (Players.Player player, JSONNode node)
    {
      blockInventory = new NPCInventory (node ["inventory"]);
      shouldTakeItems = false;
      node.TryGetAs ("shouldTakeItems", out shouldTakeItems);
      fullname = node.GetAs<string> ("fullname");
      JSONNode jsonTodos = node ["todoblocks"];
      todoblocks = new List<BlueprintTodoBlock> ();
      foreach (JSONNode jsonBlock in jsonTodos.LoopArray()) {
        todoblocks.Add (new BlueprintTodoBlock (jsonBlock));
      }
      InitializeJob (player, (Vector3Int)node ["position"], node.GetAs<int> ("npcID"));
      return this;
    }

    public override void OnNPCAtJob (ref NPCBase.NPCState state)
    {
      state.JobIsDone = true;
      usedNPC.LookAt (position.Vector);
      if (!state.Inventory.IsEmpty) {
        state.Inventory.Dump (blockInventory);
      }
      if (todoblocks.Count < 1) {
        blockInventory.Dump (usedNPC.Inventory);
        shouldTakeItems = true;
      } else {
        bool placed = false;
        ushort bluetype = ItemTypes.IndexLookup.GetIndex (fullname);
        ushort scaffoldType = ItemTypes.IndexLookup.GetIndex (ScaffoldsModEntries.SCAFFOLD_ITEM_TYPE);
        string jobname = TypeHelper.RotatableToBasetype (fullname);
        for (int i = todoblocks.Count - 1; i >= 0; i--) {
          BlueprintTodoBlock todoblock = todoblocks [i];
          Vector3Int realPosition = todoblock.GetWorldPosition (jobname, position, bluetype);
          if (realPosition.y <= 0) {
            todoblocks.RemoveAt (i);
            continue;
          }
          string baseTypename = TypeHelper.RotatableToBasetype (todoblock.typename);
          string rotatedTypename = todoblock.typename;
          if (!baseTypename.Equals (todoblock.typename)) {
            Vector3Int jobVec = TypeHelper.RotatableToVector (fullname);
            Vector3Int blockVec = TypeHelper.RotatableToVector (todoblock.typename);
            Vector3Int combinedVec = new Vector3Int (-jobVec.z * blockVec.x + jobVec.x * blockVec.z, 0, jobVec.x * blockVec.x + jobVec.z * blockVec.z);
            rotatedTypename = baseTypename + TypeHelper.VectorToXZ (combinedVec);
          }
          ushort newType = ItemTypes.IndexLookup.GetIndex (rotatedTypename);
          ushort actualType;
          if (!World.TryGetTypeAt (realPosition, out actualType) || actualType == newType) {
            todoblocks.RemoveAt (i);
          } else {
            ushort baseType = ItemTypes.IndexLookup.GetIndex (baseTypename);
            if (newType == BuiltinBlocks.Air || blockInventory.TryGetOneItem (baseType)) {
              todoblocks.RemoveAt (i);
              if (ServerManager.TryChangeBlock (realPosition, newType, ServerManager.SetBlockFlags.DefaultAudio)) {
                state.JobIsDone = true;
                if (newType == BuiltinBlocks.Air) {
                  state.SetCooldown (ConstructionModEntries.EXCAVATION_DELAY);
                  state.SetIndicator (new Shared.IndicatorState (ConstructionModEntries.EXCAVATION_DELAY, actualType));
                } else if (!blockInventory.IsEmpty && i > 0) {
                  state.SetIndicator (new Shared.IndicatorState (0.5f, ItemTypes.IndexLookup.GetIndex (rotatedTypename)));
                }
                if (actualType != BuiltinBlocks.Air && actualType != BuiltinBlocks.Water && actualType != scaffoldType) {
                  usedNPC.Inventory.Add (ItemTypes.GetType (actualType).OnRemoveItems);
                }
                placed = true;
                break;
              }
            }
          }
        }
        if (!placed) {
          blockInventory.Dump (usedNPC.Inventory);
          shouldTakeItems = true;
        }
      }
    }

    public override void OnNPCAtStockpile (ref NPCBase.NPCState state)
    {
      state.Inventory.TryDump (usedNPC.Colony.UsedStockpile);
      if (todoblocks.Count < 1) {
        ServerManager.TryChangeBlock (position, BuiltinBlocks.Air);
        return;
      }
      state.JobIsDone = true;
      if (!ToSleep) {
        shouldTakeItems = true;
        for (int i = todoblocks.Count - 1; i >= 0; i--) {
          BlueprintTodoBlock block = todoblocks [i];
          if (!block.typename.Equals ("air")) {
            ushort typeindex;
            if (ItemTypes.IndexLookup.TryGetIndex (TypeHelper.RotatableToBasetype (block.typename), out typeindex)) {
              if (usedNPC.Colony.UsedStockpile.TryRemove (typeindex, 1)) {
                shouldTakeItems = false;
                state.Inventory.Add (typeindex, 1);
                if (state.Inventory.UsedCapacity >= state.Inventory.Capacity) { // workaround for capacity issue
                  if (state.Inventory.TryGetOneItem (typeindex)) {
                    usedNPC.Colony.UsedStockpile.Add (typeindex, 1);
                  }
                  return;
                }
              }
            } else {
              Chat.Send (usedNPC.Colony.Owner, string.Format ("Bob here from site at {0}, the item type '{1}' does not exist. Ignoring it...", position, block.typename));
              todoblocks.RemoveAt (i);
            }
          } else {
            shouldTakeItems = false;
          }
        }
      }
      if (todoblocks.Count < 1) {
        ServerManager.TryChangeBlock (position, BuiltinBlocks.Air);
        return;
      } else if (shouldTakeItems) {
        state.JobIsDone = false;
        state.SetIndicator (new Shared.IndicatorState (6f, ItemTypes.IndexLookup.GetIndex (todoblocks [todoblocks.Count - 1].typename), true, false));
      }
    }

    public override void OnRemove ()
    {
      blockInventory.TryDump (Stockpile.GetStockPile (owner));
      base.OnRemove ();
    }

    NPCTypeStandardSettings INPCTypeDefiner.GetNPCTypeDefinition ()
    {
      return new NPCTypeStandardSettings () {
        keyName = NPCTypeKey,
        printName = "Constructor",
        maskColor1 = new UnityEngine.Color32 (75, 100, 140, 255),
        type = NPCTypeID.GetNextID ()
      };
    }
  }
}
