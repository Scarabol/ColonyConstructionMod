using System;
using System.IO;
using System.Collections.Generic;
using Pipliz.JSON;

namespace ScarabolMods
{
  public static class ModLocalizationHelper
  {
    public static void localize(string localePath)
    {
      localize(localePath, true);
    }

    public static void localize(string localePath, bool verbose)
    {
      string[] files = Directory.GetFiles(localePath, "types.json", SearchOption.AllDirectories);
      foreach (string filepath in files) {
        try {
          JSONNode jsonFromMod;
          if (Pipliz.JSON.JSON.Deserialize(filepath, out jsonFromMod, false)) {
            string locName = Directory.GetParent(filepath).Name;
            log(string.Format("Found mod localization file for '{0}' localization", locName), verbose);
            string patchPath = string.Format("gamedata/localization/{0}/types.json", locName);
            JSONNode jsonToPatch;
            if (Pipliz.JSON.JSON.Deserialize(patchPath, out jsonToPatch, false)) {
              foreach (KeyValuePair<string, JSONNode> entry in jsonFromMod.LoopObject()) {
                string val = jsonFromMod.GetAs<string>(entry.Key);
                if (!jsonToPatch.HasChild(entry.Key)) {
                  Pipliz.Log.Write(string.Format("Added translation '{0}' => '{1}' to '{2}'. This will only work AFTER a restart!!!", entry.Key, val, locName));
                }
                jsonToPatch.SetAs(entry.Key, val);
              }
              Pipliz.JSON.JSON.Serialize(patchPath, jsonToPatch);
              log(string.Format("Patched mod localization file '{0}/types.json' into '{1}'", locName, patchPath), verbose);
            } else {
              log(string.Format("Could not deserialize json from '{0}'", patchPath), verbose);
            }
          }
        } catch (Exception exception) {
          log(string.Format("Exception reading localization from {0}; {1}", filepath, exception.Message), verbose);
        }
      }
    }

    private static void log(string msg, bool verbose) {
      if (verbose) {
        Pipliz.Log.Write(msg);
      }
    }
  }
}
