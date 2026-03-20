namespace Shion.SDK.Core
{
    public interface IModuleInstaller
    {
        void Install(Module module);
        void Uninstall(Module module);
    }
}