using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public static class UpmStateUtility
    {
        public static bool IsInstalled(string upmId)
        {
            if (string.IsNullOrEmpty(upmId))
                return false;
            return GetInstalledUpmIds(new[] { upmId }).Contains(upmId);
        }
        public static bool TryGetInstalledVersion(string upmId, out string version)
        {
            version = null;
            if (string.IsNullOrEmpty(upmId))
                return false;
#if UNITY_EDITOR
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var manifestPath = Path.Combine(projectRoot, "Packages/manifest.json");
                if (!File.Exists(manifestPath))
                    return false;
                var json = File.ReadAllText(manifestPath);
                var escapedId = Regex.Escape(upmId);
                var pattern = $"\"{escapedId}\"\\s*:\\s*\"([^\"]+)\"";
                var match = Regex.Match(json, pattern);
                if (!match.Success || match.Groups.Count < 2)
                    return false;
                var value = match.Groups[1].Value;
                if (string.IsNullOrEmpty(value))
                    return false;
                var hashIndex = value.IndexOf('#');
                version = hashIndex >= 0 && hashIndex < value.Length - 1
                    ? value.Substring(hashIndex + 1).Trim()
                    : value.Trim();
                return !string.IsNullOrEmpty(version);
            }
            catch { }
#endif
            return false;
        }
        public static HashSet<string> GetInstalledUpmIds(IEnumerable<string> upmIdsToCheck)
        {
            var result = new HashSet<string>();
#if UNITY_EDITOR
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var manifestPath = Path.Combine(projectRoot, "Packages/manifest.json");
                if (!File.Exists(manifestPath))
                    return result;
                var json = File.ReadAllText(manifestPath);
                foreach (var id in upmIdsToCheck.Where(id => !string.IsNullOrEmpty(id)))
                {
                    if (json.Contains($"\"{id}\""))
                        result.Add(id);
                }
            }
            catch {}
#endif
            return result;
        }
    }
}