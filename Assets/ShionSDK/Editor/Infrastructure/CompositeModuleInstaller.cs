using System.Collections.Generic;
using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public class CompositeModuleInstaller : IModuleInstaller
    {
        private readonly IReadOnlyDictionary<ModuleInstallMethod, IModuleInstaller> _installersByMethod;
        internal CompositeModuleInstaller(IReadOnlyDictionary<ModuleInstallMethod, IModuleInstaller> installersByMethod)
        {
            _installersByMethod = installersByMethod ?? new Dictionary<ModuleInstallMethod, IModuleInstaller>();
        }
        public CompositeModuleInstaller(IModuleInstaller gitInstaller, IModuleInstaller upmInstaller, IModuleInstaller unityPackageInstaller)
            : this(new Dictionary<ModuleInstallMethod, IModuleInstaller>
            {
                [ModuleInstallMethod.Git] = gitInstaller,
                [ModuleInstallMethod.Upm] = upmInstaller,
                [ModuleInstallMethod.UnityPackage] = unityPackageInstaller
            })
        {
        }
        private ModuleInstallMethod ResolveMethod(Module module)
        {
            var preferred = ModuleInstallMethodStore.Get(module.Id);
            if (preferred != ModuleInstallMethod.Auto)
            {
                if (preferred == ModuleInstallMethod.Upm && string.IsNullOrEmpty(module.UpmId))
                    preferred = ModuleInstallMethod.Auto;
                else if (preferred == ModuleInstallMethod.Git && string.IsNullOrEmpty(module.GitUrl))
                    preferred = ModuleInstallMethod.Auto;
                else if (preferred == ModuleInstallMethod.UnityPackage && string.IsNullOrEmpty(module.UnityPackageRepo) && string.IsNullOrEmpty(module.UnityPackageLocalPath))
                    preferred = ModuleInstallMethod.Auto;
                else
                    return preferred;
            }
            if (!string.IsNullOrEmpty(module.UpmId))
                return ModuleInstallMethod.Upm;
            if (!string.IsNullOrEmpty(module.GitUrl))
                return ModuleInstallMethod.Git;
            if (!string.IsNullOrEmpty(module.UnityPackageRepo) || !string.IsNullOrEmpty(module.UnityPackageLocalPath))
                return ModuleInstallMethod.UnityPackage;
            return ModuleInstallMethod.Auto;
        }
        private ModuleInstallMethod GetFallbackMethod(Module module)
        {
            if (!string.IsNullOrEmpty(module.UpmId))
                return ModuleInstallMethod.Upm;
            if (!string.IsNullOrEmpty(module.GitUrl))
                return ModuleInstallMethod.Git;
            if (!string.IsNullOrEmpty(module.UnityPackageRepo) || !string.IsNullOrEmpty(module.UnityPackageLocalPath))
                return ModuleInstallMethod.UnityPackage;
            return ModuleInstallMethod.Auto;
        }
        private void Execute(Module module, bool install)
        {
            var method = ResolveMethod(module);
            if (method == ModuleInstallMethod.Auto)
                method = GetFallbackMethod(module);
            if (method != ModuleInstallMethod.Auto && _installersByMethod.TryGetValue(method, out var installer))
            {
                if (install)
                    installer.Install(module);
                else
                    installer.Uninstall(module);
            }
        }
        public void Install(Module module) => Execute(module, install: true);
        public void Uninstall(Module module) => Execute(module, install: false);
    }
}