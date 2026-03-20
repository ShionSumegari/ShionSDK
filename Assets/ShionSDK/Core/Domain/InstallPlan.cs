using System.Collections.Generic;
namespace Shion.SDK.Core
{
    public class InstallPlan
    {
        public IReadOnlyList<Module> OrderedModules { get; }
        public IReadOnlyDictionary<string, string> RequestedVersions { get; }
        public InstallPlan(IReadOnlyList<Module> orderedModules, IReadOnlyDictionary<string, string> requestedVersions = null)
        {
            OrderedModules = orderedModules;
            RequestedVersions = requestedVersions ?? new Dictionary<string, string>();
        }
    }
}