using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public interface IModuleVersionSelectionStore
    {
        void Set(ModuleId id, string version);
        bool TryGet(ModuleId id, out string version);
        void Clear();
    }
}