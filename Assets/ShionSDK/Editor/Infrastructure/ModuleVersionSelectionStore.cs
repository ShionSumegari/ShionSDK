using System.Collections.Generic;
using Shion.SDK.Core;
using UnityEditor;
namespace Shion.SDK.Editor
{
    internal class ModuleVersionSelectionStoreAdapter : IModuleVersionSelectionStore
    {
        private static string InstallVersionPrefix => ShionSDKConstants.EditorPrefsKeys.InstallVersionPrefix;
        private static readonly Dictionary<string, string> Versions = new();
        public void Set(ModuleId id, string version)
        {
            if (string.IsNullOrEmpty(id.Value) || string.IsNullOrEmpty(version)) return;
            Versions[id.Value] = version;
            EditorPrefs.SetString(InstallVersionPrefix + id.Value, version);
        }
        public bool TryGet(ModuleId id, out string version)
        {
            version = null;
            if (string.IsNullOrEmpty(id.Value)) return false;
            if (Versions.TryGetValue(id.Value, out version)) return true;
            version = EditorPrefs.GetString(InstallVersionPrefix + id.Value, null);
            if (!string.IsNullOrEmpty(version))
            {
                EditorPrefs.DeleteKey(InstallVersionPrefix + id.Value);
                return true;
            }
            return false;
        }
        public void Clear() => Versions.Clear();
    }
    internal static class ModuleVersionSelectionStore
    {
        private static readonly ModuleVersionSelectionStoreAdapter Instance = new ModuleVersionSelectionStoreAdapter();
        public static void Set(ModuleId id, string version) => Instance.Set(id, version);
        public static bool TryGet(ModuleId id, out string version) => Instance.TryGet(id, out version);
        public static void Clear() => Instance.Clear();
    }
}