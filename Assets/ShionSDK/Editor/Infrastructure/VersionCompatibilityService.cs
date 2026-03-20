using System.Collections.Generic;
namespace Shion.SDK.Editor
{
    public sealed class VersionCompatibilityService : IVersionCompatibilityService
    {
        public List<string> GetCompatibleDepVersions(string rootId, string rootVersion, string depId)
            => VersionCompatibilityRepository.GetCompatibleDepVersions(rootId, rootVersion, depId);
        public bool TryGetSelection(string rootId, string rootVersion, string depId, out string version)
            => VersionCompatibilityRepository.TryGetSelection(rootId, rootVersion, depId, out version);
        public void SetSelection(string rootId, string rootVersion, string depId, string version)
            => VersionCompatibilityRepository.SetSelection(rootId, rootVersion, depId, version);
        public void Reload() => VersionCompatibilityRepository.Reload();
    }
}