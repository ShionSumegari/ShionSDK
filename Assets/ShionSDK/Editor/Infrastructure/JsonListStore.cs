using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Shion.SDK.Editor
{
    internal class JsonListStore : IStringListStore
    {
        private readonly string _path;
        private bool _loaded;
        private readonly List<string> _items = new();
        [System.Serializable]
        private class Wrapper
        {
            public List<string> items = new();
            public List<string> roots = new();
            public List<string> ids = new();
        }
        public JsonListStore(string filePath)
        {
            _path = filePath ?? "";
        }
        public IReadOnlyCollection<string> GetAll()
        {
            EnsureLoaded();
            return new List<string>(_items);
        }
        public void Add(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            EnsureLoaded();
            if (!_items.Contains(id))
            {
                _items.Add(id);
                Save();
            }
        }
        public void Remove(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            EnsureLoaded();
            if (_items.Remove(id))
                Save();
        }
        private void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
                    return;
                var json = File.ReadAllText(_path);
                var data = JsonUtility.FromJson<Wrapper>(json);
                if (data == null)
                    return;
                var source = data.items.Count > 0 ? data.items
                    : data.roots.Count > 0 ? data.roots
                    : data.ids;
                foreach (var id in source)
                {
                    if (!string.IsNullOrEmpty(id) && !_items.Contains(id))
                        _items.Add(id);
                }
            }
            catch { }
        }
        private void Save()
        {
            try
            {
                var data = new Wrapper { items = new List<string>(_items) };
                var json = JsonUtility.ToJson(data, true);
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(_path, json);
            }
            catch { }
        }
    }
    internal static class PendingInstallStore
    {
        private const string PathFile = "Assets/ShionSDK/TextAsset/PendingAsset/pending-installs.json";
        private static JsonListStore _store;
        private static JsonListStore Store => _store ??= new JsonListStore(PathFile);
        public static IReadOnlyCollection<string> GetAll() => Store.GetAll();
        public static void Add(string id) => Store.Add(id);
        public static void Remove(string id) => Store.Remove(id);
    }
    internal static class PendingUninstallStore
    {
        private const string PathFile = "Assets/ShionSDK/TextAsset/PendingAsset/pending-uninstalls.json";
        private static JsonListStore _store;
        private static JsonListStore Store => _store ??= new JsonListStore(PathFile);
        public static IReadOnlyCollection<string> GetAll() => Store.GetAll();
        public static void Add(string id) => Store.Add(id);
        public static void Remove(string id) => Store.Remove(id);
    }
    internal static class PendingInstallAfterDependencyUpgradeStore
    {
        private const string PathFile = "Assets/ShionSDK/TextAsset/PendingAsset/pending-install-after-upgrade.json";
        [System.Serializable]
        private class Data
        {
            public string rootId;
            public List<string> depIds = new();
        }
        private static Data _cached;
        private static bool _loaded;
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _cached = new Data();
            try
            {
                if (!string.IsNullOrEmpty(PathFile) && File.Exists(PathFile))
                {
                    var json = File.ReadAllText(PathFile);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var d = JsonUtility.FromJson<Data>(json);
                        if (d != null) _cached = d;
                    }
                }
            }
            catch { }
        }
        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(PathFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonUtility.ToJson(_cached, true);
                File.WriteAllText(PathFile, json);
            }
            catch { }
        }
        public static void Set(string rootId, List<string> depIdsToUninstall)
        {
            if (string.IsNullOrEmpty(rootId) || depIdsToUninstall == null || depIdsToUninstall.Count == 0)
                return;
            EnsureLoaded();
            _cached.rootId = rootId;
            _cached.depIds = new List<string>(depIdsToUninstall);
            Save();
        }
        public static bool TryGet(out string rootId, out List<string> depIds)
        {
            rootId = null;
            depIds = null;
            EnsureLoaded();
            if (string.IsNullOrEmpty(_cached.rootId) || _cached.depIds == null || _cached.depIds.Count == 0)
                return false;
            rootId = _cached.rootId;
            depIds = new List<string>(_cached.depIds);
            return true;
        }
        public static void Clear()
        {
            EnsureLoaded();
            _cached.rootId = null;
            _cached.depIds = new List<string>();
            Save();
        }
    }
}