using System.Collections.Generic;
using Shion.SDK.Core;
namespace Shion.SDK.Editor
{
    public class LocalModuleRegistry : IModuleRegistry
    {
        private readonly LockFileSerializer _lockSerializer = new LockFileSerializer();
        private readonly IModuleRepository _repository;
        private readonly IScriptingDefineSymbolsService _symbolsService;
        public LocalModuleRegistry(IModuleRepository repository, IScriptingDefineSymbolsService symbolsService)
        {
            _repository = repository;
            _symbolsService = symbolsService;
        }
        private List<LockFileSerializer.LockEntry> _cachedEntries;
        private bool _cacheValid;
        private List<LockFileSerializer.LockEntry> LoadEntries()
        {
            if (_cacheValid && _cachedEntries != null)
                return _cachedEntries;
            _cachedEntries = _lockSerializer.Load();
            _cacheValid = true;
            return _cachedEntries;
        }
        public bool IsInstalled(ModuleId id)
        {
            var entries = LoadEntries();
            return entries.Exists(e => e.id == id.Value);
        }
        public void MarkInstalled(Module module, string version = null)
        {
            string versionStr;
            if (!string.IsNullOrEmpty(version))
                versionStr = version;
            else if (ModuleVersionSelectionStore.TryGet(module.Id, out var selectedVer) && !string.IsNullOrEmpty(selectedVer))
                versionStr = selectedVer;
            else if (module.Version.Major != 0 || module.Version.Minor != 0 || module.Version.Patch != 0)
                versionStr = module.Version.ToString();
            else
                versionStr = ShionSDKConstants.VersionPlaceholder;
            var entries = new List<LockFileSerializer.LockEntry>(LoadEntries());
            var existing = entries.Find(e => e.id == module.Id.Value);
            if (existing != null)
                existing.version = versionStr;
            else
                entries.Add(new LockFileSerializer.LockEntry { id = module.Id.Value, version = versionStr });
            _lockSerializer.Save(entries);
            _cachedEntries = entries;
            _cacheValid = true;
            LockFileSerializer.InvalidateVersionCache();
            _symbolsService.AddSymbolsForModule(module);
        }
        public void MarkUninstalled(ModuleId id)
        {
            var module = _repository?.Get(id);
            if (module != null)
                _symbolsService.RemoveSymbolsForModule(module);
            var entries = new List<LockFileSerializer.LockEntry>(LoadEntries());
            if (entries.RemoveAll(e => e.id == id.Value) > 0)
            {
                _lockSerializer.Save(entries);
                _cachedEntries = entries;
                _cacheValid = true;
                LockFileSerializer.InvalidateVersionCache();
            }
        }
        public IEnumerable<ModuleId> GetInstalledModules()
        {
            var entries = LoadEntries();
            foreach (var entry in entries)
                yield return new ModuleId(entry.id);
        }
    }
}