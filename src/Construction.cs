using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;
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

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.construction.assemblyload")]
        public static void OnAssemblyLoaded(string path)
        {
            ModDirectory = Path.GetDirectoryName(path);
            AssetsDirectory = Path.Combine(ModDirectory, "assets");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, "scarabol.construction.registercallbacks")]
        public static void AfterStartup()
        {
            Log.Write("Loaded Construction Mod 6.0.4 by Scarabol");
            ManagerBlueprints.LoadBlueprints(Path.Combine(ModDirectory, "blueprints"));
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.construction.registerjobs")]
        [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
        public static void RegisterJobs()
        {
            foreach(string blueprintTypename in ManagerBlueprints.Blueprints.Keys)
            {
                BlockJobManagerTracker.Register<ConstructionJob>(blueprintTypename);
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, "scarabol.construction.afterworldload")]
        [ModLoader.ModCallbackProvidesFor("pipliz.server.localization.convert")]
        public static void AfterWorldLoad()
        {
            ModLocalizationHelper.Localize(Path.Combine(AssetsDirectory, "localization"), MOD_PREFIX);
            foreach(KeyValuePair<string, JSONNode> locEntry in ManagerBlueprints.BlueprintsLocalizations)
            {
                try
                {
                    ModLocalizationHelper.Localize(locEntry.Key, locEntry.Value, ManagerBlueprints.BLUEPRINTS_PREFIX);
                }
                catch(Exception exception)
                {
                    Log.WriteError($"Exception while localization of {locEntry.Key}; {exception.Message}");
                }
            }
        }
    }


    public class ConstructionJob : BlockJobBase, IBlockJobBase, INPCTypeDefiner
    {
        NPCInventory BlockInventory;
        bool ShouldTakeItems;
        string Fullname;
        List<BlueprintTodoBlock> Todoblocks;

        public override string NPCTypeKey { get { return "scarabol.constructor"; } }

        public override bool NeedsItems { get { return ShouldTakeItems; } }

        public override InventoryItem RecruitementItem { get { return new InventoryItem(ConstructionModEntries.JOB_ITEM_KEY, 1); } }

        public override JSONNode GetJSON()
        {
            JSONNode jsonTodos = new JSONNode(NodeType.Array);
            foreach(BlueprintTodoBlock block in Todoblocks)
            {
                jsonTodos.AddToArray(block.GetJSON());
            }
            return base.GetJSON()
              .SetAs("inventory", BlockInventory.GetJSON())
              .SetAs("shouldTakeItems", ShouldTakeItems)
              .SetAs("fullname", Fullname)
              .SetAs("todoblocks", jsonTodos);
        }

        public ITrackableBlock InitializeOnAdd(Vector3Int position, ushort type, Players.Player player)
        {
            BlockInventory = new NPCInventory(10000000f);
            InitializeJob(player, position, 0);
            Fullname = ItemTypes.IndexLookup.GetName(type);
            string blueprintTypename = Fullname.Substring(0, Fullname.Length - 2);
            ManagerBlueprints.Blueprints.TryGetValue(blueprintTypename, out List<BlueprintTodoBlock> blocks);
            Todoblocks = new List<BlueprintTodoBlock>(blocks);
            Todoblocks.Reverse();
            return this;
        }

        public override ITrackableBlock InitializeFromJSON(Players.Player player, JSONNode node)
        {
            BlockInventory = new NPCInventory(10000000f, node["inventory"]);
            ShouldTakeItems = false;
            node.TryGetAs("shouldTakeItems", out ShouldTakeItems);
            Fullname = node.GetAs<string>("fullname");
            JSONNode jsonTodos = node["todoblocks"];
            Todoblocks = new List<BlueprintTodoBlock>();
            foreach(JSONNode jsonBlock in jsonTodos.LoopArray())
            {
                Todoblocks.Add(new BlueprintTodoBlock(jsonBlock));
            }
            InitializeJob(player, (Vector3Int)node["position"], node.GetAs<int>("npcID"));
            return this;
        }

        public override void OnNPCAtJob(ref NPCBase.NPCState state)
        {
            state.JobIsDone = true;
            usedNPC.LookAt(position.Vector);
            if(!state.Inventory.IsEmpty)
            {
                state.Inventory.Dump(BlockInventory);
            }
            if(Todoblocks.Count < 1)
            {
                BlockInventory.Dump(usedNPC.Inventory);
                ShouldTakeItems = true;
            }
            else
            {
                bool placed = false;
                if(!ItemTypes.IndexLookup.TryGetIndex(Fullname, out ushort bluetype))
                {
                    string msg = $"Bob here from site at {position}, the blueprint '{Fullname}' does not exist, stopped work here";
                    Log.WriteError(msg);
                    Chat.Send(usedNPC.Colony.Owner, msg);
                    Todoblocks.Clear();
                    return;
                }
                ushort scaffoldType = ItemTypes.IndexLookup.GetIndex(ScaffoldsModEntries.SCAFFOLD_ITEM_TYPE);
                string jobname = TypeHelper.RotatableToBasetype(Fullname);
                for(int i = Todoblocks.Count - 1; i >= 0; i--)
                {
                    BlueprintTodoBlock todoblock = Todoblocks[i];
                    Vector3Int realPosition = todoblock.GetWorldPosition(jobname, position, bluetype);
                    if(realPosition.y <= 0)
                    {
                        Todoblocks.RemoveAt(i);
                        continue;
                    }
                    string todoblockBaseTypename = TypeHelper.RotatableToBasetype(todoblock.Typename);
                    string todoblockRotatedTypename = todoblock.Typename;
                    if(!todoblockBaseTypename.Equals(todoblock.Typename))
                    {
                        Vector3Int jobVec = TypeHelper.RotatableToVector(Fullname);
                        Vector3Int blockVec = TypeHelper.RotatableToVector(todoblock.Typename);
                        Vector3Int combinedVec = new Vector3Int(-jobVec.z * blockVec.x + jobVec.x * blockVec.z, 0, jobVec.x * blockVec.x + jobVec.z * blockVec.z);
                        todoblockRotatedTypename = todoblockBaseTypename + TypeHelper.VectorToXZ(combinedVec);
                    }
                    if(!LookupAndWarnItemIndex(todoblockRotatedTypename, out ushort todoblockRotatedType))
                    {
                        Todoblocks.RemoveAt(i);
                    }
                    else if(!World.TryGetTypeAt(realPosition, out ushort actualType) || actualType == todoblockRotatedType)
                    {
                        Todoblocks.RemoveAt(i);
                    }
                    else
                    {
                        if(!LookupAndWarnItemIndex(todoblockBaseTypename, out ushort todoblockBaseType))
                        {
                            Todoblocks.RemoveAt(i);
                        }
                        else if(todoblockRotatedType == BuiltinBlocks.Air || BlockInventory.TryGetOneItem(todoblockBaseType))
                        {
                            Todoblocks.RemoveAt(i);
                            if(ServerManager.TryChangeBlock(realPosition, todoblockRotatedType, owner))
                            {
                                state.JobIsDone = true;
                                if(todoblockRotatedType == BuiltinBlocks.Air)
                                {
                                    state.SetCooldown(ConstructionModEntries.EXCAVATION_DELAY);
                                    state.SetIndicator(new Shared.IndicatorState(ConstructionModEntries.EXCAVATION_DELAY, actualType));
                                }
                                else if(!BlockInventory.IsEmpty && i > 0)
                                {
                                    state.SetIndicator(new Shared.IndicatorState(0.5f, todoblockRotatedType));
                                }
                                if(actualType != BuiltinBlocks.Air && actualType != BuiltinBlocks.Water && actualType != scaffoldType)
                                {
                                    usedNPC.Inventory.Add(ItemTypes.GetType(actualType).OnRemoveItems);
                                }
                                placed = true;
                                break;
                            }
                        }
                    }
                }
                if(!placed)
                {
                    BlockInventory.Dump(usedNPC.Inventory);
                    ShouldTakeItems = true;
                }
            }
        }

        bool LookupAndWarnItemIndex(string typename, out ushort type)
        {
            if(!ItemTypes.IndexLookup.TryGetIndex(typename, out type))
            {
                string msg = $"Bob here from site at {position}, the item type '{typename}' does not exist. Ignoring it...";
                Log.WriteError(msg);
                Chat.Send(usedNPC.Colony.Owner, msg);
                return false;
            }
            return true;
        }

        public override void OnNPCAtStockpile(ref NPCBase.NPCState state)
        {
            state.Inventory.Dump(usedNPC.Colony.UsedStockpile);
            if(Todoblocks.Count < 1)
            {
                ServerManager.TryChangeBlock(position, BuiltinBlocks.Air, owner);
                return;
            }
            state.JobIsDone = true;
            if(!ToSleep)
            {
                ShouldTakeItems = true;
                for(int i = Todoblocks.Count - 1; i >= 0; i--)
                {
                    BlueprintTodoBlock block = Todoblocks[i];
                    if(!block.Typename.Equals("air"))
                    {
                        if(LookupAndWarnItemIndex(TypeHelper.RotatableToBasetype(block.Typename), out ushort typeindex))
                        {
                            if(usedNPC.Colony.UsedStockpile.TryRemove(typeindex, 1))
                            {
                                ShouldTakeItems = false;
                                state.Inventory.Add(typeindex, 1);
                                if(state.Inventory.UsedCapacity >= state.Inventory.Capacity)
                                { // workaround for capacity issue
                                    if(state.Inventory.TryGetOneItem(typeindex))
                                    {
                                        usedNPC.Colony.UsedStockpile.Add(typeindex, 1);
                                    }
                                    return;
                                }
                            }
                        }
                        else
                        {
                            Todoblocks.RemoveAt(i);
                        }
                    }
                    else
                    {
                        ShouldTakeItems = false;
                    }
                }
            }
            if(Todoblocks.Count < 1)
            {
                ServerManager.TryChangeBlock(position, BuiltinBlocks.Air, owner);
                return;
            }
            if(ShouldTakeItems)
            {
                state.JobIsDone = false;
                state.SetIndicator(new Shared.IndicatorState(6f, ItemTypes.IndexLookup.GetIndex(Todoblocks[Todoblocks.Count - 1].Typename), true, false));
            }
        }

        public override void OnRemove()
        {
            BlockInventory.Dump(Stockpile.GetStockPile(owner));
            base.OnRemove();
        }

        NPCTypeStandardSettings INPCTypeDefiner.GetNPCTypeDefinition()
        {
            return new NPCTypeStandardSettings
            {
                keyName = NPCTypeKey,
                printName = "Constructor",
                maskColor1 = new UnityEngine.Color32(75, 100, 140, 255),
                type = NPCTypeID.GetNextID()
            };
        }
    }
}

