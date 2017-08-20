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
    private static string AssetsDirectory;
    private static string RelativeTexturesPath;
    private static string RelativeIconsPath;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.architects.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      AssetsDirectory = Path.Combine(Path.GetDirectoryName(path), "assets");
      // TODO this is realy hacky (maybe better in future ModAPI)
      RelativeTexturesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "materials", "blocks", "albedo", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(AssetsDirectory, "textures"))).OriginalString;
      RelativeIconsPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "icons", "dummyfile")).MakeRelativeUri(new Uri(MultiPath.Combine(AssetsDirectory, "icons"))).OriginalString;
    }

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
        .SetAs("albedo", MultiPath.Combine(RelativeTexturesPath, "albedo", "architectTop"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      ItemTypes.AddRawType(JOB_ITEM_KEY, new JSONNode(NodeType.Object)
                           .SetAs("icon", Path.Combine(RelativeIconsPath, "architect.png"))
                           .SetAs("onPlaceAudio", "woodPlace")
                           .SetAs("onRemoveAudio", "woodDeleteLight")
                           .SetAs("isRotatable", true)
                           .SetAs("rotatablex+", JOB_ITEM_KEY + "x+")
                           .SetAs("rotatablex-", JOB_ITEM_KEY + "x-")
                           .SetAs("rotatablez+", JOB_ITEM_KEY + "z+")
                           .SetAs("rotatablez-", JOB_ITEM_KEY + "z-")
                           .SetAs("sideall", "planks")
                           .SetAs("sidey+", ConstructionModEntries.MOD_PREFIX + "architecttop")
                           .SetAs("npcLimit", 0)
      );
      ItemTypes.AddRawType(JOB_ITEM_KEY + "x+", new JSONNode(NodeType.Object)
                           .SetAs("parentType", JOB_ITEM_KEY));
      ItemTypes.AddRawType(JOB_ITEM_KEY + "x-", new JSONNode(NodeType.Object)
                           .SetAs("parentType", JOB_ITEM_KEY));
      ItemTypes.AddRawType(JOB_ITEM_KEY + "z+", new JSONNode(NodeType.Object)
                           .SetAs("parentType", JOB_ITEM_KEY));
      ItemTypes.AddRawType(JOB_ITEM_KEY + "z-", new JSONNode(NodeType.Object)
                           .SetAs("parentType", JOB_ITEM_KEY));
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.architects.loadrecipes")]
    public static void AfterItemTypesDefined()
    {
      List<InventoryItem> requirements = new List<InventoryItem>() { new InventoryItem("planks", 1) };
      List<Recipe> architectRecipes = new List<Recipe>();
      foreach (string blueprintTypename in BlueprintsManager.blueprints.Keys) {
        Recipe architectBlueprintRecipe = new Recipe(requirements, new InventoryItem(blueprintTypename, 1));
        architectRecipes.Add(architectBlueprintRecipe);
      }
      RecipeManager.AddRecipes(JOB_NAME, architectRecipes);
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, "scarabol.architects.addplayercrafts")]
    public static void AfterWorldLoad()
    {
      // add recipes here, otherwise they're inserted before vanilla recipes in player crafts
      RecipePlayer.AllRecipes.Add(new Recipe(new JSONNode()
        .SetAs("results", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", JOB_ITEM_KEY)))
        .SetAs("requires", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "planks")))));
    }
  }

  public class ArchitectJob : CraftingJobBase, IBlockJobBase, INPCTypeDefiner
  {
    public override string NPCTypeKey { get { return ArchitectsModEntries.JOB_NAME; } }

    public override float TimeBetweenJobs { get { return 10.0f; } }

    public override int MaxRecipeCraftsPerHaul { get { return 1; } }

    public override List<string> GetCraftingLimitsTriggers ()
    {
      return new List<string>()
      {
        ArchitectsModEntries.JOB_ITEM_KEY + "x+",
        ArchitectsModEntries.JOB_ITEM_KEY + "x-",
        ArchitectsModEntries.JOB_ITEM_KEY + "z+",
        ArchitectsModEntries.JOB_ITEM_KEY + "z-"
      };
    }

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
