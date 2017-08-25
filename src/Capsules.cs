using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;
using Pipliz.Threading;
using Permissions;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class CapsulesModEntries
  {
    public static string CAPSULE_PERMISSION = ConstructionModEntries.MOD_PREFIX + "usecapsules";
    public static string CAPSULE_SUFFIX = ".capsule";

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.capsules.addrawtypes")]
    [ModLoader.ModCallbackDependsOn("scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      ItemTypesServer.AddTextureMapping(ConstructionModEntries.MOD_PREFIX + "capsuletop", new JSONNode()
        .SetAs("albedo", MultiPath.Combine(ConstructionModEntries.RelativeTexturesPath, "albedo", "capsulesTop"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      string iconFilepath = Path.Combine(ConstructionModEntries.RelativeIconsPath, "capsule.png");
      foreach (string blueprintTypename in ManagerBlueprints.blueprints.Keys) {
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX,
          new JSONNode(NodeType.Object)
            .SetAs("onPlaceAudio", "woodPlace")
            .SetAs("icon", iconFilepath)
            .SetAs("sideall", "planks")
            .SetAs("sidey+", ConstructionModEntries.MOD_PREFIX + "capsuletop")
            .SetAs("isSolid", "false")
            .SetAs("isRotatable", "true")
            .SetAs("rotatablex+", blueprintTypename + CAPSULE_SUFFIX + "x+")
            .SetAs("rotatablex-", blueprintTypename + CAPSULE_SUFFIX + "x-")
            .SetAs("rotatablez+", blueprintTypename + CAPSULE_SUFFIX + "z+")
            .SetAs("rotatablez-", blueprintTypename + CAPSULE_SUFFIX + "z-")
            .SetAs("npcLimit", "0")
        );
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX + "x+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename + CAPSULE_SUFFIX)
        );
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX + "x-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename + CAPSULE_SUFFIX)
        );
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX + "z+",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename + CAPSULE_SUFFIX)
        );
        ItemTypes.AddRawType(blueprintTypename + CAPSULE_SUFFIX + "z-",
          new JSONNode(NodeType.Object)
            .SetAs("parentType", blueprintTypename + CAPSULE_SUFFIX)
        );
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesServer, "scarabol.capsules.registertypes")]
    public static void AfterItemTypesServer()
    {
      foreach (string blueprintTypename in ManagerBlueprints.blueprints.Keys) {
        ItemTypesServer.RegisterOnAdd(blueprintTypename + CAPSULE_SUFFIX, CapsuleBlockCode.OnPlaceCapsule);
      }
      ChatCommands.CommandManager.RegisterCommand(new CapsuleChatCommand());
    }
  }

  static class CapsuleBlockCode
  {
    public static void OnPlaceCapsule(Vector3Int position, ushort capsuleType, Players.Player causedBy)
    {
      if (!PermissionsManager.CheckAndWarnPermission(causedBy, CapsulesModEntries.CAPSULE_PERMISSION)) {
        ServerManager.TryChangeBlock(position, BlockTypes.Builtin.BuiltinBlocks.Air);
        return;
      }
      ThreadManager.InvokeOnMainThread(delegate ()
      {
        ushort realType;
        if (World.TryGetTypeAt(position, out realType) && realType != capsuleType) {
          return;
        }
        ServerManager.TryChangeBlock(position, BlockTypes.Builtin.BuiltinBlocks.Air);
        string capsuleName = ItemTypes.IndexLookup.GetName(capsuleType);
        string blueprintName = capsuleName.Substring(0, capsuleName.Length - CapsulesModEntries.CAPSULE_SUFFIX.Length - 2);
        Chat.Send(causedBy, string.Format("Starting to build '{0}' at {1}", blueprintName, position));
        List<BlueprintTodoBlock> blocks;
        if (ManagerBlueprints.blueprints.TryGetValue(blueprintName, out blocks)) {
          int placed = 0, removed = 0, failed = 0;
          ushort bluetype = ItemTypes.IndexLookup.GetIndex(blueprintName + capsuleName.Substring(capsuleName.Length - 2));
          foreach (BlueprintTodoBlock block in blocks) {
            Vector3Int realPosition = block.GetWorldPosition(blueprintName, position, bluetype);
            if (ServerManager.TryChangeBlock(realPosition, ItemTypes.IndexLookup.GetIndex(block.typename))) {
              if (block.typename.Equals("air")) {
                removed++;
              } else {
                placed++;
              }
            } else {
              failed++;
            }
          }
          Chat.Send(causedBy, string.Format("Completed '{0}' at {1} with {2} placed, {3} removed and {4} failed blocks", blueprintName, position, placed, removed, failed));
        } else {
          Chat.Send(causedBy, string.Format("Blueprint '{0}' not found", blueprintName));
        }
      }, 2.0);
    }
  }

  public class CapsuleChatCommand : ChatCommands.IChatCommand
  {
    public bool IsCommand(string chat)
    {
      return chat.StartsWith("/capsule ");
    }

    public bool TryDoCommand(Players.Player causedBy, string chattext)
    {
      if (causedBy == null) {
        return true;
      } else if (!PermissionsManager.CheckAndWarnPermission(causedBy, CapsulesModEntries.CAPSULE_PERMISSION)) {
        return true;
      }
      int amount = 1;
      var matched = Regex.Match(chattext, @"/capsule (?<name>.+) (?<amount>\d+)");
      if (!matched.Success) {
        matched = Regex.Match(chattext, @"/capsule (?<name>.+)");
        if (!matched.Success) {
          Chat.Send(causedBy, "Command didn't match, use /capsule name amount");
          return true;
        }
      } else {
        amount = Int32.Parse(matched.Groups["amount"].Value);
      }
      string blueprintName = matched.Groups["name"].Value;
      string blueprintFullname = ManagerBlueprints.BLUEPRINTS_PREFIX + blueprintName;
      if (!ManagerBlueprints.blueprints.ContainsKey(blueprintFullname)) {
        Chat.Send(causedBy, string.Format("Blueprint '{0}' not known", blueprintName));
        return true;
      }
      Stockpile.GetStockPile(causedBy).Add(ItemTypes.IndexLookup.GetIndex(blueprintFullname + CapsulesModEntries.CAPSULE_SUFFIX), amount);
      Chat.Send(causedBy, string.Format("Added {0} emperor capsule '{1}' to your stockpile", amount, blueprintName));
      return true;
    }
  }
}
