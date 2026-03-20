using System.Collections.Generic;
namespace  Shion.SDK.Core
{
    public interface IModuleRepository
    {
        Module Get(ModuleId id);
        IEnumerable<Module> GetAll();
    }
}