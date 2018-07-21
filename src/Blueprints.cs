using System.Collections.Generic;
using Pipliz;
using Pipliz.JSON;

namespace ScarabolMods
{
    [ModLoader.ModManager]
    public static class BlueprintsModEntries
    {
        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.blueprints.addrawtypes")]
        public static void AfterAddingBaseTypes(Dictionary<string, ItemTypesServer.ItemTypeRaw> itemTypes)
        {
            string iconFilepath = MultiPath.Combine(ConstructionModEntries.AssetsDirectory, "icons", "blueprint.png");
            foreach(string blueprintTypename in ManagerBlueprints.Blueprints.Keys)
            {
                itemTypes.Add(blueprintTypename, new ItemTypesServer.ItemTypeRaw(blueprintTypename,
                  new JSONNode()
                    .SetAs("onPlaceAudio", "woodPlace")
                    .SetAs("onRemoveAudio", "woodDeleteLight")
                    .SetAs("icon", iconFilepath)
                    .SetAs("sideall", "planks")
                    .SetAs("sidey+", "mods.scarabol.construction.blueprintTop")
                    .SetAs("npcLimit", "0")
                    .SetAs("isRotatable", "true")
                    .SetAs("rotatablex+", blueprintTypename + "x+")
                    .SetAs("rotatablex-", blueprintTypename + "x-")
                    .SetAs("rotatablez+", blueprintTypename + "z+")
                    .SetAs("rotatablez-", blueprintTypename + "z-")
                ));
                itemTypes.Add(blueprintTypename + "x+", new ItemTypesServer.ItemTypeRaw(blueprintTypename + "x+",
                  new JSONNode()
                    .SetAs("parentType", blueprintTypename)
                ));
                itemTypes.Add(blueprintTypename + "x-", new ItemTypesServer.ItemTypeRaw(blueprintTypename + "x-",
                  new JSONNode()
                    .SetAs("parentType", blueprintTypename)
                ));
                itemTypes.Add(blueprintTypename + "z+", new ItemTypesServer.ItemTypeRaw(blueprintTypename + "z+",
                  new JSONNode()
                    .SetAs("parentType", blueprintTypename)
                ));
                itemTypes.Add(blueprintTypename + "z-", new ItemTypesServer.ItemTypeRaw(blueprintTypename + "z-",
                  new JSONNode()
                    .SetAs("parentType", blueprintTypename)
                ));
            }
        }
    }

    public class BlueprintTodoBlock
    {
        public int OffsetX;
        public int OffsetY;
        public int OffsetZ;
        public string Typename;

        public BlueprintTodoBlock(int offsetx, int offsety, int offsetz, string typename)
        {
            OffsetX = offsetx;
            OffsetY = offsety;
            OffsetZ = offsetz;
            Typename = typename;
        }

        public BlueprintTodoBlock(JSONNode node)
        {
            OffsetX = node.getAsOrElse<int>("x", "offsetx");
            OffsetY = node.getAsOrElse<int>("y", "offsety");
            OffsetZ = node.getAsOrElse<int>("z", "offsetz");
            Typename = node.getAsOrElse<string>("t", "typename");
        }

        public JSONNode GetJSON()
        {
            return new JSONNode()
              .SetAs("x", OffsetX)
              .SetAs("y", OffsetY)
              .SetAs("z", OffsetZ)
              .SetAs("t", Typename);
        }

        public Vector3Int GetWorldPosition(string typeBasename, Vector3Int position, ushort bluetype)
        {
            ushort hxm = ItemTypes.IndexLookup.GetIndex(typeBasename + "x-");
            ushort hzp = ItemTypes.IndexLookup.GetIndex(typeBasename + "z+");
            ushort hzm = ItemTypes.IndexLookup.GetIndex(typeBasename + "z-");
            int realx = OffsetZ + 1;
            int realz = -OffsetX;
            if(bluetype == hxm)
            {
                realx = -OffsetZ - 1;
                realz = OffsetX;
            }
            else if(bluetype == hzp)
            {
                realx = OffsetX;
                realz = OffsetZ + 1;
            }
            else if(bluetype == hzm)
            {
                realx = -OffsetX;
                realz = -OffsetZ - 1;
            }
            return position.Add(realx, OffsetY, realz);
        }
    }
}
