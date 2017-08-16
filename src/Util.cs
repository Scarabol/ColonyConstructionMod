using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Pipliz.JSON;

namespace ScarabolMods
{
  public static class ModLocalizationHelper
  {
    public static void localize(string localePath, string prefix)
    {
      localize(localePath, prefix, true);
    }

    public static void localize(string localePath, string keyprefix, bool verbose)
    {
      try {
        foreach (string locFileName in new string[] { "types.json", "typeuses.json", "localization.json" }) {
          string[] files = Directory.GetFiles(localePath, locFileName, SearchOption.AllDirectories);
          foreach (string filepath in files) {
            try {
              JSONNode jsonFromMod;
              if (Pipliz.JSON.JSON.Deserialize(filepath, out jsonFromMod, false)) {
                string locName = Directory.GetParent(filepath).Name;
                log(string.Format("Found mod localization file for '{0}' localization", locName), verbose);
                string patchPath = MultiPath.Combine("gamedata", "localization", locName, locFileName);
                JSONNode jsonToPatch;
                if (Pipliz.JSON.JSON.Deserialize(patchPath, out jsonToPatch, false)) {
                  foreach (KeyValuePair<string, JSONNode> entry in jsonFromMod.LoopObject()) {
                    string realkey = keyprefix + entry.Key;
                    string val = jsonFromMod.GetAs<string>(entry.Key);
                    if (!jsonToPatch.HasChild(realkey)) {
                      Pipliz.Log.Write(string.Format("translation '{0}' => '{1}' added to '{2}/{3}'. This will apply AFTER next restart!!!", realkey, val, locName, locFileName));
                    } else if (!jsonToPatch.GetAs<string>(realkey).Equals(val)) {
                      Pipliz.Log.Write(string.Format("translation '{0}' => '{1}' changed in '{2}/{3}'. This will apply AFTER next restart!!!", realkey, val, locName, locFileName));
                    }
                    jsonToPatch.SetAs(realkey, val);
                  }
                  Pipliz.JSON.JSON.Serialize(patchPath, jsonToPatch);
                  log(string.Format("Patched mod localization file '{0}/{1}' into '{2}'", locName, locFileName, patchPath), verbose);
                } else {
                  log(string.Format("Could not deserialize json from '{0}'", patchPath), verbose);
                }
              }
            } catch (Exception exception) {
              log(string.Format("Exception reading localization from {0}; {1}", filepath, exception.Message), verbose);
            }
          }
        }
      } catch (DirectoryNotFoundException) {
        log(string.Format("Localization directory not found at {0}", localePath), verbose);
      }
    }

    private static void log(string msg, bool verbose) {
      if (verbose) {
        Pipliz.Log.Write(msg);
      }
    }
  }

  public static class MultiPath
  {
    public static string Combine(params string[] pathParts)
    {
      StringBuilder result = new StringBuilder();
      foreach (string part in pathParts) {
        result.Append(part.TrimEnd('/', '\\')).Append(Path.DirectorySeparatorChar);
      }
      return result.ToString().TrimEnd(Path.DirectorySeparatorChar);
    }
  }
}