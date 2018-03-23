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

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterSelectedWorld, "scarabol.capsules.registertexturemappings")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.server.registertexturemappingtextures")]
    public static void AfterSelectedWorld ()
    {
      var textureMapping = new ItemTypesServer.TextureMapping (new JSONNode ());
      textureMapping.AlbedoPath = MultiPath.Combine (ConstructionModEntries.AssetsDirectory, "textures", "albedo", "capsulesTop.png");
      ItemTypesServer.SetTextureMapping (ConstructionModEntries.MOD_PREFIX + "capsuletop", textureMapping);
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.capsules.addrawtypes")]
    [ModLoader.ModCallbackDependsOn ("scarabol.blueprints.addrawtypes")]
    public static void AfterAddingBaseTypes (Dictionary<string, ItemTypesServer.ItemTypeRaw> itemTypes)
    {
      string iconFilepath = MultiPath.Combine (ConstructionModEntries.AssetsDirectory, "icons", "capsule.png");
      foreach (string blueprintTypename in ManagerBlueprints.blueprints.Keys) {
        itemTypes.Add (blueprintTypename + CAPSULE_SUFFIX, new ItemTypesServer.ItemTypeRaw (blueprintTypename + CAPSULE_SUFFIX,
          new JSONNode ()
            .SetAs ("onPlaceAudio", "woodPlace")
            .SetAs ("onRemoveAudio", "woodDeleteLight")
            .SetAs ("icon", iconFilepath)
            .SetAs ("sideall", "planks")
            .SetAs ("sidey+", ConstructionModEntries.MOD_PREFIX + "capsuletop")
            .SetAs ("isRotatable", "true")
            .SetAs ("rotatablex+", blueprintTypename + CAPSULE_SUFFIX + "x+")
            .SetAs ("rotatablex-", blueprintTypename + CAPSULE_SUFFIX + "x-")
            .SetAs ("rotatablez+", blueprintTypename + CAPSULE_SUFFIX + "z+")
            .SetAs ("rotatablez-", blueprintTypename + CAPSULE_SUFFIX + "z-")
            .SetAs ("npcLimit", "0")
        ));
        foreach (string xz in new string [] { "x+", "x-", "z+", "z-" }) {
          itemTypes.Add (blueprintTypename + CAPSULE_SUFFIX + xz, new ItemTypesServer.ItemTypeRaw (blueprintTypename + CAPSULE_SUFFIX + xz,
            new JSONNode ()
              .SetAs ("parentType", blueprintTypename + CAPSULE_SUFFIX)
          ));
        }
      }
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.capsules.registertypes")]
    public static void AfterItemTypesDefined ()
    {
      foreach (string blueprintTypename in ManagerBlueprints.blueprints.Keys) {
        ItemTypesServer.RegisterOnAdd (blueprintTypename + CAPSULE_SUFFIX, CapsuleBlockCode.OnPlaceCapsule);
      }
      ChatCommands.CommandManager.RegisterCommand (new CapsuleChatCommand ());
    }
  }

  static class CapsuleBlockCode
  {
    public static void OnPlaceCapsule (Vector3Int position, ushort capsuleType, Players.Player causedBy)
    {
      if (!PermissionsManager.CheckAndWarnPermission (causedBy, CapsulesModEntries.CAPSULE_PERMISSION)) {
        ServerManager.TryChangeBlock (position, BlockTypes.Builtin.BuiltinBlocks.Air, Players.GetPlayer (NetworkID.Server));
        return;
      }
      ThreadManager.InvokeOnMainThread (delegate () {
        ushort realType;
        if (World.TryGetTypeAt (position, out realType) && realType != capsuleType) {
          return;
        }
        ServerManager.TryChangeBlock (position, BlockTypes.Builtin.BuiltinBlocks.Air, Players.GetPlayer (NetworkID.Server));
        string capsuleName = ItemTypes.IndexLookup.GetName (capsuleType);
        string blueprintName = capsuleName.Substring (0, capsuleName.Length - CapsulesModEntries.CAPSULE_SUFFIX.Length - 2);
        Chat.Send (causedBy, string.Format ("Starting to build '{0}' at {1}", blueprintName, position));
        List<BlueprintTodoBlock> blocks;
        if (ManagerBlueprints.blueprints.TryGetValue (blueprintName, out blocks)) {
          int placed = 0, removed = 0, failed = 0;
          ushort bluetype;
          if (ItemTypes.IndexLookup.TryGetIndex (blueprintName + capsuleName.Substring (capsuleName.Length - 2), out bluetype)) {
            foreach (BlueprintTodoBlock block in blocks) {
              Vector3Int realPosition = block.GetWorldPosition (blueprintName, position, bluetype);
              string baseTypename = TypeHelper.RotatableToBasetype (block.typename);
              string rotatedTypename = block.typename;
              if (!baseTypename.Equals (block.typename)) {
                Vector3Int jobVec = TypeHelper.RotatableToVector (capsuleName);
                Vector3Int blockVec = TypeHelper.RotatableToVector (block.typename);
                Vector3Int combinedVec = new Vector3Int (-jobVec.z * blockVec.x + jobVec.x * blockVec.z, 0, jobVec.x * blockVec.x + jobVec.z * blockVec.z);
                rotatedTypename = baseTypename + TypeHelper.VectorToXZ (combinedVec);
              }
              ushort rotatedType;
              if (realPosition.y > 0 && ItemTypes.IndexLookup.TryGetIndex (rotatedTypename, out rotatedType) && ServerManager.TryChangeBlock (realPosition, rotatedType, Players.GetPlayer (NetworkID.Server))) {
                if (block.typename.Equals ("air")) {
                  removed++;
                } else {
                  placed++;
                }
              } else {
                failed++;
              }
            }
          }
          Chat.Send (causedBy, string.Format ("Completed '{0}' at {1} with {2} placed, {3} removed and {4} failed blocks", blueprintName, position, placed, removed, failed));
        } else {
          Chat.Send (causedBy, string.Format ("Blueprint '{0}' not found", blueprintName));
        }
      }, 2.0);
    }
  }

  public class CapsuleChatCommand : ChatCommands.IChatCommand
  {
    public bool IsCommand (string chat)
    {
      return chat.StartsWith ("/capsule ");
    }

    public bool TryDoCommand (Players.Player causedBy, string chattext)
    {
      if (causedBy == null) {
        return true;
      } else if (!PermissionsManager.CheckAndWarnPermission (causedBy, CapsulesModEntries.CAPSULE_PERMISSION)) {
        return true;
      }
      int amount = 1;
      var matched = Regex.Match (chattext, @"/capsule (?<name>.+) (?<amount>\d+)");
      if (!matched.Success) {
        matched = Regex.Match (chattext, @"/capsule (?<name>.+)");
        if (!matched.Success) {
          Chat.Send (causedBy, "Command didn't match, use /capsule name amount");
          return true;
        }
      } else {
        amount = Int32.Parse (matched.Groups ["amount"].Value);
      }
      string blueprintName = matched.Groups ["name"].Value;
      string blueprintFullname = ManagerBlueprints.BLUEPRINTS_PREFIX + blueprintName;
      if (!ManagerBlueprints.blueprints.ContainsKey (blueprintFullname)) {
        Chat.Send (causedBy, string.Format ("Blueprint '{0}' not known", blueprintName));
        return true;
      }
      Stockpile.GetStockPile (causedBy).Add (ItemTypes.IndexLookup.GetIndex (blueprintFullname + CapsulesModEntries.CAPSULE_SUFFIX), amount);
      Chat.Send (causedBy, string.Format ("Added {0} emperor capsule '{1}' to your stockpile", amount, blueprintName));
      return true;
    }
  }
}
