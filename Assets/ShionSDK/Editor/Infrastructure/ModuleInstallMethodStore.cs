using System.Collections.Generic;
using Shion.SDK.Core;
using UnityEditor;
namespace Shion.SDK.Editor
{
    internal enum ModuleInstallMethod
    {
        Auto = 0,
        Upm = 1,
        Git = 2,
        UnityPackage = 3
    }
    internal class ModuleInstallMethodStoreAdapter
    {
        private static string InstallMethodPrefix => ShionSDKConstants.EditorPrefsKeys.InstallMethodPrefix;
        private static readonly Dictionary<string, ModuleInstallMethod> Methods = new();
        public void Set(ModuleId id, ModuleInstallMethod method)
        {
            if (string.IsNullOrEmpty(id.Value)) return;
            if (method == ModuleInstallMethod.Auto)
            {
                Methods.Remove(id.Value);
                EditorPrefs.DeleteKey(InstallMethodPrefix + id.Value);
                return;
            }
            Methods[id.Value] = method;
            EditorPrefs.SetInt(InstallMethodPrefix + id.Value, (int)method);
        }
        public ModuleInstallMethod Get(ModuleId id)
        {
            if (string.IsNullOrEmpty(id.Value)) return ModuleInstallMethod.Auto;
            if (Methods.TryGetValue(id.Value, out var method))
                return method;
            var key = InstallMethodPrefix + id.Value;
            if (EditorPrefs.HasKey(key))
            {
                var stored = (ModuleInstallMethod)EditorPrefs.GetInt(key, 0);
                Methods[id.Value] = stored;
                return stored;
            }
            return ModuleInstallMethod.Auto;
        }
        public void Clear()
        {
            Methods.Clear();
        }
    }
    internal static class ModuleInstallMethodStore
    {
        private static readonly ModuleInstallMethodStoreAdapter Instance = new ModuleInstallMethodStoreAdapter();
        public static void Set(ModuleId id, ModuleInstallMethod method) => Instance.Set(id, method);
        public static ModuleInstallMethod Get(ModuleId id) => Instance.Get(id);
        public static void Clear() => Instance.Clear();
    }
}