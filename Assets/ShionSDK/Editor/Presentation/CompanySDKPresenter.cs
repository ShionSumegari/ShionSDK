using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shion.SDK.Core;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    internal class CompanySDKPresenter
    {
        private readonly CompanySDKViewModel _vm;
        private readonly Action _repaint;
        private IModuleRepository _repository;
        private IModuleRegistry _registry;
        private IVersionCompatibilityService _compatService;
        private IDependencyVersionConflictDetector _conflictDetector;
        private UninstallModuleUseCase _uninstallUseCase;
        private InstallQueueRunner _installQueueRunner;
        private UninstallQueueRunner _uninstallQueueRunner;
        private IModuleInstaller _compositeInstaller;
        private readonly Func<ShionSDKServices> _createServices;
        private readonly CompanySDKVersionFlowPresenter _versionFlowPresenter;
        private readonly CompanySDKInstallFlowPresenter _installFlowPresenter;
        public CompanySDKPresenter(CompanySDKViewModel vm, Action repaint)
            : this(vm, repaint, CompanySDKPresenterDependencies.CreateDefault())
        {
        }
        internal CompanySDKPresenter(
            CompanySDKViewModel vm,
            Action repaint,
            CompanySDKPresenterDependencies dependencies)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _repaint = repaint ?? (() => { });
            _createServices = dependencies.CreateServices ?? ShionSDKServiceFactory.Create;
            var gitVersionCacheStore = dependencies.GitVersionCacheStore ?? new EditorPrefsGitVersionCacheStore();
            var installCompatibilityGuard = dependencies.InstallCompatibilityGuard ?? new NoOpInstallCompatibilityGuard();
            _versionFlowPresenter = new CompanySDKVersionFlowPresenter(
                _vm,
                _repaint,
                gitVersionCacheStore,
                () => _repository,
                () => _registry,
                () => _compatService);
            _installFlowPresenter = new CompanySDKInstallFlowPresenter(
                _vm,
                _repaint,
                installCompatibilityGuard,
                SetWarning,
                () => _repository,
                () => _registry,
                () => _compatService,
                () => _conflictDetector,
                () => _uninstallUseCase,
                () => _installQueueRunner,
                () => _uninstallQueueRunner);
            InitializeServices();
        }
        private void InitializeServices()
        {
            var services = _createServices();
            _repository = services.Repository;
            _registry = services.Registry;
            _compatService = services.VersionCompatibilityService;
            _conflictDetector = services.ConflictDetector;
            _uninstallUseCase = services.UninstallUseCase;
            _compositeInstaller = services.CompositeInstaller;
            _installQueueRunner = new InstallQueueRunner(
                _repository,
                _registry,
                _compositeInstaller,
                _vm.OperationStatus,
                OnInstallErrorFromQueue,
                _compatService);
            _uninstallQueueRunner = new UninstallQueueRunner(
                _repository,
                _registry,
                _uninstallUseCase,
                _vm.OperationStatus);
        }
        private void OnInstallErrorFromQueue(Module module, string error)
        {
            SetWarning($"Failed to install '{module.Name}': {error}");
            Debug.LogError($"[ShionSDK] {error}");
        }
        public void OnEnable()
        {
            _installFlowPresenter.OnEnable();
        }
        public IModuleRepository Repository => _repository;
        public void EnsureStyles()
        {
            _versionFlowPresenter.EnsureStyles();
        }
        public void RefreshInstalledStateCache(List<Module> modules)
        {
            _versionFlowPresenter.RefreshInstalledStateCache(modules);
        }
        public void TryLoadVersionCacheFromEditorPrefs()
        {
            _versionFlowPresenter.TryLoadVersionCacheFromEditorPrefs();
        }
        public void SaveVersionCacheToEditorPrefs()
        {
            _versionFlowPresenter.SaveVersionCacheToEditorPrefs();
        }
        public void ClearVersionCacheEditorPrefs()
        {
            _versionFlowPresenter.ClearVersionCacheEditorPrefs();
        }
        public void EnsureGitInstallableVersionsLoaded(List<Module> modules, System.Action onComplete = null)
        {
            _versionFlowPresenter.EnsureGitInstallableVersionsLoaded(modules, onComplete);
        }
        public void OnRefresh()
        {
            var services = _createServices();
            _repository = services.Repository;
            _registry = services.Registry;
            _compatService = services.VersionCompatibilityService;
            _conflictDetector = services.ConflictDetector;
            _uninstallUseCase = services.UninstallUseCase;
            _compositeInstaller = services.CompositeInstaller;
            _installQueueRunner = new InstallQueueRunner(_repository, _registry, _compositeInstaller, _vm.OperationStatus, OnInstallErrorFromQueue, _compatService);
            _uninstallQueueRunner = new UninstallQueueRunner(_repository, _registry, _uninstallUseCase, _vm.OperationStatus);
            _vm.OperationStatus.Clear();
            _vm.DependenciesFoldout.Clear();
            _vm.PendingBatchUninstall = null;
            _vm.CachedUpmInstalledIds = null;
            _vm.CachedRegistryIds = null;
            _vm.SelectedVersionOverrides.Clear();
            _vm.GitInstallableVersionsByModuleId.Clear();
            _vm.GitVersionsScanned = false;
            ModuleVersionSelectionStore.Clear();
            _compatService?.Reload();
            ClearVersionCacheEditorPrefs();
            _repaint();
        }
        public string GetModuleVersionForDisplay(Module module, bool installed, ModuleOperationStatus opStatus)
        {
            return _versionFlowPresenter.GetModuleVersionForDisplay(module, installed, opStatus);
        }
        public string GetDepVersionLabel(Module rootModule, Dependency dep, bool depInstalled, bool rootInstalled, string rootVersion)
        {
            return _versionFlowPresenter.GetDepVersionLabel(rootModule, dep, depInstalled, rootInstalled, rootVersion);
        }
        private void SetWarning(string message)
        {
            _vm.WarningMessage = message;
            _vm.WarningExpireTime = EditorApplication.timeSinceStartup + ShionSDKConstants.WarningDisplaySeconds;
        }
        public List<string> GetCompatibleDepVersionsForSelector(Module rootModule, string rootVersion, Dependency dep)
        {
            return _versionFlowPresenter.GetCompatibleDepVersionsForSelector(rootModule, rootVersion, dep);
        }
        public bool IsDepVersionSelectable(Module rootModule, Dependency dep, bool depInstalled, bool rootInstalled, string rootVersion, ModuleOperationStatus rootOpStatus)
        {
            return _versionFlowPresenter.IsDepVersionSelectable(rootModule, dep, depInstalled, rootInstalled, rootVersion, rootOpStatus);
        }
        public void ShowDepVersionSelectionPopup(Module rootModule, string rootVersion, Dependency dep, string currentVersion, Rect activatorRect)
        {
            _versionFlowPresenter.ShowDepVersionSelectionPopup(rootModule, rootVersion, dep, currentVersion, activatorRect);
        }
        public void OnInstallClicked(Module module)
        {
            _installFlowPresenter.OnInstallClicked(module);
        }
        public void OnUninstallClicked(Module module)
        {
            _installFlowPresenter.OnUninstallClicked(module);
        }
        public void ShowVersionSelectionPopup(Module module, string currentVersionCore, Rect activatorRect)
        {
            _versionFlowPresenter.ShowVersionSelectionPopup(module, currentVersionCore, activatorRect);
        }
        public void OnBatchUninstallAccept()
        {
            _installFlowPresenter.OnBatchUninstallAccept();
        }
        public void OnCancelBatch()
        {
            _installFlowPresenter.OnCancelBatch();
        }
        public static string GetGitHubUrlForModule(Module module)
        {
            if (module == null) return null;
            if (!string.IsNullOrEmpty(module.UpmSource) && module.UpmSource.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0)
                return module.UpmSource;
            if (!string.IsNullOrEmpty(module.GitUrl) && module.GitUrl.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0)
                return module.GitUrl;
            if (!string.IsNullOrEmpty(module.UnityPackageRepo))
            {
                var spec = module.UnityPackageRepo.Trim();
                if (spec.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    return spec;
                return $"https://github.com/{spec}.git";
            }
            return null;
        }
        public static string TruncateWithEllipsis(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
            if (maxChars <= 3) return text.Substring(0, maxChars);
            return text.Substring(0, maxChars - 3) + "...";
        }
        public void TickQueues()
        {
            _installFlowPresenter.TickQueues();
        }
    }
    internal sealed class CompanySDKVersionFlowPresenter
    {
        private readonly CompanySDKViewModel _vm;
        private readonly Action _repaint;
        private readonly IGitVersionCacheStore _gitVersionCacheStore;
        private readonly Func<IModuleRepository> _repository;
        private readonly Func<IModuleRegistry> _registry;
        private readonly Func<IVersionCompatibilityService> _compatService;
        public CompanySDKVersionFlowPresenter(
            CompanySDKViewModel vm,
            Action repaint,
            IGitVersionCacheStore gitVersionCacheStore,
            Func<IModuleRepository> repository,
            Func<IModuleRegistry> registry,
            Func<IVersionCompatibilityService> compatService)
        {
            _vm = vm;
            _repaint = repaint;
            _gitVersionCacheStore = gitVersionCacheStore;
            _repository = repository;
            _registry = registry;
            _compatService = compatService;
        }
        public void EnsureStyles()
        {
            if (_vm.StateStyles != null) return;
            _vm.StateStyles = new Dictionary<ModuleState, GUIStyle>
            {
                [ModuleState.Draft] = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.84f, 0.2f) } },
                [ModuleState.Stable] = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } },
                [ModuleState.Deprecated] = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.64f, 0f) } },
                [ModuleState.Archived] = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.gray } }
            };
            _vm.StatusWaitingStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _vm.StatusInstallingStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } };
            _vm.StatusUninstallingStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
            _vm.StatusInstalledStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.green } };
            _vm.StatusUninstalledStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.red } };
            _vm.RefreshButtonStyle = new GUIStyle(EditorStyles.miniButton) { fontSize = 11, fixedHeight = 28 };
        }
        public void RefreshInstalledStateCache(List<Module> modules)
        {
            var registry = _registry();
            var upmIds = modules.Where(m => !string.IsNullOrEmpty(m.UpmId)).Select(m => m.UpmId).Distinct().ToList();
            _vm.CachedUpmInstalledIds = upmIds.Count > 0 ? UpmStateUtility.GetInstalledUpmIds(upmIds) : new HashSet<string>();
            _vm.CachedRegistryIds = new HashSet<string>(registry.GetInstalledModules().Select(x => x.Value));
            foreach (var module in modules)
            {
                if (_vm.OperationStatus.TryGetValue(module.Id.Value, out var op) && op == ModuleOperationStatus.Uninstalling)
                    continue;
                bool inRegistry = _vm.CachedRegistryIds.Contains(module.Id.Value);
                if (!string.IsNullOrEmpty(module.UpmId))
                {
                    bool inManifest = _vm.CachedUpmInstalledIds.Contains(module.UpmId);
                    if (inManifest && !inRegistry)
                    {
                        registry.MarkInstalled(module);
                        _vm.CachedRegistryIds.Add(module.Id.Value);
                    }
                    else if (!inManifest && inRegistry)
                    {
                        registry.MarkUninstalled(module.Id);
                        _vm.CachedRegistryIds.Remove(module.Id.Value);
                    }
                    continue;
                }
                if ((!string.IsNullOrEmpty(module.UnityPackageRepo) || !string.IsNullOrEmpty(module.UnityPackageLocalPath)) &&
                    module.UnityPackageRootFolders != null &&
                    module.UnityPackageRootFolders.Count > 0)
                {
                    bool exists = false;
                    var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                    foreach (var root in module.UnityPackageRootFolders)
                    {
                        if (string.IsNullOrEmpty(root)) continue;
                        var normalized = root.Replace("\\", "/");
                        var path = normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                            ? Path.Combine(projectRoot, normalized)
                            : Path.Combine(projectRoot, "Assets", normalized.TrimStart('/'));
                        if (Directory.Exists(path) || File.Exists(path))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (exists && !inRegistry)
                    {
                        registry.MarkInstalled(module);
                        _vm.CachedRegistryIds.Add(module.Id.Value);
                    }
                    else if (!exists && inRegistry)
                    {
                        registry.MarkUninstalled(module.Id);
                        _vm.CachedRegistryIds.Remove(module.Id.Value);
                    }
                }
            }
        }
        public void TryLoadVersionCacheFromEditorPrefs() => _gitVersionCacheStore.Load(_vm.GitInstallableVersionsByModuleId);
        public void SaveVersionCacheToEditorPrefs() => _gitVersionCacheStore.Save(_vm.GitInstallableVersionsByModuleId);
        public void ClearVersionCacheEditorPrefs() => _gitVersionCacheStore.Clear();
        public void EnsureGitInstallableVersionsLoaded(List<Module> modules, Action onComplete = null)
        {
            if (_vm.GitVersionsScanned) return;
            _vm.GitVersionsScanned = true;
            var modulesWithGit = new List<(Module module, string gitUrl)>();
            foreach (var m in modules)
            {
                var url = CompanySDKPresenter.GetGitHubUrlForModule(m);
                if (!string.IsNullOrEmpty(url))
                    modulesWithGit.Add((m, url));
            }
            if (modulesWithGit.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }
            void FetchNext(int index)
            {
                if (index >= modulesWithGit.Count)
                {
                    EditorUtility.ClearProgressBar();
                    onComplete?.Invoke();
                    return;
                }
                var (module, gitUrl) = modulesWithGit[index];
                EditorUtility.DisplayProgressBar("Shion SDK", $"Loading version list for {module.Name}...", (float)index / modulesWithGit.Count);
                GitHubReleaseVersionFetcher.FetchVersionsAsync(
                    gitUrl,
                    includePrerelease: true,
                    maxReleases: ShionSDKConstants.MaxReleasesPerModule,
                    (versions, error) =>
                    {
                        if (versions != null && versions.Count > 0)
                            _vm.GitInstallableVersionsByModuleId[module.Id.Value] = versions;
                        else if (!string.IsNullOrEmpty(error))
                            Debug.LogWarning($"[ShionSDK] Failed to fetch versions for '{module.Name}': {error}");
                        EditorApplication.delayCall += () => FetchNext(index + 1);
                    });
            }
            FetchNext(0);
        }
        public string GetModuleVersionForDisplay(Module module, bool installed, ModuleOperationStatus opStatus)
        {
            var id = module.Id.Value;
            if (opStatus == ModuleOperationStatus.Waiting || opStatus == ModuleOperationStatus.Installing)
            {
                if (ModuleVersionSelectionStore.TryGet(module.Id, out var v) && !string.IsNullOrEmpty(v))
                    return v;
            }
            if (_vm.SelectedVersionOverrides.TryGetValue(id, out var overridden) && !string.IsNullOrEmpty(overridden))
                return overridden;
            if (installed)
            {
                if (ModuleVersionSelectionStore.TryGet(module.Id, out var selectedVer) && !string.IsNullOrEmpty(selectedVer))
                    return selectedVer;
                if (LockFileSerializer.TryGetInstalledVersion(id, out var lockVersion) && !string.IsNullOrEmpty(lockVersion))
                    return lockVersion;
                if (_vm.GitInstallableVersionsByModuleId.TryGetValue(id, out var installable) && installable != null && installable.Count > 0)
                    return installable[0];
                return module.Version.ToString();
            }
            if (_vm.GitInstallableVersionsByModuleId.TryGetValue(id, out var list) && list != null && list.Count > 0)
            {
                if (ModuleVersionSelectionStore.TryGet(module.Id, out var selected) && !string.IsNullOrEmpty(selected))
                    return selected;
                var latest = list[0];
                _vm.SelectedVersionOverrides[id] = latest;
                return latest;
            }
            return module.Version.ToString();
        }
        public string GetDepVersionLabel(Module rootModule, Dependency dep, bool depInstalled, bool rootInstalled, string rootVersion)
        {
            var repository = _repository();
            var compatService = _compatService();
            var depModule = repository.Get(dep.Id);
            var rootId = rootModule?.Id.Value ?? "";
            var depId = dep.Id.Value;
            var compatList = compatService.GetCompatibleDepVersions(rootId, rootVersion, depId);
            if (rootInstalled)
            {
                var installedVer = GetDepInstalledVersion(depModule);
                return !string.IsNullOrEmpty(installedVer) ? installedVer : compatList?[0] ?? (depModule != null ? depModule.Version.ToString() : ShionSDKConstants.VersionPlaceholder);
            }
            if (depInstalled && depModule != null)
            {
                var installedVer = GetDepInstalledVersion(depModule);
                if (!string.IsNullOrEmpty(installedVer) && compatList != null && compatList.Count > 0 &&
                    compatList.Any(v => VersionsMatch(v, installedVer)))
                {
                    return installedVer;
                }
            }
            if (compatList != null && compatList.Count > 0)
            {
                if (compatService.TryGetSelection(rootId, rootVersion, depId, out var sel) && compatList.Any(v => VersionsMatch(v, sel)))
                    return sel;
                return compatList[0];
            }
            if (compatService.TryGetSelection(rootId, rootVersion, depId, out var saved))
                return saved;
            if (!string.IsNullOrEmpty(dep.RequestedVersion))
                return dep.RequestedVersion;
            if (depModule != null && _vm.GitInstallableVersionsByModuleId.TryGetValue(depId, out var i) && i != null && i.Count > 0)
                return i[0];
            if (depModule != null)
                return depModule.Version.ToString();
            return ShionSDKConstants.VersionPlaceholder;
        }
        public List<string> GetCompatibleDepVersionsForSelector(Module rootModule, string rootVersion, Dependency dep)
        {
            var rootId = rootModule?.Id.Value ?? "";
            var depId = dep.Id.Value;
            var compatList = _compatService().GetCompatibleDepVersions(rootId, rootVersion, depId);
            if (compatList != null && compatList.Count > 0)
                return new List<string>(compatList);
            if (_vm.GitInstallableVersionsByModuleId.TryGetValue(depId, out var all) && all != null && all.Count > 0)
                return new List<string>(all);
            return new List<string>();
        }
        public bool IsDepVersionSelectable(Module rootModule, Dependency dep, bool depInstalled, bool rootInstalled, string rootVersion, ModuleOperationStatus rootOpStatus)
        {
            if (rootOpStatus == ModuleOperationStatus.Waiting || rootOpStatus == ModuleOperationStatus.Installing || rootOpStatus == ModuleOperationStatus.Uninstalling)
                return false;
            if (rootInstalled)
                return false;
            var depModule = _repository().Get(dep.Id);
            if (string.IsNullOrEmpty(CompanySDKPresenter.GetGitHubUrlForModule(depModule)))
                return false;
            if (depInstalled && depModule != null)
            {
                var installedVer = GetDepInstalledVersion(depModule);
                var compatList = _compatService().GetCompatibleDepVersions(rootModule.Id.Value, rootVersion ?? "", dep.Id.Value);
                if (!string.IsNullOrEmpty(installedVer) && compatList != null && compatList.Count > 0 &&
                    compatList.Any(v => VersionsMatch(v, installedVer)))
                {
                    return false;
                }
            }
            return true;
        }
        public void ShowDepVersionSelectionPopup(Module rootModule, string rootVersion, Dependency dep, string currentVersion, Rect activatorRect)
        {
            var depModule = _repository().Get(dep.Id);
            var list = GetCompatibleDepVersionsForSelector(rootModule, rootVersion, dep);
            if (list == null || list.Count == 0)
            {
                EditorUtility.DisplayDialog("Version", $"No versions available for dependency '{depModule?.Name ?? dep.Id.Value}'.", "OK");
                return;
            }
            PopupWindow.Show(activatorRect, new VersionSelectionPopup(
                list,
                currentVersion,
                selected =>
                {
                    _compatService().SetSelection(rootModule.Id.Value, rootVersion ?? "", dep.Id.Value, selected);
                    _repaint();
                }));
        }
        public void ShowVersionSelectionPopup(Module module, string currentVersionCore, Rect activatorRect)
        {
            var gitHubUrl = CompanySDKPresenter.GetGitHubUrlForModule(module);
            if (string.IsNullOrEmpty(gitHubUrl))
            {
                EditorUtility.DisplayDialog("Versions", "This module has no GitHub URL (UpmSource or GitUrl), so the version list cannot be fetched.", "OK");
                return;
            }
            void ShowPopup(List<string> versions)
            {
                PopupWindow.Show(activatorRect, new VersionSelectionPopup(
                    versions,
                    currentVersionCore,
                    selected =>
                    {
                        _vm.SelectedVersionOverrides[module.Id.Value] = selected;
                        ModuleVersionSelectionStore.Set(module.Id, selected);
                        _repaint();
                    }));
            }
            if (_vm.GitInstallableVersionsByModuleId.TryGetValue(module.Id.Value, out var cached) && cached != null && cached.Count > 0)
            {
                ShowPopup(cached);
                return;
            }
            EditorUtility.DisplayDialog("Versions", "Version list not loaded yet. Please wait for it to finish or click Refresh Modules.", "OK");
        }
        private static string GetDepInstalledVersion(Module depModule)
        {
            if (depModule == null) return null;
            if (!string.IsNullOrEmpty(depModule.UpmId) && UpmStateUtility.TryGetInstalledVersion(depModule.UpmId, out var upmVer))
                return upmVer;
            if (LockFileSerializer.TryGetInstalledVersion(depModule.Id.Value, out var lockVer) && !string.IsNullOrEmpty(lockVer))
                return lockVer;
            return null;
        }
        private static bool VersionsMatch(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b);
            var na = a.Trim().TrimStart('v', 'V');
            var nb = b.Trim().TrimStart('v', 'V');
            return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
        }
    }
    internal sealed class CompanySDKInstallFlowPresenter
    {
        private readonly CompanySDKViewModel _vm;
        private readonly Action _repaint;
        private readonly IInstallCompatibilityGuard _installCompatibilityGuard;
        private readonly Action<string> _setWarning;
        private readonly Func<IModuleRepository> _repository;
        private readonly Func<IModuleRegistry> _registry;
        private readonly Func<IVersionCompatibilityService> _compatService;
        private readonly Func<IDependencyVersionConflictDetector> _conflictDetector;
        private readonly Func<UninstallModuleUseCase> _uninstallUseCase;
        private readonly Func<InstallQueueRunner> _installQueueRunner;
        private readonly Func<UninstallQueueRunner> _uninstallQueueRunner;
        public CompanySDKInstallFlowPresenter(
            CompanySDKViewModel vm,
            Action repaint,
            IInstallCompatibilityGuard installCompatibilityGuard,
            Action<string> setWarning,
            Func<IModuleRepository> repository,
            Func<IModuleRegistry> registry,
            Func<IVersionCompatibilityService> compatService,
            Func<IDependencyVersionConflictDetector> conflictDetector,
            Func<UninstallModuleUseCase> uninstallUseCase,
            Func<InstallQueueRunner> installQueueRunner,
            Func<UninstallQueueRunner> uninstallQueueRunner)
        {
            _vm = vm;
            _repaint = repaint;
            _installCompatibilityGuard = installCompatibilityGuard;
            _setWarning = setWarning;
            _repository = repository;
            _registry = registry;
            _compatService = compatService;
            _conflictDetector = conflictDetector;
            _uninstallUseCase = uninstallUseCase;
            _installQueueRunner = installQueueRunner;
            _uninstallQueueRunner = uninstallQueueRunner;
        }
        public void OnEnable()
        {
            ResumePendingInstalls();
            ResumePendingUninstalls();
        }
        public void OnInstallClicked(Module module)
        {
            try
            {
                if (module != null && module.Id.Value == ShionSDKConstants.ModuleIds.GoogleAdsMobile)
                {
                    var handled = _installCompatibilityGuard.ShouldCancelInstall(
                        module,
                        _registry(),
                        _vm.SelectedVersionOverrides,
                        _vm.GitInstallableVersionsByModuleId);
                    if (handled)
                    {
                        _repaint();
                        return;
                    }
                }
                var conflicts = _conflictDetector().GetConflicts(module);
                if (conflicts != null && conflicts.Count > 0)
                {
                    var depNames = string.Join(", ", conflicts.Select(c => $"{c.DepModule.Name} (current: {c.CurrentVersion}, required: {c.RequestedVersion})").ToArray());
                    var ok = EditorUtility.DisplayDialog(
                        "Upgrade dependency",
                        $"The following packages are installed with a version that does not match the requirement of '{module.Name}':\n\n{depNames}\n\nDo you want to uninstall and reinstall them with the version required by '{module.Name}'?",
                        "Yes",
                        "Cancel");
                    if (ok)
                    {
                        string rootVersion = null;
                        ModuleVersionSelectionStore.TryGet(module.Id, out rootVersion);
                        foreach (var c in conflicts)
                        {
                            _compatService().SetSelection(module.Id.Value, rootVersion ?? "", c.DepModule.Id.Value, c.RequestedVersion);
                            ModuleVersionSelectionStore.Set(c.DepModule.Id, c.RequestedVersion);
                        }
                        PendingInstallStore.Add(module.Id.Value);
                        PendingInstallAfterDependencyUpgradeStore.Set(module.Id.Value, conflicts.Select(c => c.DepModule.Id.Value).ToList());
                        var modulesToUninstall = conflicts.Select(c => c.DepModule).ToList();
                        foreach (var m in modulesToUninstall)
                            PendingUninstallStore.Add(m.Id.Value);
                        _uninstallQueueRunner()?.Enqueue(modulesToUninstall);
                    }
                    _repaint();
                    return;
                }
                PendingInstallStore.Add(module.Id.Value);
                _installQueueRunner()?.Enqueue(module.Id);
            }
            catch (Exception ex)
            {
                _setWarning(ex.Message);
                Debug.LogError($"[ShionSDK] Install failed: {ex.Message}\n{ex.StackTrace}");
            }
            _repaint();
        }
        public void OnUninstallClicked(Module module)
        {
            try
            {
                var hasBlocking = _uninstallUseCase().HasBlockingDependents(module.Id, out var dependents);
                if (hasBlocking)
                {
                    var msg = dependents.Count == 1
                        ? $"Cannot uninstall '{module.Name}' because '{dependents[0].Name}' depends on it."
                        : $"Cannot uninstall '{module.Name}' because these modules depend on it: {string.Join(", ", dependents.Select(d => d.Name))}.";
                    _setWarning(msg);
                    _vm.PendingBatchUninstall = new List<Module> { module };
                    foreach (var dep in dependents)
                    {
                        if (!_vm.PendingBatchUninstall.Any(m => m.Id.Value == dep.Id.Value))
                            _vm.PendingBatchUninstall.Add(dep);
                    }
                    if (_vm.OperationStatus.TryGetValue(module.Id.Value, out var s) && s == ModuleOperationStatus.Uninstalling)
                        _vm.OperationStatus[module.Id.Value] = ModuleOperationStatus.None;
                }
                else
                {
                    PendingUninstallStore.Add(module.Id.Value);
                    _uninstallQueueRunner()?.Enqueue(new List<Module> { module });
                }
            }
            catch (Exception ex)
            {
                _setWarning(ex.Message);
                Debug.LogError($"[ShionSDK] Uninstall failed: {ex.Message}\n{ex.StackTrace}");
            }
            _repaint();
        }
        public void OnBatchUninstallAccept()
        {
            if (_vm.PendingBatchUninstall == null || _vm.PendingBatchUninstall.Count == 0) return;
            foreach (var m in _vm.PendingBatchUninstall)
                PendingUninstallStore.Add(m.Id.Value);
            _uninstallQueueRunner()?.Enqueue(_vm.PendingBatchUninstall);
            _vm.PendingBatchUninstall = null;
            _repaint();
        }
        public void OnCancelBatch()
        {
            _vm.PendingBatchUninstall = null;
            _repaint();
        }
        public void TickQueues()
        {
            _uninstallQueueRunner()?.Tick();
            TryEnqueuePendingInstallAfterDependencyUpgrade();
            _installQueueRunner()?.Tick();
        }
        private void ResumePendingInstalls()
        {
            foreach (var rootId in PendingInstallStore.GetAll())
            {
                var repository = _repository();
                var registry = _registry();
                var module = repository.Get(new ModuleId(rootId));
                if (module == null) continue;
                bool installed = !string.IsNullOrEmpty(module.UpmId)
                    ? UpmStateUtility.IsInstalled(module.UpmId)
                    : registry.IsInstalled(module.Id);
                if (installed)
                {
                    PendingInstallStore.Remove(rootId);
                    continue;
                }
                if (PendingInstallAfterDependencyUpgradeStore.TryGet(out var pendingRoot, out var depIds) &&
                    pendingRoot == rootId && depIds != null && depIds.Count > 0)
                {
                    bool depsStillInstalled = false;
                    foreach (var depId in depIds)
                    {
                        var dep = repository.Get(new ModuleId(depId));
                        if (dep != null && ModuleInstallStateUtility.IsActuallyInstalled(dep, registry))
                        {
                            depsStillInstalled = true;
                            break;
                        }
                    }
                    if (depsStillInstalled)
                        continue;
                }
                _installQueueRunner()?.Enqueue(module.Id);
            }
        }
        private void ResumePendingUninstalls()
        {
            var modulesToUninstall = new List<Module>();
            foreach (var id in PendingUninstallStore.GetAll())
            {
                var module = _repository().Get(new ModuleId(id));
                if (module == null)
                {
                    PendingUninstallStore.Remove(id);
                    continue;
                }
                bool installed = !string.IsNullOrEmpty(module.UpmId)
                    ? UpmStateUtility.IsInstalled(module.UpmId)
                    : _registry().IsInstalled(module.Id);
                if (!installed)
                {
                    PendingUninstallStore.Remove(id);
                    continue;
                }
                modulesToUninstall.Add(module);
            }
            if (modulesToUninstall.Count > 0)
                _uninstallQueueRunner()?.Enqueue(modulesToUninstall);
        }
        private void TryEnqueuePendingInstallAfterDependencyUpgrade()
        {
            if (!PendingInstallAfterDependencyUpgradeStore.TryGet(out var rootId, out var depIds))
                return;
            foreach (var depId in depIds)
            {
                var dep = _repository().Get(new ModuleId(depId));
                if (dep == null) continue;
                if (ModuleInstallStateUtility.IsActuallyInstalled(dep, _registry()))
                    return;
            }
            PendingInstallAfterDependencyUpgradeStore.Clear();
            _installQueueRunner()?.Enqueue(new ModuleId(rootId));
        }
    }
    internal readonly struct CompanySDKPresenterDependencies
    {
        public CompanySDKPresenterDependencies(
            Func<ShionSDKServices> createServices,
            IGitVersionCacheStore gitVersionCacheStore,
            IInstallCompatibilityGuard installCompatibilityGuard)
        {
            CreateServices = createServices;
            GitVersionCacheStore = gitVersionCacheStore;
            InstallCompatibilityGuard = installCompatibilityGuard;
        }
        public Func<ShionSDKServices> CreateServices { get; }
        public IGitVersionCacheStore GitVersionCacheStore { get; }
        public IInstallCompatibilityGuard InstallCompatibilityGuard { get; }
        public static CompanySDKPresenterDependencies CreateDefault()
        {
            return new CompanySDKPresenterDependencies(
                ShionSDKServiceFactory.Create,
                new EditorPrefsGitVersionCacheStore(),
                new NoOpInstallCompatibilityGuard());
        }
    }
    internal sealed class VersionSelectionPopup : PopupWindowContent
    {
        private readonly List<string> _versions;
        private readonly string _current;
        private readonly Action<string> _onSelected;
        private Vector2 _scroll;
        public VersionSelectionPopup(List<string> versions, string current, Action<string> onSelected)
        {
            _versions = versions ?? new List<string>();
            _current = current;
            _onSelected = onSelected;
        }
        public override Vector2 GetWindowSize()
        {
            const float headerHeight = 22f;
            const float headerSpace = 2f;
            const float rowHeight = 20f;
            const float padding = 8f;
            int rowCount = Mathf.Clamp(_versions.Count, 1, 8);
            float height = padding * 2 + headerHeight + headerSpace + rowCount * rowHeight;
            return new Vector2(320f, height);
        }
        public override void OnGUI(Rect rect)
        {
            if (_versions == null || _versions.Count == 0)
            {
                GUILayout.Label("No versions found.", EditorStyles.miniLabel);
                return;
            }
            var headerRect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(headerRect, new Color(0.20f, 0.20f, 0.20f, 1f));
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 2, 2)
            };
            GUI.Label(headerRect, "Select version", headerStyle);
            GUILayout.Space(2);
            _scroll = GUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _versions.Count; i++)
            {
                var v = _versions[i];
                bool isCurrent = string.Equals(v, _current, StringComparison.OrdinalIgnoreCase);
                var rowRect = GUILayoutUtility.GetRect(new GUIContent(v), EditorStyles.label, GUILayout.Height(20));
                var dark = new Color(0.28f, 0.28f, 0.28f, 1f);
                var darker = new Color(0.24f, 0.24f, 0.24f, 1f);
                var bgColor = (i % 2 == 0) ? dark : darker;
                if (isCurrent)
                    bgColor = new Color(bgColor.r + 0.05f, bgColor.g + 0.05f, bgColor.b + 0.05f, bgColor.a);
                EditorGUI.DrawRect(rowRect, bgColor);
                var style = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal,
                    padding = new RectOffset(8, 4, 2, 2)
                };
                if (GUI.Button(rowRect, v, style))
                {
                    _onSelected?.Invoke(v);
                    editorWindow.Close();
                    break;
                }
            }
            GUILayout.EndScrollView();
        }
    }
    internal interface IGitVersionCacheStore
    {
        void Load(Dictionary<string, List<string>> target);
        void Save(Dictionary<string, List<string>> source);
        void Clear();
    }
    internal sealed class EditorPrefsGitVersionCacheStore : IGitVersionCacheStore
    {
        private readonly string _versionCachePrefix = ShionSDKConstants.EditorPrefsKeys.VersionCachePrefix;
        private readonly string _versionCacheModulesKey = ShionSDKConstants.EditorPrefsKeys.VersionCacheModules;
        public void Load(Dictionary<string, List<string>> target)
        {
            if (target == null)
                return;
            var modulesStr = EditorPrefs.GetString(_versionCacheModulesKey, "");
            if (string.IsNullOrEmpty(modulesStr))
                return;
            foreach (var id in modulesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var key = _versionCachePrefix + id;
                var val = EditorPrefs.GetString(key, "");
                if (string.IsNullOrEmpty(val))
                    continue;
                var versions = val.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (versions.Length > 0)
                    target[id] = new List<string>(versions);
            }
        }
        public void Save(Dictionary<string, List<string>> source)
        {
            Clear();
            if (source == null || source.Count == 0)
                return;
            var ids = new List<string>();
            foreach (var kv in source)
            {
                if (kv.Value == null || kv.Value.Count == 0)
                    continue;
                EditorPrefs.SetString(_versionCachePrefix + kv.Key, string.Join("|", kv.Value));
                ids.Add(kv.Key);
            }
            if (ids.Count > 0)
                EditorPrefs.SetString(_versionCacheModulesKey, string.Join(",", ids));
        }
        public void Clear()
        {
            var modulesStr = EditorPrefs.GetString(_versionCacheModulesKey, "");
            if (!string.IsNullOrEmpty(modulesStr))
            {
                foreach (var id in modulesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    EditorPrefs.DeleteKey(_versionCachePrefix + id);
            }
            EditorPrefs.DeleteKey(_versionCacheModulesKey);
        }
    }
    internal interface IInstallCompatibilityGuard
    {
        bool ShouldCancelInstall(
            Module module,
            IModuleRegistry registry,
            Dictionary<string, string> selectedVersionOverrides,
            Dictionary<string, List<string>> gitInstallableVersionsByModuleId);
    }
    internal sealed class NoOpInstallCompatibilityGuard : IInstallCompatibilityGuard
    {
        public bool ShouldCancelInstall(
            Module module,
            IModuleRegistry registry,
            Dictionary<string, string> selectedVersionOverrides,
            Dictionary<string, List<string>> gitInstallableVersionsByModuleId)
        {
            return false;
        }
    }
    internal static class CompatibilityOrchestrator
    {
        public static bool IsUpgrade(string targetVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(VersionComparisonService.Normalize(targetVersion ?? "")) || string.IsNullOrEmpty(VersionComparisonService.Normalize(currentVersion ?? "")))
                return false;
            return VersionComparisonService.Compare(targetVersion, currentVersion) > 0;
        }
    }
}