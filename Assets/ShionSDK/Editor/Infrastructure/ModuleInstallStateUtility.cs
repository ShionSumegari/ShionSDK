using Shion.SDK.Core;
using System.IO;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public static class ModuleInstallStateUtility
    {
        public static bool IsActuallyInstalled(Module module, IModuleRegistry registry)
        {
            if (module == null)
                return false;
            if (!string.IsNullOrEmpty(module.UpmId))
                return UpmStateUtility.IsInstalled(module.UpmId);
            if ((!string.IsNullOrEmpty(module.UnityPackageRepo) || !string.IsNullOrEmpty(module.UnityPackageLocalPath)) &&
                module.UnityPackageRootFolders != null &&
                module.UnityPackageRootFolders.Count > 0)
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                foreach (var root in module.UnityPackageRootFolders)
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    var normalized = root.Replace("\\", "/");
                    var path = normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                        ? Path.Combine(projectRoot, normalized)
                        : Path.Combine(projectRoot, "Assets", normalized.TrimStart('/'));
                    if (Directory.Exists(path) || File.Exists(path))
                        return true;
                }
            }
            return registry.IsInstalled(module.Id);
        }
    }
}