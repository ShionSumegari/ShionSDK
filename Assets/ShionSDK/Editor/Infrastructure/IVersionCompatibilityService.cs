using System.Collections.Generic;
namespace Shion.SDK.Editor
{
    public interface IVersionCompatibilityService
    {
        List<string> GetCompatibleDepVersions(string rootId, string rootVersion, string depId);
        bool TryGetSelection(string rootId, string rootVersion, string depId, out string version);
        void SetSelection(string rootId, string rootVersion, string depId, string version);
        void Reload();
    }
}