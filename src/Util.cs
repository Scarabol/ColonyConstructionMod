using System.IO;
using System.Text;

namespace ScarabolMods
{
  public class MultiPath
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