using System.Collections.Generic;
namespace Shion.SDK.Core
{
    public interface IModuleRegistry
    {
        bool IsInstalled(ModuleId id);
        void MarkInstalled(Module module, string version = null);
        void MarkUninstalled(ModuleId id);
        IEnumerable<ModuleId> GetInstalledModules();
    }
}