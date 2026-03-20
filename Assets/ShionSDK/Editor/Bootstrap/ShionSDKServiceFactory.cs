using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public static class ShionSDKServiceFactory
    {
        public static ShionSDKServices Create()
        {
            IModuleRepository repository = new JsonModuleRepository();
            IScriptingDefineSymbolsService symbolsService = new ScriptingDefineSymbolsService();
            IModuleRegistry registry = new LocalModuleRegistry(repository, symbolsService);
            IModuleInstaller gitInstaller = new GitInstaller();
            IModuleInstaller unityPackageInstaller = new UnityPackageInstaller();
            IModuleInstaller upmInstaller = new UpmInstaller();
            IModuleInstaller compositeInstaller = new CompositeModuleInstaller(gitInstaller, upmInstaller, unityPackageInstaller);
            IVersionCompatibilityService compatService = new VersionCompatibilityService();
            var installUseCase = new InstallModuleUseCase(repository, registry, compositeInstaller);
            var uninstallUseCase = new UninstallModuleUseCase(repository, registry, compositeInstaller);
            var conflictDetector = new DependencyVersionConflictDetector(repository, registry, compatService);
            return new ShionSDKServices(
                repository,
                registry,
                compatService,
                conflictDetector,
                installUseCase,
                uninstallUseCase,
                compositeInstaller,
                gitInstaller,
                upmInstaller,
                unityPackageInstaller);
        }
    }
    public class ShionSDKServices
    {
        public ShionSDKServices(
            IModuleRepository repository,
            IModuleRegistry registry,
            IVersionCompatibilityService versionCompatibilityService,
            IDependencyVersionConflictDetector conflictDetector,
            InstallModuleUseCase installUseCase,
            UninstallModuleUseCase uninstallUseCase,
            IModuleInstaller compositeInstaller,
            IModuleInstaller gitInstaller,
            IModuleInstaller upmInstaller,
            IModuleInstaller unityPackageInstaller)
        {
            Repository = repository;
            Registry = registry;
            VersionCompatibilityService = versionCompatibilityService;
            ConflictDetector = conflictDetector;
            InstallUseCase = installUseCase;
            UninstallUseCase = uninstallUseCase;
            CompositeInstaller = compositeInstaller;
            GitInstaller = gitInstaller;
            UpmInstaller = upmInstaller;
            UnityPackageInstaller = unityPackageInstaller;
        }
        public IModuleRepository Repository { get; }
        public IModuleRegistry Registry { get; }
        public IVersionCompatibilityService VersionCompatibilityService { get; }
        public IDependencyVersionConflictDetector ConflictDetector { get; }
        public InstallModuleUseCase InstallUseCase { get; }
        public UninstallModuleUseCase UninstallUseCase { get; }
        public IModuleInstaller CompositeInstaller { get; }
        public IModuleInstaller GitInstaller { get; }
        public IModuleInstaller UpmInstaller { get; }
        public IModuleInstaller UnityPackageInstaller { get; }
    }
}