namespace Shion.SDK.Editor
{
    public static class SDKInstallerBootstrap
    {
        public static InstallModuleUseCase CreateInstaller()
        {
            return ShionSDKServiceFactory.Create().InstallUseCase;
        }
    }
}