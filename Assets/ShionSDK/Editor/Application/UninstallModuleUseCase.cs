using System.Collections.Generic;
using System.Linq;
using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public class UninstallModuleUseCase
    {
        private readonly IModuleRepository _repository;
        private readonly IModuleRegistry _registry;
        private readonly IModuleInstaller _installer;
        public UninstallModuleUseCase(IModuleRepository repository, IModuleRegistry registry, IModuleInstaller installer)
        {
            _repository = repository;
            _registry = registry;
            _installer = installer;
        }
        public bool HasBlockingDependents(ModuleId id, out List<Module> dependents)
        {
            var installedIds = _registry.GetInstalledModules();
            var installedModules = installedIds.Select(i => _repository.Get(i))
                .Where(m => m != null).ToList();
            dependents = installedModules
                .Where(m => m.Dependencies != null &&
                            m.Dependencies.Any(dep => dep.Id.Value == id.Value))
                .ToList();
            return dependents.Count > 0;
        }
        public bool Execute(ModuleId id, out List<Module> dependents)
        {
            if (HasBlockingDependents(id, out dependents))
                return false;
            var module = _repository.Get(id);
            if (module == null)
                return false;
            _installer.Uninstall(module);
            _registry.MarkUninstalled(id);
            return true;
        }
    }
}