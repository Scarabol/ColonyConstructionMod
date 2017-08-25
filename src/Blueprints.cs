using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Pipliz;
using Pipliz.JSON;
using Pipliz.Chatting;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class BlueprintsModEntries
  {
    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      Pipliz.Log.Write(string.Format("Blueprints relative texture path is {0}", ConstructionModEntries.RelativeTexturesPath));
      ItemTypesServer.AddTextureMapping(ConstructionModEntries.MOD_PREFIX + "blueprinttop", new JSONNode()
        .SetAs("albedo", MultiPath.Combine(ConstructionModEntries.RelativeTexturesPath, "albedo", "blueprintsTop"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      string iconFilepath = MultiPath.Combine(ConstructionModEntries.RelativeIconsPath, "blueprint.png");
      foreach (string blueprintTypename in ManagerBlueprints.blueprints.Keys) {
        ItemTypes.AddRawType(blueprintTypename,
          new JSONNode(NodeType.Object)
            .SetAs("onRemoveAudio", "woodDeleteLight")
            .SetAs("onPlaceAudio", "woodPlace")
            .SetAs("icon", iconFilepath)
            .SetAs("sideall", "planks")
            .SetAs("sidey+", ConstructionModEntries.MOD_PREFIX + "blueprinttop")
            .SetAs("npcLimit", "0")
            .SetAs("isRotatable", "true")
            .SetAs("rotatablex+", blueprintTypename + "x+")
            .SetAs("rotatablex-", blueprintTypename + "x-")
            .SetAs("rotatablez+", blueprintTypename + "z+")
            .SetAs("rotatablez-", blueprintTypename + "z-")
        );
        ItemTypes.AddRawType(blueprintTypename + "x+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename + "x-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename + "z+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
        ItemTypes.AddRawType(blueprintTypename + "z-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename)
        );
      }
    }
  }

  public class BlueprintTodoBlock
  {
    public int offsetx;
    public int offsety;
    public int offsetz;
    public string typename;

    public BlueprintTodoBlock(int offsetx, int offsety, int offsetz, string typename) {
      this.offsetx = offsetx;
      this.offsety = offsety;
      this.offsetz = offsetz;
      this.typename = typename;
    }

    public BlueprintTodoBlock(JSONNode node)
      : this(node.GetAs<int>("offsetx"), node.GetAs<int>("offsety"), node.GetAs<int>("offsetz"), node.GetAs<string>("typename"))
    {
    }

    public JSONNode GetJSON() {
      return new JSONNode()
        .SetAs("offsetx", offsetx)
        .SetAs("offsety", offsety)
        .SetAs("offsetz", offsetz)
        .SetAs("typename", typename);
    }

    public Vector3Int GetWorldPosition(string typeBasename, Vector3Int position, ushort bluetype) {
      ushort hxm = ItemTypes.IndexLookup.GetIndex(typeBasename + "x-");
      ushort hzp = ItemTypes.IndexLookup.GetIndex(typeBasename + "z+");
      ushort hzm = ItemTypes.IndexLookup.GetIndex(typeBasename + "z-");
      int realx = this.offsetz+1;
      int realz = -this.offsetx;
      if (bluetype == hxm) {
        realx = -this.offsetz-1;
        realz = this.offsetx;
      } else if (bluetype == hzp) {
        realx = this.offsetx;
        realz = this.offsetz+1;
      } else if (bluetype == hzm) {
        realx = -this.offsetx;
        realz = -this.offsetz-1;
      }
      return position.Add(realx, this.offsety, realz);
    }
  }
}
