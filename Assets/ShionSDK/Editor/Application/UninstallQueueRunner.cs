using System.Collections.Generic;
using System.Linq;
using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    internal class UninstallQueueRunner
    {
        private readonly IModuleRepository _repository;
        private readonly IModuleRegistry _registry;
        private readonly UninstallModuleUseCase _uninstallUseCase;
        private readonly Dictionary<string, ModuleOperationStatus> _operationStatus;
        private readonly Dictionary<string, int> _uninstallCompleteTicks = new Dictionary<string, int>();
        private class Job
        {
            public List<Module> Ordered;
            public int Index;
            public bool StartedCurrent;
        }
        private readonly Queue<Job> _queue = new Queue<Job>();
        private Job _currentJob;
        public UninstallQueueRunner(
            IModuleRepository repository,
            IModuleRegistry registry,
            UninstallModuleUseCase uninstallUseCase,
            Dictionary<string, ModuleOperationStatus> operationStatus)
        {
            _repository = repository;
            _registry = registry;
            _uninstallUseCase = uninstallUseCase;
            _operationStatus = operationStatus;
        }
        public void Enqueue(IEnumerable<Module> batch)
        {
            var list = batch?.Distinct().ToList();
            if (list == null || list.Count == 0)
                return;
            var remaining = new List<Module>(list);
            var ordered = new List<Module>();
            while (remaining.Count > 0)
            {
                Module leaf = null;
                foreach (var m in remaining)
                {
                    bool hasDependent = remaining.Any(other =>
                        other.Id.Value != m.Id.Value &&
                        other.Dependencies != null &&
                        other.Dependencies.Any(d => d.Id.Value == m.Id.Value));
                    if (!hasDependent)
                    {
                        leaf = m;
                        break;
                    }
                }
                if (leaf == null)
                {
                    ordered.AddRange(remaining);
                    break;
                }
                ordered.Add(leaf);
                remaining.Remove(leaf);
            }
            var job = new Job
            {
                Ordered = ordered,
                Index = 0,
                StartedCurrent = false
            };
            _queue.Enqueue(job);
        }
        public void Tick()
        {
            if (_uninstallCompleteTicks.Count > 0)
            {
                var keys = new List<string>(_uninstallCompleteTicks.Keys);
                foreach (var mid in keys)
                {
                    _uninstallCompleteTicks[mid]++;
                    if (_uninstallCompleteTicks[mid] >= ShionSDKEditorSettings.MinUninstallTicksVisible)
                    {
                        if (_operationStatus.ContainsKey(mid) &&
                            _operationStatus[mid] == ModuleOperationStatus.Uninstalling)
                        {
                            _operationStatus[mid] = ModuleOperationStatus.None;
                        }
                        PendingUninstallStore.Remove(mid);
                        _uninstallCompleteTicks.Remove(mid);
                    }
                }
            }
            if (_currentJob == null)
            {
                if (_queue.Count == 0)
                    return;
                _currentJob = _queue.Dequeue();
            }
            if (_currentJob.Index >= _currentJob.Ordered.Count)
            {
                foreach (var m in _currentJob.Ordered)
                {
                    var mid = m.Id.Value;
                    _operationStatus[mid] = ModuleOperationStatus.Uninstalling;
                    if (!_uninstallCompleteTicks.ContainsKey(mid))
                        _uninstallCompleteTicks[mid] = 0;
                }
                _currentJob = null;
                return;
            }
            var candidate = _currentJob.Ordered[_currentJob.Index];
            var id = candidate.Id.Value;
            for (int i = _currentJob.Index; i < _currentJob.Ordered.Count; i++)
            {
                var m = _currentJob.Ordered[i];
                var mid = m.Id.Value;
                if (!ModuleInstallStateUtility.IsActuallyInstalled(m, _registry))
                    continue;
                if (i == _currentJob.Index)
                    _operationStatus[mid] = ModuleOperationStatus.Uninstalling;
                else
                    _operationStatus[mid] = ModuleOperationStatus.Waiting;
            }
            if (_currentJob.StartedCurrent)
            {
                if (!string.IsNullOrEmpty(candidate.UpmId))
                {
                    if (!ModuleInstallStateUtility.IsActuallyInstalled(candidate, _registry))
                    {
                        _currentJob.Index++;
                        _currentJob.StartedCurrent = false;
                    }
                    return;
                }
                _currentJob.Index++;
                _currentJob.StartedCurrent = false;
                return;
            }
            _currentJob.StartedCurrent = true;
            var ok = _uninstallUseCase.Execute(candidate.Id, out var dependents);
            if (!ok)
            {
                if (dependents != null && dependents.Count > 0)
                {
                    var batchIds = new HashSet<string>(_currentJob.Ordered.Select(m => m.Id.Value));
                    var outsideBatch = dependents
                        .Where(d => !batchIds.Contains(d.Id.Value))
                        .ToList();
                    if (outsideBatch.Count > 0)
                    {
                        var names = string.Join(", ", outsideBatch.Select(d => d.Name));
                        UnityEngine.Debug.LogWarning(
                            $"{ShionSDKConstants.LogPrefix} Cannot batch uninstall '{candidate.Name}' because these modules (outside batch) depend on it: {names}.");
                    }
                }
                PendingUninstallStore.Remove(id);
            }
            _currentJob.Index++;
            _currentJob.StartedCurrent = false;
        }
    }
}