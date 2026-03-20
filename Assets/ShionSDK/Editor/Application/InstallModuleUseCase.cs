using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public class InstallModuleUseCase
    {
        private readonly IModuleRepository repository;
        private readonly IModuleRegistry registry;
        private readonly IModuleInstaller installer;
        private readonly DependencyResolver resolver;
        public InstallModuleUseCase(IModuleRepository repository, IModuleRegistry registry, IModuleInstaller installer)
        {
            this.repository = repository;
            this.registry = registry;
            this.installer = installer;
            resolver = new DependencyResolver(repository);
        }
        public void Execute(ModuleId id)
        {
            var root = repository.Get(id);
            var plan = resolver.BuildInstallPlan(root);
            foreach (var module in plan.OrderedModules)
            {
                if (!registry.IsInstalled(module.Id))
                {
                    installer.Install(module);
                    registry.MarkInstalled(module);
                }
            }
        }
    }
}