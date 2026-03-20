using System.Collections.Generic;
using Shion.SDK.Core;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class JsonModuleRepository : IModuleRepository
    {
        private readonly Dictionary<string, Module> _modules;
        [System.Serializable]
        private class Wrapper
        {
            public List<ModuleDTO> modules;
        }
        [System.Serializable]
        private class ModuleDTO
        {
            public string id;
            public string name;
            public string version;
            public string state;
            public string category;
            public string[] symbols;
            public MethodsDTO methods;
            public List<DependencyDTO> dependencies;
        }
        [System.Serializable]
        private class MethodsDTO
        {
            public UpmMethodDTO upm;
            public GitMethodDTO git;
            public UnityPackageMethodDTO unitypackage;
        }
        [System.Serializable]
        private class UpmMethodDTO
        {
            public string id;
            public string source;
            public string path;
        }
        [System.Serializable]
        private class GitMethodDTO
        {
            public string url;
            public string localPath;
        }
        [System.Serializable]
        private class UnityPackageMethodDTO
        {
            public string repo;
            public string localPath;
            public string[] rootFolders;
        }
        [System.Serializable]
        private class DependencyDTO
        {
            public string id;
            public string version;
        }
        public JsonModuleRepository()
        {
            _modules = new Dictionary<string, Module>();
            var textAsset = Resources.Load<TextAsset>("modules");
            if (textAsset == null)
            {
                Debug.LogError("[ShionSDK] Invalid modules configuration: Resources TextAsset 'modules' not found.");
                return;
            }
            var wrapper = JsonUtility.FromJson<Wrapper>(textAsset.text);
            if (wrapper == null || wrapper.modules == null)
            {
                Debug.LogError("[ShionSDK] Invalid modules configuration: JSON in 'modules' is null or has no 'modules' array.");
                return;
            }
            foreach (var dto in wrapper.modules)
            {
                if (string.IsNullOrEmpty(dto.id) || string.IsNullOrEmpty(dto.name))
                {
                    Debug.LogError("[ShionSDK] Invalid module entry in 'modules' JSON: id/name is missing or empty. Skipping this entry.");
                    continue;
                }
                var deps = new List<Dependency>();
                if (dto.dependencies != null)
                {
                    foreach (var d in dto.dependencies)
                    {
                        var reqVer = string.IsNullOrEmpty(d.version) ? null : d.version.Trim();
                        deps.Add(new Dependency(new ModuleId(d.id), reqVer));
                    }
                }
                var state = (ModuleState)System.Enum.Parse(typeof(ModuleState), dto.state ?? "Stable");
                var category = System.Enum.TryParse<Shion.SDK.Core.ModuleCategory>(dto.category ?? "Other", true, out var cat) ? cat : Shion.SDK.Core.ModuleCategory.Other;
                var gitUrl = dto.methods != null && dto.methods.git != null ? dto.methods.git.url ?? "" : "";
                var localPath = dto.methods != null && dto.methods.git != null ? dto.methods.git.localPath ?? "" : "";
                var upmId = dto.methods != null && dto.methods.upm != null ? dto.methods.upm.id ?? "" : "";
                var upmSource = dto.methods != null && dto.methods.upm != null ? dto.methods.upm.source ?? "" : "";
                var upmPath = dto.methods != null && dto.methods.upm != null ? dto.methods.upm.path ?? "" : "";
                var unityRepo = dto.methods != null && dto.methods.unitypackage != null ? dto.methods.unitypackage.repo ?? "" : "";
                var unityPackageLocalPath = dto.methods != null && dto.methods.unitypackage != null ? dto.methods.unitypackage.localPath ?? "" : "";
                var unityRoots = dto.methods != null && dto.methods.unitypackage != null && dto.methods.unitypackage.rootFolders != null
                    ? dto.methods.unitypackage.rootFolders
                    : System.Array.Empty<string>();
                var symbols = dto.symbols != null && dto.symbols.Length > 0
                    ? (IReadOnlyList<string>)new List<string>(dto.symbols)
                    : (IReadOnlyList<string>)System.Array.Empty<string>();
                var module = new Module(
                    new ModuleId(dto.id),
                    dto.name,
                    string.IsNullOrWhiteSpace(dto.version) ? default : SemanticVersion.Parse(dto.version),
                    gitUrl,
                    localPath,
                    upmId,
                    upmSource,
                    upmPath,
                    unityRepo,
                    unityPackageLocalPath,
                    unityRoots,
                    state,
                    category,
                    deps,
                    symbols);
                _modules.Add(dto.id, module);
            }
        }
        public Module Get(ModuleId id) => _modules.TryGetValue(id.Value, out var m) ? m : null;
        public IEnumerable<Module> GetAll() => _modules.Values;
    }
}