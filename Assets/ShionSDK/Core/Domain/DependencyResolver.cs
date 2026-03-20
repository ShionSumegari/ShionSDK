using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Shion.SDK.Core
{
    public class DependencyResolver
    {
        private readonly IModuleRepository repository;
        public DependencyResolver(IModuleRepository repository)
        {
            this.repository = repository;
        }
        public InstallPlan BuildInstallPlan(Module root)
        {
            var result = new List<Module>();
            var requestedVersions = new Dictionary<string, string>();
            var visited = new HashSet<string>();
            var stack = new HashSet<string>();
            Visit(root, visited, stack, result, requestedVersions);
            return new InstallPlan(result, requestedVersions);
        }
        private void Visit(Module module, HashSet<string> visited, HashSet<string> stack, List<Module> result, Dictionary<string, string> requestedVersions)
        {
            if (visited.Contains(module.Id.Value))
                return;
            if (stack.Contains(module.Id.Value))
                throw new Exception($"Circular dependency detected: {module.Id}");
            stack.Add(module.Id.Value);
            foreach (var dep in module.Dependencies)
            {
                var depModule = repository.Get(dep.Id);
                if (depModule == null)
                    throw new Exception($"Missing dependency: {dep.Id}");
                if (!string.IsNullOrEmpty(dep.RequestedVersion) && !requestedVersions.ContainsKey(dep.Id.Value))
                    requestedVersions[dep.Id.Value] = dep.RequestedVersion;
                Visit(depModule, visited, stack, result, requestedVersions);
            }
            stack.Remove(module.Id.Value);
            visited.Add(module.Id.Value);
            result.Add(module);
        }
    }
}