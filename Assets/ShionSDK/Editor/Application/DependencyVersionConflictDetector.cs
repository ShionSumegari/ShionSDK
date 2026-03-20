using System;
using System.Collections.Generic;
using System.Linq;
using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public struct DepVersionConflict
    {
        public Module DepModule;
        public string CurrentVersion;
        public string RequestedVersion;
    }
    public sealed class DependencyVersionConflictDetector : IDependencyVersionConflictDetector
    {
        private readonly IModuleRepository _repository;
        private readonly IModuleRegistry _registry;
        private readonly IVersionCompatibilityService _compatService;
        public DependencyVersionConflictDetector(IModuleRepository repository, IModuleRegistry registry, IVersionCompatibilityService compatService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _compatService = compatService ?? throw new ArgumentNullException(nameof(compatService));
        }
        public List<DepVersionConflict> GetConflicts(Module root)
        {
            var resolver = new DependencyResolver(_repository);
            var plan = resolver.BuildInstallPlan(root);
            var rootId = root.Id.Value;
            ModuleVersionSelectionStore.TryGet(root.Id, out var rootVersion);
            var requestedVersions = new Dictionary<string, string>();
            if (plan.RequestedVersions != null)
            {
                foreach (var kv in plan.RequestedVersions)
                    requestedVersions[kv.Key] = kv.Value;
            }
            foreach (var m in plan.OrderedModules)
            {
                if (m.Id.Value == rootId) continue;
                if (_compatService.TryGetSelection(rootId, rootVersion ?? "", m.Id.Value, out var sel) && !string.IsNullOrEmpty(sel))
                    requestedVersions[m.Id.Value] = sel;
                else if (!requestedVersions.ContainsKey(m.Id.Value))
                {
                    var compatList = _compatService.GetCompatibleDepVersions(rootId, rootVersion ?? "", m.Id.Value);
                    if (compatList != null && compatList.Count > 0)
                        requestedVersions[m.Id.Value] = compatList[0];
                }
            }
            var conflicts = new List<DepVersionConflict>();
            foreach (var m in plan.OrderedModules)
            {
                if (m.Id.Value == rootId) continue;
                if (!ModuleInstallStateUtility.IsActuallyInstalled(m, _registry))
                    continue;
                var compatList = _compatService.GetCompatibleDepVersions(rootId, rootVersion ?? "", m.Id.Value);
                if (compatList == null || compatList.Count == 0)
                    continue;
                var current = GetInstalledVersion(m);
                if (string.IsNullOrEmpty(current))
                    continue;
                if (compatList.Any(v => VersionStringUtility.Matches(v, current)))
                    continue;
                if (!requestedVersions.TryGetValue(m.Id.Value, out var requested) || string.IsNullOrEmpty(requested))
                    requested = compatList[0];
                conflicts.Add(new DepVersionConflict
                {
                    DepModule = m,
                    CurrentVersion = current,
                    RequestedVersion = requested
                });
            }
            return conflicts;
        }
        private static string GetInstalledVersion(Module m)
        {
            if (!string.IsNullOrEmpty(m.UpmId) && UpmStateUtility.TryGetInstalledVersion(m.UpmId, out var upmVer))
                return upmVer;
            if (LockFileSerializer.TryGetInstalledVersion(m.Id.Value, out var lockVer))
                return lockVer;
            return null;
        }
    }
    internal static class VersionStringUtility
    {
        public static string Normalize(string version, bool trimBrackets = false)
        {
            if (string.IsNullOrWhiteSpace(version))
                return string.Empty;
            var normalized = version.Trim().TrimStart('v', 'V');
            return trimBrackets ? normalized.Trim('[', ']') : normalized;
        }
        public static bool Matches(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b);
            return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
        }
        public static int Compare(string a, string b, bool trimBrackets = true, bool emptyIsLower = true)
        {
            var na = Normalize(a, trimBrackets);
            var nb = Normalize(b, trimBrackets);
            if (string.IsNullOrEmpty(na) && string.IsNullOrEmpty(nb))
                return 0;
            if (string.IsNullOrEmpty(na))
                return emptyIsLower ? -1 : 1;
            if (string.IsNullOrEmpty(nb))
                return emptyIsLower ? 1 : -1;
            var pa = na.Split('.').Select(ParsePart).ToArray();
            var pb = nb.Split('.').Select(ParsePart).ToArray();
            var max = Math.Max(pa.Length, pb.Length);
            for (var i = 0; i < max; i++)
            {
                var va = i < pa.Length ? pa[i] : 0;
                var vb = i < pb.Length ? pb[i] : 0;
                if (va != vb)
                    return va.CompareTo(vb);
            }
            return 0;
        }
        private static int ParsePart(string value)
        {
            return int.TryParse(value, out var parsed) ? parsed : 0;
        }
    }
}