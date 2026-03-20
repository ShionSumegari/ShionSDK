using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class LockFileSerializer
    {
        private const string PathFile = ShionSDKConstants.Paths.LockFile;
        [System.Serializable]
        public class LockData
        {
            public List<LockEntry> installed = new();
        }
        [System.Serializable]
        public class LockEntry
        {
            public string id;
            public string version;
        }
        public void Save(IEnumerable<LockEntry> entries)
        {
            var data = new LockData();
            data.installed.AddRange(entries);
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(PathFile, json);
        }
        public List<LockEntry> Load()
        {
            if (!File.Exists(PathFile))
                return new();
            var json = File.ReadAllText(PathFile);
            var data = JsonUtility.FromJson<LockData>(json);
            return data?.installed ?? new();
        }
        private static List<LockEntry> _cachedEntries;
        private static double _lastLoadTime;
        private const double CacheSeconds = 2.0;
        public static bool TryGetInstalledVersion(string moduleId, out string version)
        {
            version = null;
            if (string.IsNullOrEmpty(moduleId))
                return false;
            var now = EditorApplication.timeSinceStartup;
            if (_cachedEntries == null || (now - _lastLoadTime) > CacheSeconds)
            {
                _cachedEntries = new LockFileSerializer().Load();
                _lastLoadTime = now;
            }
            var entry = _cachedEntries.Find(e => e != null && e.id == moduleId);
            if (entry == null || string.IsNullOrEmpty(entry.version))
                return false;
            version = entry.version.Trim();
            return true;
        }
        public static void InvalidateVersionCache()
        {
            _cachedEntries = null;
        }
    }
}