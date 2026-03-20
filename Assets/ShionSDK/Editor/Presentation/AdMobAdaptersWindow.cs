using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class AdMobAdaptersWindow : EditorWindow
    {
        private const int MenuPriority = 2001;
        private List<AdapterViewModel> _adapters = new List<AdapterViewModel>();
        private Dictionary<string, List<string>> _versionCache = new Dictionary<string, List<string>>();
        private Dictionary<string, string> _latestCache = new Dictionary<string, string>();
        private Dictionary<string, (string Android, string Ios)> _supportVersionCache = new Dictionary<string, (string, string)>();
        private Vector2 _scroll;
        private bool _admobInstalled;
        private string _loadError;
        private bool _isInstalling;
        private string _installProgress;
        private int _statusFilter;
        private string _selectedVersionByPackage = "";
        private bool _versionsLoading;
        private const string SessionRefreshKey = "ShionSDK.AdMobAdaptersWindow.HasRefreshedThisSession";
        private const string SessionSnapshotKey = "ShionSDK.AdMobAdaptersWindow.Snapshot";
        [Serializable]
        private class AdapterSessionSnapshot
        {
            public List<Item> Items = new List<Item>();
            public List<VersionCacheItem> VersionCaches = new List<VersionCacheItem>();
            public List<LatestCacheItem> LatestCaches = new List<LatestCacheItem>();
            public List<SupportCacheItem> SupportCaches = new List<SupportCacheItem>();
            public int StatusFilter;
            public float ScrollY;
            [Serializable]
            public class Item
            {
                public string PackageId;
                public bool IsInstalled;
                public string InstalledVersion;
            }
            [Serializable]
            public class VersionCacheItem
            {
                public string PackageId;
                public List<string> Versions = new List<string>();
            }
            [Serializable]
            public class LatestCacheItem
            {
                public string PackageId;
                public string Latest;
            }
            [Serializable]
            public class SupportCacheItem
            {
                public string Key;
                public string Android;
                public string Ios;
            }
        }
        [MenuItem("Shion/Ad SDK/AdMob Adapters (full)", false, MenuPriority)]
        public static void OpenFromMenu()
        {
            Open();
        }
        [MenuItem("Shion/Ad SDK/AdMob Adapters (full)", true, MenuPriority)]
        private static bool ValidateAdMobAdapters()
        {
            return IsGoogleMobileAdsInstalled();
        }
        private static bool IsGoogleMobileAdsInstalled()
        {
            if (AssetDatabase.IsValidFolder("Assets/GoogleMobileAds")) return true;
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return false;
            var assetsPath = Path.Combine(projectRoot, "Assets", "GoogleMobileAds");
            if (Directory.Exists(assetsPath)) return true;
            var packagesPath = Path.Combine(projectRoot, "Packages");
            if (Directory.Exists(packagesPath))
            {
                foreach (var dir in Directory.GetDirectories(packagesPath))
                {
                    var name = Path.GetFileName(dir);
                    if (name != null && ((name.Contains("google") && name.Contains("ads")) || name == "com.google.ads.mobile"))
                        return true;
                }
            }
            return false;
        }
        public static void Open()
        {
            var w = GetWindow<AdMobAdaptersWindow>(true, "AdMob Adapters", true);
            w.minSize = new Vector2(900, 420);
        }
        public static void NotifyExternalInstallCompleted(string packageId, string installedVersion)
        {
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(installedVersion))
                return;
            foreach (var window in Resources.FindObjectsOfTypeAll<AdMobAdaptersWindow>())
            {
                if (window == null)
                    continue;
                window.ApplyExternalInstallUpdate(packageId, installedVersion);
            }
        }
        private void OnEnable()
        {
            if (!SessionState.GetBool(SessionRefreshKey, false))
            {
                SessionState.SetBool(SessionRefreshKey, true);
                RefreshState();
            }
            else
            {
                _admobInstalled = IsGoogleMobileAdsInstalled();
                if (_admobInstalled && _adapters.Count == 0)
                {
                    if (!TryRestoreSnapshot())
                        InitializeAdaptersWithoutRefresh();
                }
            }
            EditorApplication.delayCall += ReconcileInstallStateAfterDomainReload;
        }
        private void RefreshState()
        {
            _admobInstalled = IsGoogleMobileAdsInstalled();
            _isInstalling = false;
            _installProgress = null;
            _adapters.Clear();
            _loadError = null;
            _versionCache.Clear();
            _latestCache.Clear();
            _supportVersionCache.Clear();
            AdMobChangelogVersionFetcher.ClearCache();
            if (!_admobInstalled) return;
            foreach (var def in AdMobAdapterConfig.AllAdapters)
            {
                var installed = AdMobAdapterInstaller.IsInstalled(def.PackageId);
                var installedVer = installed ? AdMobAdapterInstaller.GetInstalledVersion(def.PackageId) : null;
                _adapters.Add(new AdapterViewModel
                {
                    Def = def,
                    IsInstalled = installed,
                    InstalledVersion = installedVer
                });
            }
            SaveSnapshot();
        }
        private void InitializeAdaptersWithoutRefresh()
        {
            _adapters.Clear();
            foreach (var def in AdMobAdapterConfig.AllAdapters)
                _adapters.Add(new AdapterViewModel { Def = def, IsInstalled = false, InstalledVersion = null });
        }
        private void SaveSnapshot()
        {
            var snap = new AdapterSessionSnapshot();
            foreach (var vm in _adapters)
            {
                snap.Items.Add(new AdapterSessionSnapshot.Item
                {
                    PackageId = vm.Def?.PackageId ?? "",
                    IsInstalled = vm.IsInstalled,
                    InstalledVersion = vm.InstalledVersion
                });
            }
            foreach (var kv in _versionCache)
            {
                snap.VersionCaches.Add(new AdapterSessionSnapshot.VersionCacheItem
                {
                    PackageId = kv.Key,
                    Versions = kv.Value != null ? new List<string>(kv.Value) : new List<string>()
                });
            }
            foreach (var kv in _latestCache)
            {
                snap.LatestCaches.Add(new AdapterSessionSnapshot.LatestCacheItem
                {
                    PackageId = kv.Key,
                    Latest = kv.Value
                });
            }
            foreach (var kv in _supportVersionCache)
            {
                snap.SupportCaches.Add(new AdapterSessionSnapshot.SupportCacheItem
                {
                    Key = kv.Key,
                    Android = kv.Value.Android,
                    Ios = kv.Value.Ios
                });
            }
            snap.StatusFilter = _statusFilter;
            snap.ScrollY = _scroll.y;
            SessionState.SetString(SessionSnapshotKey, JsonUtility.ToJson(snap));
        }
        private void ApplyExternalInstallUpdate(string packageId, string installedVersion)
        {
            if (_adapters == null || _adapters.Count == 0)
                return;
            var changed = false;
            foreach (var vm in _adapters)
            {
                if (vm?.Def == null)
                    continue;
                if (!string.Equals(vm.Def.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!vm.IsInstalled || !string.Equals(vm.InstalledVersion, installedVersion, StringComparison.Ordinal))
                {
                    vm.IsInstalled = true;
                    vm.InstalledVersion = installedVersion;
                    changed = true;
                }
                break;
            }
            if (!changed)
                return;
            EditorPrefs.SetString(ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + packageId, installedVersion);
            SaveSnapshot();
            Repaint();
        }
        private bool TryRestoreSnapshot()
        {
            var json = SessionState.GetString(SessionSnapshotKey, "");
            if (string.IsNullOrEmpty(json)) return false;
            AdapterSessionSnapshot snap;
            try { snap = JsonUtility.FromJson<AdapterSessionSnapshot>(json); }
            catch { return false; }
            if (snap?.Items == null || snap.Items.Count == 0) return false;
            var byPackage = snap.Items.ToDictionary(x => x.PackageId ?? "", x => x);
            _adapters.Clear();
            foreach (var def in AdMobAdapterConfig.AllAdapters)
            {
                byPackage.TryGetValue(def.PackageId ?? "", out var item);
                _adapters.Add(new AdapterViewModel
                {
                    Def = def,
                    IsInstalled = item != null && item.IsInstalled,
                    InstalledVersion = item?.InstalledVersion
                });
            }
            _versionCache.Clear();
            if (snap.VersionCaches != null)
            {
                foreach (var item in snap.VersionCaches)
                {
                    if (string.IsNullOrEmpty(item?.PackageId)) continue;
                    _versionCache[item.PackageId] = item.Versions ?? new List<string>();
                }
            }
            _latestCache.Clear();
            if (snap.LatestCaches != null)
            {
                foreach (var item in snap.LatestCaches)
                {
                    if (string.IsNullOrEmpty(item?.PackageId)) continue;
                    _latestCache[item.PackageId] = item.Latest ?? "";
                }
            }
            _supportVersionCache.Clear();
            if (snap.SupportCaches != null)
            {
                foreach (var item in snap.SupportCaches)
                {
                    if (string.IsNullOrEmpty(item?.Key)) continue;
                    _supportVersionCache[item.Key] = (item.Android ?? "-", item.Ios ?? "-");
                }
            }
            _statusFilter = snap.StatusFilter;
            _scroll = new Vector2(_scroll.x, snap.ScrollY);
            _versionsLoading = false;
            return true;
        }
        private void ReconcileInstallStateAfterDomainReload()
        {
            if (this == null) return;
            _admobInstalled = IsGoogleMobileAdsInstalled();
            if (!_admobInstalled || _adapters == null || _adapters.Count == 0)
            {
                _isInstalling = false;
                _installProgress = null;
                return;
            }
            var changed = false;
            foreach (var vm in _adapters)
            {
                if (vm?.Def == null) continue;
                var installed = AdMobAdapterInstaller.IsInstalled(vm.Def.PackageId);
                var installedVer = installed ? AdMobAdapterInstaller.GetInstalledVersion(vm.Def.PackageId) : null;
                if (installed && string.IsNullOrEmpty(installedVer))
                {
                    var verKey = ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + vm.Def.PackageId;
                    installedVer = EditorPrefs.GetString(verKey, vm.InstalledVersion);
                }
                if (vm.IsInstalled != installed || !string.Equals(vm.InstalledVersion, installedVer, StringComparison.Ordinal))
                {
                    vm.IsInstalled = installed;
                    vm.InstalledVersion = installedVer;
                    changed = true;
                }
            }
            if (_isInstalling || !string.IsNullOrEmpty(_installProgress))
            {
                _isInstalling = false;
                _installProgress = null;
                changed = true;
            }
            if (changed)
            {
                SaveSnapshot();
                Repaint();
            }
        }
        private void LoadVersionsForAdapter(AdapterViewModel vm)
        {
            var cacheKey = vm.Def.PackageId;
            if (_versionCache.ContainsKey(cacheKey)) return;
            _versionsLoading = true;
            AdMobChangelogVersionFetcher.FetchVersionListAsync(vm.Def.IntegrationSlug, (versions, latest, error) =>
            {
                _versionsLoading = false;
                if (error == null && versions != null)
                {
                    _versionCache[cacheKey] = versions;
                    if (!string.IsNullOrEmpty(latest))
                        _latestCache[cacheKey] = latest;
                    SaveSnapshot();
                }
                Repaint();
            });
        }
        private void LoadSupportVersionsForAdapter(AdapterViewModel vm, string selectedMediationVersion)
        {
            if (string.IsNullOrEmpty(selectedMediationVersion) || selectedMediationVersion == "-") return;
            if (!selectedMediationVersion.Contains(".")) return;
            var cacheKey = $"{vm.Def.PackageId}|{selectedMediationVersion}";
            if (_supportVersionCache.ContainsKey(cacheKey)) return;
            AdMobChangelogVersionFetcher.FetchSupportedVersionsAsync(
                vm.Def.IntegrationSlug,
                selectedMediationVersion,
                (androidVer, iosVer, error) =>
                {
                    if (error == null)
                        _supportVersionCache[cacheKey] = (androidVer ?? "-", iosVer ?? "-");
                    else
                        _supportVersionCache[cacheKey] = ("-", "-");
                    SaveSnapshot();
                    Repaint();
                });
        }
        private void OnGUI()
        {
            if (!_admobInstalled)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox(
                    "Google Mobile Ads SDK is not installed. Install it from Shion SDK Manager (SDKs tab) first.",
                    MessageType.Warning);
                if (GUILayout.Button("Open SDK Manager", GUILayout.Height(28)))
                    CompanySDKWindow.Open();
                return;
            }
            EditorGUILayout.LabelField("AdMob Mediation Adapters", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Integrate ad sources from " +
                "https://developers.google.com/admob/unity/mediation. " +
                "Install downloads .zip from changelog, extracts and imports .unitypackage.",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            var installing = _isInstalling || AdMobAdapterInstaller.IsOperationInProgress;
            GUI.enabled = !installing;
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                RefreshState();
            var newStatusFilter = EditorGUILayout.Popup(_statusFilter, new[] { "All", "Installed", "Not Installed" }, GUILayout.Width(120));
            if (newStatusFilter != _statusFilter)
            {
                _statusFilter = newStatusFilter;
                SaveSnapshot();
            }
            if (GUILayout.Button("Open Integration Docs", GUILayout.Width(140)))
                Application.OpenURL("https://developers.google.com/admob/unity/mediation");
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(_versionsLoading ? "Loading versions..." : " ", EditorStyles.miniLabel);
            GUILayout.Space(8);
            var visibleAdapters = _adapters.Where(vm =>
            {
                if (_statusFilter == 1 && !vm.IsInstalled) return false;
                if (_statusFilter == 2 && vm.IsInstalled) return false;
                return true;
            }).ToList();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Adapter", headerStyle, GUILayout.MinWidth(160), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Status", headerStyle, GUILayout.Width(70));
            EditorGUILayout.LabelField("Version", headerStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField("Android", headerStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField("iOS", headerStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField("", headerStyle, GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            foreach (var vm in visibleAdapters)
            {
                LoadVersionsForAdapter(vm);
                var statusStr = vm.IsInstalled ? "Installed" : "Not installed";
                var statusColor = vm.IsInstalled ? new Color(0.2f, 0.8f, 0.2f) : Color.gray;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(vm.Def.DisplayName, GUILayout.MinWidth(160), GUILayout.ExpandWidth(true));
                var statusStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = statusColor } };
                EditorGUILayout.LabelField(statusStr, statusStyle, GUILayout.Width(70));
                var versionOptions = _versionCache.TryGetValue(vm.Def.PackageId, out var vers) ? vers : new List<string>();
                var currentVer = vm.IsInstalled ? vm.InstalledVersion : (_latestCache.TryGetValue(vm.Def.PackageId, out var l) ? l : null);
                if (!vm.IsInstalled && string.IsNullOrEmpty(currentVer) && versionOptions.Count > 0)
                    currentVer = versionOptions[0];
                var verKey = ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + vm.Def.PackageId;
                if (!EditorPrefs.HasKey(verKey) && !string.IsNullOrEmpty(currentVer))
                    EditorPrefs.SetString(verKey, currentVer);
                var selected = EditorPrefs.GetString(verKey, currentVer ?? "");
                if (vm.IsInstalled && !string.IsNullOrEmpty(currentVer))
                    selected = currentVer;
                var selIdx = versionOptions.IndexOf(selected);
                if (selIdx < 0 && versionOptions.Count > 0)
                    selIdx = 0;
                if (vm.IsInstalled)
                {
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(currentVer) ? "-" : currentVer, GUILayout.Width(100));
                    selected = currentVer;
                }
                else
                {
                    var labels = versionOptions.Count > 0
                        ? versionOptions.ToArray()
                        : new[] { string.IsNullOrEmpty(currentVer) ? "-" : currentVer };
                    var newIdx = EditorGUILayout.Popup(selIdx >= 0 ? selIdx : 0, labels, GUILayout.Width(100));
                    if (versionOptions.Count > 0 && newIdx >= 0 && newIdx < versionOptions.Count)
                    {
                        var newVer = versionOptions[newIdx];
                        if (newVer != selected)
                            EditorPrefs.SetString(verKey, newVer);
                        selected = newVer;
                    }
                    else if (versionOptions.Count == 0)
                    {
                        selected = currentVer;
                    }
                }
                LoadSupportVersionsForAdapter(vm, selected);
                var supportKey = $"{vm.Def.PackageId}|{selected}";
                string androidVer, iosVer;
                if (string.IsNullOrEmpty(selected))
                {
                    androidVer = "-";
                    iosVer = "-";
                }
                else if (_supportVersionCache.TryGetValue(supportKey, out var supportVers))
                {
                    androidVer = supportVers.Android ?? "-";
                    iosVer = supportVers.Ios ?? "-";
                }
                else
                {
                    androidVer = "...";
                    iosVer = "...";
                }
                EditorGUILayout.LabelField(androidVer, GUILayout.Width(100));
                EditorGUILayout.LabelField(iosVer, GUILayout.Width(100));
                if (vm.IsInstalled)
                {
                    GUI.enabled = !installing;
                    if (GUILayout.Button("Remove", GUILayout.Width(65)))
                    {
                        AdMobAdapterInstaller.Uninstall(vm.Def.PackageId, (ok, err) =>
                        {
                            vm.IsInstalled = false;
                            vm.InstalledVersion = null;
                            SaveSnapshot();
                            Repaint();
                        });
                    }
                }
                else
                {
                    var canInstall = !vm.IsInstalled && (versionOptions.Count > 0 || !string.IsNullOrEmpty(selected)) && !installing;
                    GUI.enabled = canInstall;
                    if (GUILayout.Button(versionOptions.Count == 0 ? "..." : "Install", GUILayout.Width(65)))
                    {
                        var ver = !string.IsNullOrEmpty(selected) ? selected : (versionOptions.Count > 0 ? versionOptions[0] : null);
                        StartInstallWithPrecheck(vm, ver, versionOptions);
                    }
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            if (_isInstalling && !string.IsNullOrEmpty(_installProgress))
                EditorGUILayout.LabelField(_installProgress, EditorStyles.miniLabel);
        }
        private void StartInstallWithPrecheck(AdapterViewModel vm, string version, List<string> versionOptions)
        {
            if (_isInstalling || AdMobAdapterInstaller.IsOperationInProgress)
                return;
            if (vm?.Def == null || string.IsNullOrEmpty(version))
            {
                EditorUtility.DisplayDialog("Install Failed", "Invalid adapter version.", "OK");
                return;
            }
            if (vm.IsInstalled)
                return;
            _isInstalling = true;
            _installProgress = $"Installing {vm.Def.DisplayName} ({version})...";
            Repaint();
            var versionPrefKey = ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + vm.Def.PackageId;
            EditorPrefs.SetString(versionPrefKey, version);
            if (vm.IsInstalled)
            {
                var currentInstalled = vm.InstalledVersion ?? AdMobAdapterInstaller.GetInstalledVersion(vm.Def.PackageId);
                _installProgress = $"Uninstalling {vm.Def.DisplayName} ({currentInstalled ?? "current"})...";
                Repaint();
                AdMobAdapterInstaller.Uninstall(vm.Def.PackageId, (uninstallOk, uninstallErr) =>
                {
                    if (!uninstallOk)
                    {
                        _isInstalling = false;
                        _installProgress = null;
                        var msg = !string.IsNullOrEmpty(uninstallErr) ? uninstallErr : "Uninstall failed";
                        EditorUtility.DisplayDialog("Uninstall Failed", msg, "OK");
                        Repaint();
                        return;
                    }
                    InstallResolvedAdMobVersion(vm, version);
                });
                return;
            }
            InstallResolvedAdMobVersion(vm, version);
        }
        private void InstallResolvedAdMobVersion(AdapterViewModel vm, string installVersion)
        {
            _installProgress = $"Installing {vm.Def.DisplayName} ({installVersion})...";
            Repaint();
            AdMobAdapterInstaller.Install(vm.Def.PackageId, installVersion, (ok, err) =>
            {
                _isInstalling = false;
                _installProgress = null;
                if (ok)
                {
                    Debug.Log($"[ShionSDK] AdMob auto-align install completed: {vm.Def.PackageId} => {installVersion}");
                    vm.IsInstalled = true;
                    vm.InstalledVersion = installVersion;
                    EditorPrefs.SetString(ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + vm.Def.PackageId, installVersion);
                    SaveSnapshot();
                }
                else
                {
                    var msg = !string.IsNullOrEmpty(err) ? err : "Install failed";
                    Debug.LogWarning($"[ShionSDK] AdMob adapter install failed: {msg}");
                    vm.IsInstalled = false;
                    vm.InstalledVersion = null;
                    SaveSnapshot();
                    EditorUtility.DisplayDialog("Install Failed", msg, "OK");
                }
                Repaint();
            });
        }
        public static void RunAdMobAdapterInstallForAdSDK(
            AdMobAdapterConfig.AdapterDef def,
            string selectedVersion,
            Action<string, string> onComplete)
        {
            if (def == null || string.IsNullOrEmpty(selectedVersion))
            {
                onComplete?.Invoke(null, "Invalid adapter version.");
                return;
            }
            EditorApplication.delayCall += () => onComplete?.Invoke(selectedVersion, null);
        }
        private class AdapterViewModel
        {
            public AdMobAdapterConfig.AdapterDef Def;
            public bool IsInstalled;
            public string InstalledVersion;
        }
    }
}