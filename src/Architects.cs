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

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.architects.registerjobs")]
        [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
        public static void RegisterJobs()
        {
            BlockJobManagerTracker.Register<ArchitectJob>(JOB_ITEM_KEY);
            //add job interface to the job
            RecipeStorage.AddBlockToRecipeMapping(JOB_ITEM_KEY, JOB_NAME);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.architects.loadrecipes")]
        [ModLoader.ModCallbackDependsOn("pipliz.server.loadresearchables")]
        public static void LoadRecipes()
        {
            //Add blueprints as recipes
            foreach(string blueprintTypename in ManagerBlueprints.Blueprints.Keys)
            {
                RecipeStorage.AddDefaultLimitTypeRecipe(JOB_NAME, new ArchitectRecipe(blueprintTypename));
            }
        }
    }

    public class ArchitectJob : CraftingJobBase, IBlockJobBase, INPCTypeDefiner
    {
        public override string NPCTypeKey { get { return ArchitectsModEntries.JOB_NAME; } }

        public override float CraftingCooldown { get { return 10.0f; } }

        public override int MaxRecipeCraftsPerHaul { get { return 1; } }

        //Decrease the amount of blueprints to build each time that one is crafted
        protected override void OnRecipeCrafted()
        {
            var recipeStorage = RecipeStorage.GetPlayerStorage(owner);
            recipeStorage.SetLimit(selectedRecipe.Name, recipeStorage.GetRecipeSetting(selectedRecipe.Name).Limit - 1);
        }

        NPCTypeStandardSettings INPCTypeDefiner.GetNPCTypeDefinition()
        {
            return new NPCTypeStandardSettings
            {
                keyName = NPCTypeKey,
                printName = "Architect",
                maskColor1 = new UnityEngine.Color32(220, 220, 220, 255),
                type = NPCTypeID.GetNextID()
            };
        }

    }

    public class ArchitectRecipe : Recipe
    {
        public ArchitectRecipe(string blueprintTypename)
          : base(blueprintTypename + ".recipe", new InventoryItem(BuiltinBlocks.Planks, 1), new InventoryItem(blueprintTypename, 1), 0)
        {
        }

        public override int ShouldBeMade(Stockpile stockpile, RecipeStorage.PlayerRecipeStorage playerStorage = null)
        {
            return RecipeStorage.GetPlayerStorage(stockpile.Owner).GetRecipeSetting(Name).Limit;
        }
    }
}
