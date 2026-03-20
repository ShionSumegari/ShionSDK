using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public interface IScriptingDefineSymbolsService
    {
        void AddSymbolsForModule(Module module);
        void RemoveSymbolsForModule(Module module);
    }
}