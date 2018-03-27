using System.Collections.Generic;
using Pipliz.JSON;
using Pipliz.Mods.APIProvider.Jobs;
using Server.NPCs;
using BlockTypes.Builtin;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class ArchitectsModEntries
  {
    public static string JOB_NAME = "scarabol.architect";
    public static string JOB_ITEM_KEY = ConstructionModEntries.MOD_PREFIX + "architects.table";

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.architects.registerjobs")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.apiprovider.jobs.resolvetypes")]
    public static void RegisterJobs ()
    {
      BlockJobManagerTracker.Register<ArchitectJob> (JOB_ITEM_KEY);
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterSelectedWorld, "scarabol.architects.registertexturemappings")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.server.registertexturemappingtextures")]
    public static void AfterSelectedWorld ()
    {
      var textureMapping = new ItemTypesServer.TextureMapping (new JSONNode ());
      textureMapping.AlbedoPath = MultiPath.Combine (ConstructionModEntries.AssetsDirectory, "textures", "albedo", "architectTop.png");
      ItemTypesServer.SetTextureMapping (ConstructionModEntries.MOD_PREFIX + "architecttop", textureMapping);
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.architects.addrawtypes")]
    [ModLoader.ModCallbackDependsOn ("scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes (Dictionary<string, ItemTypesServer.ItemTypeRaw> itemTypes)
    {
      itemTypes.Add (JOB_ITEM_KEY, new ItemTypesServer.ItemTypeRaw (JOB_ITEM_KEY, new JSONNode ()
        .SetAs ("onPlaceAudio", "woodPlace")
        .SetAs ("onRemoveAudio", "woodDeleteLight")
        .SetAs ("icon", MultiPath.Combine (ConstructionModEntries.AssetsDirectory, "icons", "architect.png"))
        .SetAs ("sideall", "planks")
        .SetAs ("sidey+", ConstructionModEntries.MOD_PREFIX + "architecttop")
        .SetAs ("npcLimit", 0)
      ));
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.architects.loadrecipes")]
    [ModLoader.ModCallbackDependsOn ("pipliz.server.loadresearchables")]
    public static void LoadRecipes ()
    {
      RecipePlayer.AddDefaultRecipe (new Recipe (JOB_ITEM_KEY + ".recipe", new InventoryItem (BuiltinBlocks.Planks, 1), new InventoryItem (JOB_ITEM_KEY, 1), 0));
    }
  }

  public class ArchitectJob : CraftingJobBase, IBlockJobBase, INPCTypeDefiner
  {
    public override string NPCTypeKey { get { return ArchitectsModEntries.JOB_NAME; } }

    public override float CraftingCooldown { get { return 10.0f; } }

    public override int MaxRecipeCraftsPerHaul { get { return 1; } }

    public override List<string> GetCraftingLimitsTriggers ()
    {
      return new List<string> { ArchitectsModEntries.JOB_ITEM_KEY };
    }

    protected override void OnRecipeCrafted ()
    {
      var recipeStorage = RecipeStorage.GetPlayerStorage (owner);
      recipeStorage.SetLimit (selectedRecipe.Name, recipeStorage.GetRecipeSetting (selectedRecipe.Name).Limit - 1);
    }

    NPCTypeStandardSettings INPCTypeDefiner.GetNPCTypeDefinition ()
    {
      return new NPCTypeStandardSettings {
        keyName = NPCTypeKey,
        printName = "Architect",
        maskColor1 = new UnityEngine.Color32 (220, 220, 220, 255),
        type = NPCTypeID.GetNextID ()
      };
    }

    public override IList<Recipe> GetCraftingLimitsRecipes ()
    {
      List<Recipe> result = new List<Recipe> ();
      foreach (string blueprintTypename in ManagerBlueprints.Blueprints.Keys) {
        result.Add (new ArchitectRecipe (blueprintTypename));
      }
      return result;
    }
  }

  public class ArchitectRecipe : Recipe
  {
    public ArchitectRecipe (string blueprintTypename)
      : base (blueprintTypename + ".recipe", new InventoryItem (BuiltinBlocks.Planks, 1), new InventoryItem (blueprintTypename, 1), 0)
    {
    }

    public override int ShouldBeMade (Stockpile stockpile, RecipeStorage.PlayerRecipeStorage playerStorage = null)
    {
      return RecipeStorage.GetPlayerStorage (stockpile.Owner).GetRecipeSetting (this.Name).Limit;
    }
  }
}
