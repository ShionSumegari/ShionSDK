namespace Shion.SDK.Core
{
    public class Dependency
    {
        public ModuleId Id { get; }
        public string RequestedVersion { get; }
        public Dependency(ModuleId id, string requestedVersion)
        {
            Id = id;
            RequestedVersion = requestedVersion;
        }
    }
}