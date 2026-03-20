using System.Collections.Generic;
using System.Linq;
using Shion.SDK.Core;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public sealed class ScriptingDefineSymbolsService : IScriptingDefineSymbolsService
    {
        public void AddSymbolsForModule(Module module) => ScriptingDefineSymbolsHelper.AddSymbolsForModule(module);
        public void RemoveSymbolsForModule(Module module) => ScriptingDefineSymbolsHelper.RemoveSymbolsForModule(module);
    }
    internal static class ScriptingDefineSymbolsHelper
    {
        private static BuildTargetGroup[] GetAllBuildTargetGroups()
        {
            return new[]
            {
                BuildTargetGroup.Standalone,
                BuildTargetGroup.iOS,
                BuildTargetGroup.Android,
                BuildTargetGroup.WebGL,
                BuildTargetGroup.tvOS
            };
        }
        public static void AddSymbolsForModule(Module module)
        {
            if (module?.Symbols == null || module.Symbols.Count == 0)
                return;
            foreach (var group in GetAllBuildTargetGroups())
            {
                try
                {
                    var defines = GetScriptingDefineSymbols(group);
                    var toAdd = module.Symbols.Where(s => !string.IsNullOrWhiteSpace(s) && !defines.Contains(s.Trim())).ToList();
                    if (toAdd.Count == 0)
                        continue;
                    foreach (var sym in toAdd)
                        defines.Add(sym.Trim());
                    SetScriptingDefineSymbols(group, defines);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ShionSDK] Failed to add symbols for {group}: {e.Message}");
                }
            }
        }
        public static void RemoveSymbolsForModule(Module module)
        {
            if (module?.Symbols == null || module.Symbols.Count == 0)
                return;
            var toRemove = new HashSet<string>(module.Symbols.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            foreach (var group in GetAllBuildTargetGroups())
            {
                try
                {
                    var defines = GetScriptingDefineSymbols(group);
                    var modified = defines.RemoveAll(d => toRemove.Contains(d.Trim())) > 0;
                    if (modified)
                        SetScriptingDefineSymbols(group, defines);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ShionSDK] Failed to remove symbols for {group}: {e.Message}");
                }
            }
        }
        private static List<string> GetScriptingDefineSymbols(BuildTargetGroup group)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            return (defines ?? "").Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();
        }
        private static void SetScriptingDefineSymbols(BuildTargetGroup group, List<string> defines)
        {
            var joined = string.Join(";", defines.Where(d => !string.IsNullOrEmpty(d)));
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joined);
        }
    }
}