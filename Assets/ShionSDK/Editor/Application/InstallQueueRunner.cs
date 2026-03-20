using System.Collections.Generic;
using System.Linq;
using Shion.SDK.Core;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
namespace Shion.SDK.Editor
{
    internal class InstallQueueRunner
    {
        private readonly IModuleRepository _repository;
        private readonly IModuleRegistry _registry;
        private readonly IModuleInstaller _installer;
        private readonly IVersionCompatibilityService _compatService;
        private readonly DependencyResolver _resolver;
        private readonly Dictionary<string, ModuleOperationStatus> _operationStatus;
        private readonly System.Action<Module, string> _onInstallError;
        private class Job
        {
            public Module Root;
            public List<Module> Plan;
            public IReadOnlyDictionary<string, string> RequestedVersions;
            public int Index;
            public HashSet<string> Started = new HashSet<string>();
            public HashSet<string> RetriedWithAlternateTag = new HashSet<string>();
            public Dictionary<string, string> VersionByModuleId = new Dictionary<string, string>();
        }
        private readonly Queue<Job> _queue = new();
        private Job _currentJob;
        public InstallQueueRunner(
            IModuleRepository repository,
            IModuleRegistry registry,
            IModuleInstaller installer,
            Dictionary<string, ModuleOperationStatus> operationStatus,
            System.Action<Module, string> onInstallError,
            IVersionCompatibilityService compatService = null)
        {
            _repository = repository;
            _registry = registry;
            _installer = installer;
            _compatService = compatService;
            _resolver = new DependencyResolver(repository);
            _operationStatus = operationStatus;
            _onInstallError = onInstallError;
        }
        public void Enqueue(ModuleId rootId)
        {
            var root = _repository.Get(rootId);
            if (root == null)
                return;
            var installPlan = _resolver.BuildInstallPlan(root);
            var plan = installPlan.OrderedModules.ToList();
            if (plan.Count == 0)
                return;
            var requestedVersions = new Dictionary<string, string>();
            if (installPlan.RequestedVersions != null)
            {
                foreach (var kv in installPlan.RequestedVersions)
                    requestedVersions[kv.Key] = kv.Value;
            }
            string rootVersion = null;
            ModuleVersionSelectionStore.TryGet(rootId, out rootVersion);
            if (!string.IsNullOrEmpty(rootVersion))
                requestedVersions[rootId.Value] = rootVersion;
            foreach (var m in plan)
            {
                if (m.Id.Value == rootId.Value) continue;
                if (_compatService != null && _compatService.TryGetSelection(rootId.Value, rootVersion, m.Id.Value, out var sel) && !string.IsNullOrEmpty(sel))
                    requestedVersions[m.Id.Value] = sel;
                else if (requestedVersions.TryGetValue(m.Id.Value, out _)) { }
                else if (_compatService != null)
                {
                    var compatList = _compatService.GetCompatibleDepVersions(rootId.Value, rootVersion, m.Id.Value);
                    if (compatList != null && compatList.Count > 0)
                        requestedVersions[m.Id.Value] = compatList[0];
                }
            }
            EnqueueJob(root, rootId, plan, requestedVersions);
        }
        private void EnqueueJob(Module root, ModuleId rootId, List<Module> plan,
            Dictionary<string, string> requestedVersions)
        {
            var job = new Job
            {
                Root = root,
                Plan = plan,
                RequestedVersions = requestedVersions,
                Index = 0
            };
            _queue.Enqueue(job);
            foreach (var m in plan)
            {
                bool isRoot = m.Id.Value == rootId.Value;
                if (!isRoot && _registry.IsInstalled(m.Id))
                    continue;
                var id = m.Id.Value;
                if (!_operationStatus.TryGetValue(id, out var state) ||
                    state == ModuleOperationStatus.None)
                {
                    _operationStatus[id] = ModuleOperationStatus.Waiting;
                }
                if (requestedVersions.TryGetValue(id, out var reqVer) && !string.IsNullOrEmpty(reqVer))
                    ModuleVersionSelectionStore.Set(m.Id, reqVer);
                else if (id == rootId.Value && ModuleVersionSelectionStore.TryGet(m.Id, out var existingVer) && !string.IsNullOrEmpty(existingVer))
                    ModuleVersionSelectionStore.Set(m.Id, existingVer);
            }
        }
        public void Tick()
        {
            if (_currentJob == null)
            {
                if (_queue.Count == 0)
                    return;
                _currentJob = _queue.Dequeue();
            }
            while (_currentJob.Index < _currentJob.Plan.Count)
            {
                var current = _currentJob.Plan[_currentJob.Index];
                bool skip = ModuleInstallStateUtility.IsActuallyInstalled(current, _registry);
                if (!skip)
                    break;
                var aid = current.Id.Value;
                if (_operationStatus.TryGetValue(aid, out var s) &&
                    (s == ModuleOperationStatus.Waiting ||
                     s == ModuleOperationStatus.Installing))
                {
                    _operationStatus[aid] = ModuleOperationStatus.None;
                }
                _currentJob.Index++;
            }
            if (_currentJob.Index >= _currentJob.Plan.Count)
            {
                if (_currentJob.Root != null)
                {
                    PendingInstallStore.Remove(_currentJob.Root.Id.Value);
                }
                _currentJob = null;
                return;
            }
            var module = _currentJob.Plan[_currentJob.Index];
            var id = module.Id.Value;
            if (_currentJob.Started.Contains(id))
            {
                if (UpmAddRequestStore.TryGet(module.Id, out AddRequest addReq) && addReq != null && addReq.IsCompleted)
                {
                    if (addReq.Status == StatusCode.Failure)
                    {
                        if (!_currentJob.RetriedWithAlternateTag.Contains(id) &&
                            ModuleVersionSelectionStore.TryGet(module.Id, out var currentTag) &&
                            !string.IsNullOrEmpty(currentTag))
                        {
                            var alternateTag = currentTag.StartsWith("v", System.StringComparison.OrdinalIgnoreCase)
                                ? currentTag.Substring(1)
                                : "v" + currentTag;
                            UpmAddRequestStore.Clear(module.Id);
                            _currentJob.Started.Remove(id);
                            _currentJob.RetriedWithAlternateTag.Add(id);
                            ModuleVersionSelectionStore.Set(module.Id, alternateTag);
                            _currentJob.VersionByModuleId[id] = alternateTag;
                            Debug.Log($"[ShionSDK] Tag '{currentTag}' failed for '{module.Name}', retrying with '{alternateTag}'.");
                            return;
                        }
                        var msg = addReq.Error != null
                            ? addReq.Error.message
                            : "Unknown Package Manager error.";
                        _onInstallError?.Invoke(module, msg);
                        _operationStatus[id] = ModuleOperationStatus.None;
                        UpmAddRequestStore.Clear(module.Id);
                        _currentJob.Index++;
                        return;
                    }
                    if (addReq.Status == StatusCode.Success)
                        UpmAddRequestStore.Clear(module.Id);
                }
                if (ModuleInstallStateUtility.IsActuallyInstalled(module, _registry))
                {
                    var version = _currentJob.VersionByModuleId.TryGetValue(id, out var v) ? v : null;
                    _registry.MarkInstalled(module, version);
                    _operationStatus[id] = ModuleOperationStatus.None;
                    _currentJob.Index++;
                }
                return;
            }
            _currentJob.Started.Add(id);
            _operationStatus[id] = ModuleOperationStatus.Installing;
            string versionToInstall;
            if (_currentJob.VersionByModuleId.TryGetValue(id, out var retryVer) && !string.IsNullOrEmpty(retryVer))
                versionToInstall = retryVer;
            else if (_currentJob.RequestedVersions != null && _currentJob.RequestedVersions.TryGetValue(id, out var reqVer) && !string.IsNullOrEmpty(reqVer))
                versionToInstall = reqVer;
            else if (ModuleVersionSelectionStore.TryGet(module.Id, out var storedVer) && !string.IsNullOrEmpty(storedVer))
                versionToInstall = storedVer;
            else
                versionToInstall = null;
            if (!string.IsNullOrEmpty(versionToInstall))
            {
                ModuleVersionSelectionStore.Set(module.Id, versionToInstall);
                _currentJob.VersionByModuleId[id] = versionToInstall;
            }
            _installer.Install(module);
        }
    }
}