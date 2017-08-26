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
  public static class ArchitectsModEntries
  {
    public static string JOB_NAME = "scarabol.architect";
    public static string JOB_ITEM_KEY = ConstructionModEntries.MOD_PREFIX + "architects.table";

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterDefiningNPCTypes, "scarabol.architects.registerjobs")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
    public static void AfterDefiningNPCTypes()
    {
      BlockJobManagerTracker.Register<ArchitectJob>(JOB_ITEM_KEY);
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.architects.addrawtypes")]
    [ModLoader.ModCallbackDependsOn("scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      ItemTypesServer.AddTextureMapping(ConstructionModEntries.MOD_PREFIX + "architecttop", new JSONNode()
        .SetAs("albedo", MultiPath.Combine(ConstructionModEntries.RelativeTexturesPath, "albedo", "architectTop"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      ItemTypes.AddRawType(JOB_ITEM_KEY, new JSONNode(NodeType.Object)
                           .SetAs("icon", Path.Combine(ConstructionModEntries.RelativeIconsPath, "architect.png"))
                           .SetAs("onPlaceAudio", "woodPlace")
                           .SetAs("onRemoveAudio", "woodDeleteLight")
                           .SetAs("sideall", "planks")
                           .SetAs("sidey+", ConstructionModEntries.MOD_PREFIX + "architecttop")
                           .SetAs("npcLimit", 0)
      );
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.architects.loadrecipes")]
    public static void AfterItemTypesDefined()
    {
      List<InventoryItem> requirements = new List<InventoryItem>() { new InventoryItem("planks", 1) };
      List<Recipe> architectRecipes = new List<Recipe>();
      foreach (string blueprintTypename in ManagerBlueprints.blueprints.Keys) {
        Recipe architectBlueprintRecipe = new Recipe(requirements, new InventoryItem(blueprintTypename, 1));
        architectRecipes.Add(architectBlueprintRecipe);
      }
      RecipeManager.AddRecipes(JOB_NAME, architectRecipes);
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, "scarabol.architects.addplayercrafts")]
    public static void AfterWorldLoad()
    {
      // add recipes here, otherwise they're inserted before vanilla recipes in player crafts
      RecipePlayer.AllRecipes.Add(new Recipe(new InventoryItem("planks", 1), new InventoryItem(JOB_ITEM_KEY, 1)));
    }
  }

  public class ArchitectJob : CraftingJobBase, IBlockJobBase, INPCTypeDefiner
  {
    public override string NPCTypeKey { get { return ArchitectsModEntries.JOB_NAME; } }

    public override float TimeBetweenJobs { get { return 10.0f; } }

    public override int MaxRecipeCraftsPerHaul { get { return 1; } }

    public override List<string> GetCraftingLimitsTriggers () { return new List<string>() { ArchitectsModEntries.JOB_ITEM_KEY }; }

    NPCTypeSettings INPCTypeDefiner.GetNPCTypeDefinition()
    {
      NPCTypeSettings def = NPCTypeSettings.Default;
      def.keyName = NPCTypeKey;
      def.printName = "Architect";
      def.maskColor1 = new UnityEngine.Color32(220, 220, 220, 255);
      def.type = NPCTypeID.GetNextID();
      return def;
    }
  }
}
