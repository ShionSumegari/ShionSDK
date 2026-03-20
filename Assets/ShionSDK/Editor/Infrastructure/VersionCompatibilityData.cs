using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Shion.SDK.Editor
{
    [Serializable]
    internal class VersionCompatibilityFileDTO
    {
        public List<CompatEntryDTO> compatEntries = new List<CompatEntryDTO>();
        public List<SelectionEntryDTO> selectionEntries = new List<SelectionEntryDTO>();
    }
    [Serializable]
    internal class CompatEntryDTO
    {
        public string rootId;
        public string rootVersion;
        public string depId;
        public List<string> versions = new List<string>();
    }
    [Serializable]
    internal class SelectionEntryDTO
    {
        public string rootId;
        public string rootVersion;
        public string depId;
        public string version;
    }
    internal static class VersionCompatibilityRepository
    {
        private static string PathFile => ShionSDKConstants.Paths.VersionCompatibilityFullPath;
        private static bool _loaded;
        private static Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>> _compat;
        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> _selections;
        [Serializable]
        private class DepCompatEntry
        {
            public string rootVersion;
            public List<string> versions = new List<string>();
        }
        [Serializable]
        private class DepSelectionEntry
        {
            public string rootVersion;
            public string version;
        }
        [Serializable]
        private class DepWithCompatDTO
        {
            public string id;
            public List<DepCompatEntry> compatibility;
            public List<DepSelectionEntry> selections;
        }
        [Serializable]
        private class ModuleWithCompatDTO
        {
            public string id;
            public List<DepWithCompatDTO> dependencies;
        }
        [Serializable]
        private class ModulesWrapperDTO
        {
            public List<ModuleWithCompatDTO> modules;
        }
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _compat = new Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>();
            _selections = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            try
            {
                if (File.Exists(PathFile))
                {
                    LoadFromNewFile();
                }
                else
                {
                    MigrateFromModulesJsonIfNeeded();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ShionSDKConstants.LogPrefix} VersionCompatibilityRepository load failed: {ex.Message}");
            }
        }
        private static void LoadFromNewFile()
        {
            var json = File.ReadAllText(PathFile);
            if (string.IsNullOrEmpty(json)) return;
            var dto = JsonUtility.FromJson<VersionCompatibilityFileDTO>(json);
            if (dto == null) return;
            if (dto.compatEntries != null)
            {
                foreach (var e in dto.compatEntries)
                {
                    if (string.IsNullOrEmpty(e?.rootId) || string.IsNullOrEmpty(e.depId)) continue;
                    SetCompatInMemory(e.rootId, e.rootVersion ?? "", e.depId, e.versions ?? new List<string>());
                }
            }
            if (dto.selectionEntries != null)
            {
                foreach (var e in dto.selectionEntries)
                {
                    if (string.IsNullOrEmpty(e?.rootId) || string.IsNullOrEmpty(e.depId) || string.IsNullOrEmpty(e.version)) continue;
                    SetSelectionInMemory(e.rootId, e.rootVersion ?? "", e.depId, e.version);
                }
            }
        }
        private static void MigrateFromModulesJsonIfNeeded()
        {
            var modulesPath = Path.Combine(Application.dataPath, "ShionSDK", "Resources", "modules.json");
            if (!File.Exists(modulesPath)) return;
            var json = File.ReadAllText(modulesPath);
            if (string.IsNullOrEmpty(json)) return;
            var wrapper = JsonUtility.FromJson<ModulesWrapperDTO>(json);
            if (wrapper?.modules == null) return;
            foreach (var mod in wrapper.modules)
            {
                var rootId = mod?.id ?? "";
                if (string.IsNullOrEmpty(rootId) || mod.dependencies == null) continue;
                foreach (var dep in mod.dependencies)
                {
                    var depId = dep?.id ?? "";
                    if (string.IsNullOrEmpty(depId)) continue;
                    if (dep.compatibility != null)
                    {
                        foreach (var ce in dep.compatibility)
                        {
                            if (ce?.versions == null || ce.versions.Count == 0) continue;
                            SetCompatInMemory(rootId, ce.rootVersion ?? "", depId, ce.versions);
                        }
                    }
                    if (dep.selections != null)
                    {
                        foreach (var se in dep.selections)
                        {
                            if (se == null || string.IsNullOrEmpty(se.version)) continue;
                            SetSelectionInMemory(rootId, se.rootVersion ?? "", depId, se.version);
                        }
                    }
                }
            }
            Save();
        }
        private static void SetCompatInMemory(string rootId, string rootVersion, string depId, List<string> versions)
        {
            if (!_compat.TryGetValue(rootId, out var byVer)) _compat[rootId] = byVer = new Dictionary<string, Dictionary<string, List<string>>>();
            if (!byVer.TryGetValue(rootVersion, out var byDep)) byVer[rootVersion] = byDep = new Dictionary<string, List<string>>();
            byDep[depId] = versions;
        }
        private static void SetSelectionInMemory(string rootId, string rootVersion, string depId, string version)
        {
            if (!_selections.TryGetValue(rootId, out var byVer)) _selections[rootId] = byVer = new Dictionary<string, Dictionary<string, string>>();
            if (!byVer.TryGetValue(rootVersion, out var byDep)) byVer[rootVersion] = byDep = new Dictionary<string, string>();
            byDep[depId] = version;
        }
        public static List<string> GetCompatibleDepVersions(string rootId, string rootVersion, string depId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(rootId) || string.IsNullOrEmpty(depId)) return null;
            var verKey = (rootVersion ?? "").Trim();
            var altKey = string.IsNullOrEmpty(verKey) ? "" : (verKey.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? verKey.Substring(1) : "v" + verKey);
            if (_compat.TryGetValue(rootId, out var byVer))
            {
                foreach (var key in new[] { verKey, altKey, "" })
                {
                    if (byVer.TryGetValue(key, out var byDep) && byDep != null && byDep.TryGetValue(depId, out var list) && list != null && list.Count > 0)
                        return new List<string>(list);
                }
            }
            return null;
        }
        public static bool TryGetSelection(string rootId, string rootVersion, string depId, out string version)
        {
            version = null;
            EnsureLoaded();
            if (string.IsNullOrEmpty(rootId) || string.IsNullOrEmpty(depId)) return false;
            var verKey = (rootVersion ?? "").Trim();
            var altKey = string.IsNullOrEmpty(verKey) ? "" : (verKey.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? verKey.Substring(1) : "v" + verKey);
            if (!_selections.TryGetValue(rootId, out var byVer)) return false;
            foreach (var key in new[] { verKey, altKey, "" })
            {
                if (byVer.TryGetValue(key, out var byDep) && byDep != null && byDep.TryGetValue(depId, out version) && !string.IsNullOrEmpty(version))
                    return true;
            }
            return false;
        }
        public static void SetSelection(string rootId, string rootVersion, string depId, string version)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(rootId) || string.IsNullOrEmpty(depId)) return;
            var verKey = (rootVersion ?? "").Trim();
            var canonicalKey = string.IsNullOrEmpty(verKey) ? "" : (verKey.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? verKey.Substring(1) : verKey);
            SetSelectionInMemory(rootId, canonicalKey, depId, version ?? "");
            Save();
        }
        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(PathFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var dto = new VersionCompatibilityFileDTO();
                foreach (var kvRoot in _compat)
                {
                    foreach (var kvVer in kvRoot.Value)
                    {
                        foreach (var kvDep in kvVer.Value)
                        {
                            if (kvDep.Value == null || kvDep.Value.Count == 0) continue;
                            dto.compatEntries.Add(new CompatEntryDTO
                            {
                                rootId = kvRoot.Key,
                                rootVersion = kvVer.Key,
                                depId = kvDep.Key,
                                versions = kvDep.Value
                            });
                        }
                    }
                }
                foreach (var kvRoot in _selections)
                {
                    foreach (var kvVer in kvRoot.Value)
                    {
                        foreach (var kvDep in kvVer.Value)
                        {
                            if (string.IsNullOrEmpty(kvDep.Value)) continue;
                            dto.selectionEntries.Add(new SelectionEntryDTO
                            {
                                rootId = kvRoot.Key,
                                rootVersion = kvVer.Key,
                                depId = kvDep.Key,
                                version = kvDep.Value
                            });
                        }
                    }
                }
                var json = JsonUtility.ToJson(dto, true);
                File.WriteAllText(PathFile, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ShionSDKConstants.LogPrefix} VersionCompatibilityRepository save failed: {ex.Message}");
            }
        }
        public static void Clear()
        {
            _compat = new Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>();
            _selections = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            _loaded = true;
        }
        public static void Reload()
        {
            _loaded = false;
            EnsureLoaded();
        }
    }
}