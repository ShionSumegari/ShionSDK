using System.Collections.Generic;
namespace Shion.SDK.Core
{
    public class Module
    {
        public ModuleId Id { get; }
        public string Name { get; }
        public SemanticVersion Version { get; }
        public string GitUrl { get; }
        public string LocalPath { get; }
        public string UpmId { get; }
        public string UpmSource { get; }
        public string UpmPath { get; }
        public string UnityPackageRepo { get; }
        public string UnityPackageLocalPath { get; }
        public IReadOnlyList<string> UnityPackageRootFolders { get; }
        public ModuleState State { get; }
        public ModuleCategory Category { get; }
        public IReadOnlyList<Dependency> Dependencies { get; }
        public IReadOnlyList<string> Symbols { get; }
        public Module(
            ModuleId id,
            string name,
            SemanticVersion version,
            string gitUrl,
            string localPath,
            string upmId,
            string upmSource,
            string upmPath,
            string unityPackageRepo,
            string unityPackageLocalPath,
            IReadOnlyList<string> unityPackageRootFolders,
            ModuleState state,
            ModuleCategory category,
            IReadOnlyList<Dependency> dependencies,
            IReadOnlyList<string> symbols = null)
        {
            Id = id;
            Name = name;
            Version = version;
            GitUrl = gitUrl;
            LocalPath = localPath;
            UpmId = upmId;
            UpmSource = upmSource;
            UpmPath = upmPath;
            UnityPackageRepo = unityPackageRepo;
            UnityPackageLocalPath = unityPackageLocalPath;
            UnityPackageRootFolders = unityPackageRootFolders;
            State = state;
            Category = category;
            Dependencies = dependencies ?? System.Array.Empty<Dependency>();
            Symbols = symbols ?? System.Array.Empty<string>();
        }
    }
}