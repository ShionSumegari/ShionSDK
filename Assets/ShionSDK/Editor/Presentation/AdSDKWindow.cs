using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shion.SDK.Core;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class AdSDKWindow : EditorWindow
    {
        private const string MaxSdkPath = "Assets/MaxSdk";
        private static bool s_hasRefreshedThisSession;
        private static class AdSDKDataCache
        {
            public static bool IsValid;
            public static bool MaxSdkInstalled;
            public static bool AdmobInstalled;
            public static (string Version, string Android, string Ios) AppLovinPluginInfo;
            public static (string Version, string Android, string Ios) AdMobPluginInfo;
#if USE_APPLOVIN
            public static readonly List<AppLovinIntegrationBridge.AdapterInfo> AppLovinAdapters = new List<AppLovinIntegrationBridge.AdapterInfo>();
#endif
            public static readonly List<AdMobAdapterViewModel> AdmobAdapters = new List<AdMobAdapterViewModel>();
        }
        private AdSDKPluginInstallBridge _pluginBridge;
        private Dictionary<string, int> _selectedVersionIndex = new Dictionary<string, int>();
        private Dictionary<string, (string Android, string Ios)> _supportVersionCache = new Dictionary<string, (string, string)>();
#if USE_APPLOVIN
        private bool _appLovinAdapterInstalling;
        private string _appLovinAdapterProgress;
#endif
        private Dictionary<string, List<string>> _admobVersionCache = new Dictionary<string, List<string>>();
        private Dictionary<string, string> _admobLatestCache = new Dictionary<string, string>();
        private Dictionary<string, (string Android, string Ios)> _admobSupportVersionCache = new Dictionary<string, (string, string)>();
        private Dictionary<string, string> _admobSelectedVersionByPackage = new Dictionary<string, string>();
        private bool _admobAdapterInstalling;
        private string _admobAdapterProgress;
        private string _admobAdapterInstallingPackageId;
#if USE_APPLOVIN
        private Vector2 _appLovinAdapterScroll;
        private HashSet<string> _selectedAppLovinAdapterIds = new HashSet<string>();
#endif
        private Vector2 _admobAdapterScroll;
        private HashSet<string> _selectedAdMobAdapterIds = new HashSet<string>();
        private GUIContent _trashIconContent;
        private static int _cachedCompatibilityMismatchCount;
        private class AdMobAdapterViewModel
        {
            public AdMobAdapterConfig.AdapterDef Def;
            public bool IsInstalled;
            public string InstalledVersion;
        }
        [MenuItem("Shion/Ad SDK", false, 1999)]
        public static void OpenFromMenu()
        {
            Open();
        }
        [MenuItem("Shion/Ad SDK", true, 1999)]
        private static bool ValidateAdSDK() => true;
        [MenuItem("Shion/Ad SDK Manager", false, 1998)]
        public static void OpenFromAdSdkManagerMenu()
        {
            Open();
        }
        [MenuItem("Shion/Ad SDK Manager", true, 1998)]
        private static bool ValidateAdSdkManagerMenu() => true;
        public static void SetCachedCompatibilityMismatchCount(int count)
        {
            _cachedCompatibilityMismatchCount = count;
        }
        public static void Open()
        {
            var w = GetWindow<AdSDKWindow>(true, "Ad SDK", true);
            w.minSize = new Vector2(MinWindowWidth, ShionSDKConstants.Window.MinAdSDKHeight);
            var p = w.position;
            if (p.width < InitialWindowWidth || p.height < InitialWindowHeight)
                w.position = new Rect(p.x, p.y, Mathf.Max(p.width, InitialWindowWidth), Mathf.Max(p.height, InitialWindowHeight));
        }
        private static readonly string PendingAdMobQueueKey = ShionSDKConstants.EditorPrefsKeys.PendingAdMobQueue;
        private static readonly string PendingAdMobIndexKey = ShionSDKConstants.EditorPrefsKeys.PendingAdMobIndex;
        private void OnEnable()
        {
            minSize = new Vector2(MinWindowWidth, ShionSDKConstants.Window.MinAdSDKHeight);
            var pos = position;
            if (pos.width < InitialWindowWidth || pos.height < InitialWindowHeight)
                position = new Rect(pos.x, pos.y, Mathf.Max(pos.width, InitialWindowWidth), Mathf.Max(pos.height, InitialWindowHeight));
            if (!s_hasRefreshedThisSession)
            {
                FetchAndUpdateCache();
                s_hasRefreshedThisSession = true;
            }
            if (_pluginBridge == null)
                _pluginBridge = new AdSDKPluginInstallBridge(Repaint, FetchAndUpdateCache);
            EditorApplication.delayCall += () => _pluginBridge?.EnsureVersionsLoaded(Repaint);
            EditorApplication.delayCall += TryResumePendingAdMobInstallQueue;
        }
        private void OnDisable()
        {
        }
        private void ClearAdMobEditorPrefsCache()
        {
            foreach (var def in AdMobAdapterConfig.AllAdapters)
            {
                if (def?.PackageId == null) continue;
                EditorPrefs.DeleteKey(AdMobVersionCachePrefix + def.PackageId);
                EditorPrefs.DeleteKey(AdMobLatestCachePrefix + def.PackageId);
            }
            var supportKeys = EditorPrefs.GetString(AdMobSupportKeysListKey, "").Split(new[] { '\x1f' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var k in supportKeys)
            {
                if (!string.IsNullOrEmpty(k)) EditorPrefs.DeleteKey(k);
            }
            EditorPrefs.DeleteKey(AdMobSupportKeysListKey);
        }
        private void OnRefreshClicked()
        {
            _supportVersionCache.Clear();
            _admobSupportVersionCache.Clear();
            _admobVersionCache.Clear();
            _admobLatestCache.Clear();
            ClearAdMobEditorPrefsCache();
            AdMobAppLovinCompatibilityService.ClearCache();
            AdMobAppLovinCompatibilityService.ClearAppLovinSupportCache();
            FetchAndUpdateCache();
            _pluginBridge?.ForceReloadVersions(Repaint);
            Repaint();
            EditorApplication.delayCall += Repaint;
        }
        private static string FormatSupportInParentheses(string android, string ios)
        {
            var a = (android ?? "").Trim();
            var i = (ios ?? "").Trim();
            if (string.IsNullOrEmpty(a)) a = "_";
            if (string.IsNullOrEmpty(i)) i = "_";
            return $"Android: {a} - iOS: {i}";
        }
        private string GetInstallSelectedDialogMessage()
        {
            var lines = new List<string>();
#if USE_APPLOVIN
            var alList = new List<string>();
            foreach (var a in AdSDKDataCache.AppLovinAdapters)
            {
                if (a == null) continue;
                var canInstall = (a.Action == AppLovinIntegrationBridge.ActionType.Install || a.Action == AppLovinIntegrationBridge.ActionType.Upgrade) && a.Network != null;
                var id = a.Name ?? a.DisplayName ?? "";
                if (!canInstall || !_selectedAppLovinAdapterIds.Contains(id)) continue;
                var parenContent = FormatSupportInParentheses(a.AndroidVersion, a.IosVersion);
                alList.Add($"  • {a.DisplayName ?? a.Name ?? "AppLovin"}");
                alList.Add($"      ({parenContent})");
            }
            if (alList.Count > 0)
            {
                lines.Add("AppLovin MAX:");
                lines.AddRange(alList);
            }
#endif
            var admobList = new List<string>();
            foreach (var vm in AdSDKDataCache.AdmobAdapters)
            {
                if (vm?.Def == null || !_selectedAdMobAdapterIds.Contains(vm.Def.PackageId)) continue;
                var versionOptions = _admobVersionCache.TryGetValue(vm.Def.PackageId, out var vers) ? vers : null;
                var latest = _admobLatestCache.TryGetValue(vm.Def.PackageId, out var l) ? l : null;
                var canInstall = !vm.IsInstalled && (versionOptions?.Count > 0 || !string.IsNullOrEmpty(latest));
                var canUpdate = vm.IsInstalled && !string.IsNullOrEmpty(latest) && VersionComparisonService.Compare(latest, vm.InstalledVersion ?? "") > 0;
                if (!canInstall && !canUpdate) continue;
                var ver = canUpdate ? latest : (_admobSelectedVersionByPackage.TryGetValue(vm.Def.PackageId, out var sv) && !string.IsNullOrEmpty(sv) ? sv : (latest ?? (versionOptions?.Count > 0 ? versionOptions[0] : "_")));
                var supportKey = $"{vm.Def.PackageId}|{ver ?? "_"}";
                var parenContent = _admobSupportVersionCache.TryGetValue(supportKey, out var support)
                    ? FormatSupportInParentheses(support.Android, support.Ios)
                    : FormatSupportInParentheses("_", "_");
                admobList.Add($"  • {vm.Def.DisplayName} - {ver ?? "_"}");
                admobList.Add($"      ({parenContent})");
            }
            if (admobList.Count > 0)
            {
                if (lines.Count > 0) lines.Add("");
                lines.Add("AdMob / GMA:");
                lines.AddRange(admobList);
            }
            if (lines.Count == 0) return "Install 0 adapter(s)?";
#if USE_APPLOVIN
            var total = (alList.Count / 2) + (admobList.Count / 2);
#else
            var total = admobList.Count / 2;
#endif
            lines.Insert(0, $"Install {total} adapter(s):");
            lines.Insert(1, "");
            return string.Join("\n", lines);
        }
        private int GetSelectedAdaptersCount()
        {
            int n = 0;
#if USE_APPLOVIN
            foreach (var a in AdSDKDataCache.AppLovinAdapters)
            {
                if (a == null) continue;
                var canInstall = (a.Action == AppLovinIntegrationBridge.ActionType.Install || a.Action == AppLovinIntegrationBridge.ActionType.Upgrade) && a.Network != null;
                var id = a.Name ?? a.DisplayName ?? "";
                if (canInstall && _selectedAppLovinAdapterIds.Contains(id)) n++;
            }
#endif
            foreach (var vm in AdSDKDataCache.AdmobAdapters)
            {
                if (vm?.Def == null) continue;
                var versionOptions = _admobVersionCache.TryGetValue(vm.Def.PackageId, out var v) ? v : null;
                var latest = _admobLatestCache.TryGetValue(vm.Def.PackageId, out var l) ? l : null;
                var canInstall = !vm.IsInstalled && (versionOptions?.Count > 0 || !string.IsNullOrEmpty(latest)) && !_admobAdapterInstalling;
                var canUpdate = vm.IsInstalled && !string.IsNullOrEmpty(latest) && VersionComparisonService.Compare(latest, vm.InstalledVersion ?? "") > 0 && !_admobAdapterInstalling;
                if ((canInstall || canUpdate) && _selectedAdMobAdapterIds.Contains(vm.Def.PackageId)) n++;
            }
            return n;
        }
        private void InstallSelectedAdapters()
        {
#if USE_APPLOVIN
            var alToInstall = new List<AppLovinMax.Scripts.IntegrationManager.Editor.Network>();
            var alDisplayNames = new List<string>();
            foreach (var a in AdSDKDataCache.AppLovinAdapters)
            {
                if (a == null) continue;
                var canInstall = (a.Action == AppLovinIntegrationBridge.ActionType.Install || a.Action == AppLovinIntegrationBridge.ActionType.Upgrade) && a.Network != null;
                var id = a.Name ?? a.DisplayName ?? "";
                if (canInstall && _selectedAppLovinAdapterIds.Contains(id) && a.Network != null)
                {
                    alToInstall.Add(a.Network);
                    alDisplayNames.Add(a.DisplayName ?? a.Name ?? "AppLovin");
                }
            }
            if (alToInstall.Count > 0)
            {
                _appLovinAdapterInstalling = true;
                _appLovinAdapterProgress = $"Installing {alDisplayNames[0]} (1/{alToInstall.Count})...";
                foreach (var n in alToInstall)
                    _selectedAppLovinAdapterIds.Remove(n.Name);
                AppLovinIntegrationBridge.InstallAdapters(alToInstall,
                    (cur, tot) =>
                    {
                        var idx = Math.Max(0, Math.Min(cur - 1, alDisplayNames.Count - 1));
                        _appLovinAdapterProgress = $"Installing {alDisplayNames[idx]} ({cur}/{tot})...";
                        Repaint();
                    },
                    () =>
                    {
                        _appLovinAdapterInstalling = false;
                        _appLovinAdapterProgress = null;
                        FetchAndUpdateCache();
                        TryInstallSelectedAdMobAdapters();
                        Repaint();
                    },
                    () => { FetchAndUpdateCache(); Repaint(); });
                return;
            }
#endif
            TryInstallSelectedAdMobAdapters();
        }
        private void TryInstallSelectedAdMobAdapters()
        {
            var admobQueue = new List<(string PackageId, string Version)>();
            foreach (var vm in AdSDKDataCache.AdmobAdapters)
            {
                if (vm?.Def == null || !_selectedAdMobAdapterIds.Contains(vm.Def.PackageId)) continue;
                var versionOptions = _admobVersionCache.TryGetValue(vm.Def.PackageId, out var vers) ? vers : null;
                var latest = _admobLatestCache.TryGetValue(vm.Def.PackageId, out var l) ? l : null;
                var canInstall = !vm.IsInstalled && (versionOptions?.Count > 0 || !string.IsNullOrEmpty(latest));
                var canUpdate = vm.IsInstalled && !string.IsNullOrEmpty(latest) && VersionComparisonService.Compare(latest, vm.InstalledVersion ?? "") > 0;
                if (!canInstall && !canUpdate) continue;
                var ver = canUpdate ? latest : (_admobSelectedVersionByPackage.TryGetValue(vm.Def.PackageId, out var sv) && !string.IsNullOrEmpty(sv) ? sv : (latest ?? (versionOptions?.Count > 0 ? versionOptions[0] : null)));
                if (!string.IsNullOrEmpty(ver)) admobQueue.Add((vm.Def.PackageId, ver));
            }
            if (admobQueue.Count == 0) return;
            _admobAdapterInstalling = true;
            InstallAdMobAdapterQueue(admobQueue, 0);
        }
        private void SavePendingAdMobQueue(List<(string PackageId, string Version)> queue, int nextIndex)
        {
            if (queue == null || queue.Count == 0 || nextIndex >= queue.Count)
            {
                EditorPrefs.DeleteKey(PendingAdMobQueueKey);
                EditorPrefs.DeleteKey(PendingAdMobIndexKey);
                return;
            }
            EditorPrefs.SetString(PendingAdMobQueueKey, string.Join(";", queue.Select(x => $"{x.PackageId}|{x.Version}")));
            EditorPrefs.SetInt(PendingAdMobIndexKey, nextIndex);
        }
        private void TryResumePendingAdMobInstallQueue()
        {
            var queueStr = EditorPrefs.GetString(PendingAdMobQueueKey, null);
            if (string.IsNullOrEmpty(queueStr)) return;
            var idx = EditorPrefs.GetInt(PendingAdMobIndexKey, 0);
            var queue = new List<(string PackageId, string Version)>();
            foreach (var item in queueStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split(new[] { '|' }, 2, StringSplitOptions.None);
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0]) && !string.IsNullOrEmpty(parts[1]))
                    queue.Add((parts[0], parts[1]));
            }
            if (queue.Count == 0) { EditorPrefs.DeleteKey(PendingAdMobQueueKey); EditorPrefs.DeleteKey(PendingAdMobIndexKey); return; }
            while (idx < queue.Count && AdMobAdapterInstaller.IsInstalled(queue[idx].PackageId))
                idx++;
            if (idx >= queue.Count)
            {
                EditorPrefs.DeleteKey(PendingAdMobQueueKey);
                EditorPrefs.DeleteKey(PendingAdMobIndexKey);
                return;
            }
            _admobAdapterInstalling = true;
            InstallAdMobAdapterQueue(queue, idx);
            Repaint();
        }
        private void InstallAdMobAdapterQueue(List<(string PackageId, string Version)> queue, int index)
        {
            if (index >= queue.Count)
            {
                _admobAdapterInstalling = false;
                _admobAdapterProgress = null;
                _admobAdapterInstallingPackageId = null;
                SavePendingAdMobQueue(null, 0);
                Repaint();
                return;
            }
            var (packageId, version) = queue[index];
            var def = AdMobAdapterConfig.AllAdapters.FirstOrDefault(a => a?.PackageId == packageId);
            _admobAdapterInstallingPackageId = packageId;
            _admobAdapterProgress = $"Preparing {def?.DisplayName ?? packageId} ({index + 1}/{queue.Count})...";
            Repaint();
            void DoInstall(string installVersion)
            {
                _selectedAdMobAdapterIds.Remove(packageId);
                SavePendingAdMobQueue(queue, index + 1);
                _admobAdapterProgress = $"Installing {def?.DisplayName ?? packageId} ({index + 1}/{queue.Count})...";
                Repaint();
                AdMobAdapterInstaller.Install(packageId, installVersion, (ok, err) =>
                {
                    if (ok)
                    {
                        EditorPrefs.SetString(ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + packageId, installVersion);
                        _admobSelectedVersionByPackage[packageId] = installVersion;
                        UpdateAdMobAdapterStateInCache(packageId, true, installVersion);
                    }
                    else
                    {
                        AdapterInstallDialogService.ShowInstallFailed(!string.IsNullOrEmpty(err) ? err : "Install failed.");
                    }
                    Repaint();
                    InstallAdMobAdapterQueue(queue, index + 1);
                });
            }
            if (def != null)
            {
                AdMobAdaptersWindow.RunAdMobAdapterInstallForAdSDK(def, version, (resolvedVersion, precheckError) =>
                {
                    if (!string.IsNullOrEmpty(precheckError))
                        AdapterInstallDialogService.ShowInstallFailed(precheckError);
                    if (resolvedVersion != null)
                        DoInstall(resolvedVersion);
                    else
                        InstallAdMobAdapterQueue(queue, index + 1);
                });
            }
            else
            {
                DoInstall(version);
            }
        }
        private void FetchAndUpdateCache()
        {
            AssetDatabase.Refresh();
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var maxSdkInstalled = false;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var maxPath = Path.Combine(projectRoot, MaxSdkPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                maxSdkInstalled = Directory.Exists(maxPath);
            }
            var admobInstalled = AdSDKWindowUtil.IsGoogleMobileAdsInstalled();
            var appLovinRaw = AdSDKWindowUtil.GetAppLovinPluginInfo(maxSdkInstalled);
            var admobRaw = AdSDKWindowUtil.GetAdMobPluginInfo(admobInstalled);
            if (maxSdkInstalled)
            {
                if (LockFileSerializer.TryGetInstalledVersion("applovinsdk", out var al) && !string.IsNullOrEmpty(al))
                    appLovinRaw = (al, appLovinRaw.Android, appLovinRaw.Ios);
                else if (ModuleVersionSelectionStore.TryGet(new ModuleId("applovinsdk"), out var al2) && !string.IsNullOrEmpty(al2))
                    appLovinRaw = (al2, appLovinRaw.Android, appLovinRaw.Ios);
            }
            if (admobInstalled)
            {
                if (LockFileSerializer.TryGetInstalledVersion("googleadsmobile", out var gm) && !string.IsNullOrEmpty(gm))
                    admobRaw = (gm, admobRaw.Android, admobRaw.Ios);
                else if (ModuleVersionSelectionStore.TryGet(new ModuleId("googleadsmobile"), out var gm2) && !string.IsNullOrEmpty(gm2))
                    admobRaw = (gm2, admobRaw.Android, admobRaw.Ios);
            }
            AdSDKDataCache.MaxSdkInstalled = maxSdkInstalled;
            AdSDKDataCache.AdmobInstalled = admobInstalled;
            AdSDKDataCache.AppLovinPluginInfo = appLovinRaw;
            AdSDKDataCache.AdMobPluginInfo = admobRaw;
#if USE_APPLOVIN
            AdSDKDataCache.AppLovinAdapters.Clear();
            if (maxSdkInstalled && AppLovinIntegrationBridge.IsAvailable())
            {
                var alList = AppLovinIntegrationBridge.LoadAdapters();
                if (alList != null)
                    AdSDKDataCache.AppLovinAdapters.AddRange(alList);
            }
#endif
            AdSDKDataCache.AdmobAdapters.Clear();
            if (admobInstalled)
            {
                var discovered = MediationCompatibilityService.DiscoverInstalledAdMobAdapters();
                var slugsToPrefetch = new List<string>();
                foreach (var def in AdMobAdapterConfig.AllAdapters)
                {
                    if (def == null) continue;
                    var installed = AdMobAdapterInstaller.IsInstalled(def.PackageId);
                    var installedVer = installed ? AdMobAdapterInstaller.GetInstalledVersion(def.PackageId) : null;
                    if (installed && string.IsNullOrEmpty(installedVer))
                        installedVer = EditorPrefs.GetString(ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + def.PackageId, null);
                    if (installed && string.IsNullOrEmpty(installedVer))
                    {
                        var disc = discovered.FirstOrDefault(d => d.Def?.PackageId == def.PackageId);
                        if (disc.Def != null && (!string.IsNullOrEmpty(disc.AndroidVersion) || !string.IsNullOrEmpty(disc.IosVersion)))
                        {
                            var resolved = AdMobChangelogVersionFetcher.ResolveUnityVersionFromSupportVersions(def.IntegrationSlug, disc.AndroidVersion, disc.IosVersion);
                            if (!string.IsNullOrEmpty(resolved)) installedVer = resolved;
                            else if (!string.IsNullOrEmpty(def.IntegrationSlug)) slugsToPrefetch.Add(def.IntegrationSlug);
                        }
                    }
                    AdSDKDataCache.AdmobAdapters.Add(new AdMobAdapterViewModel
                    {
                        Def = def,
                        IsInstalled = installed,
                        InstalledVersion = installedVer
                    });
                }
                if (slugsToPrefetch.Count > 0)
                    AdMobChangelogVersionFetcher.PrefetchAllAdaptersAsync(slugsToPrefetch.Distinct(), () => { EditorApplication.delayCall += () => { FetchAndUpdateCache(); Repaint(); }; });
            }
            AdSDKDataCache.IsValid = true;
        }
        private void UpdateAdMobAdapterStateInCache(string packageId, bool installed, string version)
        {
            var vm = AdSDKDataCache.AdmobAdapters.FirstOrDefault(x => x?.Def != null && x.Def.PackageId == packageId);
            if (vm != null)
            {
                vm.IsInstalled = installed;
                vm.InstalledVersion = installed ? version : null;
            }
        }
        private void OnGUI()
        {
            if (_admobAdapterInstalling && !string.IsNullOrEmpty(_admobAdapterInstallingPackageId))
            {
                var inInstallPhase = !string.IsNullOrEmpty(_admobAdapterProgress) &&
                    (_admobAdapterProgress.StartsWith("Installing", StringComparison.OrdinalIgnoreCase) ||
                     _admobAdapterProgress.StartsWith("Updating", StringComparison.OrdinalIgnoreCase));
                if (inInstallPhase && (AdMobAdapterInstaller.IsInstalled(_admobAdapterInstallingPackageId) || !AdMobAdapterInstaller.IsOperationInProgress))
                {
                    var pkg = _admobAdapterInstallingPackageId;
                    _admobAdapterInstalling = false;
                    _admobAdapterProgress = null;
                    _admobAdapterInstallingPackageId = null;
                    var installedVer = AdMobAdapterInstaller.GetInstalledVersion(pkg);
                    UpdateAdMobAdapterStateInCache(pkg, AdMobAdapterInstaller.IsInstalled(pkg), installedVer);
                    if (!string.IsNullOrEmpty(installedVer))
                        _admobSelectedVersionByPackage[pkg] = installedVer;
                }
            }
            var contentW = position.width;
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("Ad SDK", titleStyle, GUILayout.Width(60));
            EditorGUILayout.LabelField("", GUILayout.ExpandWidth(true));
            var anySelected = GetSelectedAdaptersCount() > 0;
            var anyInstalling = _admobAdapterInstalling
#if USE_APPLOVIN
                || _appLovinAdapterInstalling
#endif
                || AdMobAdapterInstaller.IsOperationInProgress;
            EditorGUI.BeginDisabledGroup(!anySelected || anyInstalling);
            if (GUILayout.Button("Install selected", GUILayout.Width(100)))
            {
                if (anyInstalling)
                    return;
                var count = GetSelectedAdaptersCount();
                if (count > 0 && EditorUtility.DisplayDialog("Install Selected", GetInstallSelectedDialogMessage(), "Install", "Cancel"))
                    InstallSelectedAdapters();
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                OnRefreshClicked();
            }
            if (GUILayout.Button("Check compatibility", GUILayout.Width(130)))
            {
                MediationCompatibilityWindow.Open(() => { FetchAndUpdateCache(); Repaint(); });
            }
            if (GUILayout.Button("SDK Manager", GUILayout.Width(90)))
                CompanySDKWindow.Open();
            EditorGUILayout.EndHorizontal();
            if (_cachedCompatibilityMismatchCount > 0)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(new GUIContent("⚠"), GUILayout.Width(20));
                EditorGUILayout.LabelField($"{_cachedCompatibilityMismatchCount} compatibility issue(s) detected.", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Check & Fix", GUILayout.Width(90)))
                    MediationCompatibilityWindow.Open(() => { FetchAndUpdateCache(); Repaint(); });
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            DrawPluginHeaders(contentW);
            _pluginBridge?.TickQueues();
        }
        private const float PluginHeaderHeight = 150f;
        private const float AdapterRowHeight = 28f;
        private const float AdapterHeaderHeight = 24f;
        private const float PluginEdgePadding = 5f;
        private const float ColumnGap = 8f;
        private const float MinWindowWidth = ShionSDKConstants.Window.MinAdSDKWidth;
        private const float InitialWindowWidth = ShionSDKConstants.Window.InitialAdSDKWidth;
        private const float InitialWindowHeight = ShionSDKConstants.Window.InitialAdSDKHeight;
        private void DrawPluginHeaders(float width)
        {
            var adapterSectionHeight = Mathf.Max(200f, position.height - 190f);
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Space(PluginEdgePadding);
            var boxStyle = new GUIStyle("box") { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(0, 0, 0, 0) };
            var contentW = width - 2f * PluginEdgePadding - ColumnGap;
            var halfW = contentW * 0.5f;
            EditorGUILayout.BeginVertical(GUILayout.Width(halfW), GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical(boxStyle, GUILayout.Height(PluginHeaderHeight));
            DrawPluginHeader("AppLovin MAX", AdSDKDataCache.MaxSdkInstalled, AdSDKDataCache.AppLovinPluginInfo,
#if USE_APPLOVIN
                AppLovinIntegrationBridge.IsAvailable(),
#else
                true,
#endif
                null, "applovinsdk");
            EditorGUILayout.EndVertical();
#if USE_APPLOVIN
            if (AdSDKDataCache.MaxSdkInstalled)
                DrawAppLovinAdaptersSection(halfW - 24f, adapterSectionHeight);
#endif
            EditorGUILayout.EndVertical();
            GUILayout.Space(ColumnGap);
            EditorGUILayout.BeginVertical(GUILayout.Width(halfW), GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical(boxStyle, GUILayout.Height(PluginHeaderHeight));
            DrawPluginHeader("AdMob / GMA", AdSDKDataCache.AdmobInstalled, AdSDKDataCache.AdMobPluginInfo, true, null, "googleadsmobile");
            EditorGUILayout.EndVertical();
            if (AdSDKDataCache.AdmobInstalled)
                DrawAdMobAdaptersSection(halfW - 24f, adapterSectionHeight);
            EditorGUILayout.EndVertical();
            GUILayout.Space(PluginEdgePadding);
            EditorGUILayout.EndHorizontal();
        }
#if USE_APPLOVIN
        private void DrawAppLovinAdaptersSection(float width, float sectionHeight)
        {
            var adapterBoxStyle = new GUIStyle(GUI.skin.box) { margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(5, 5, 4, 4) };
            EditorGUILayout.BeginVertical(adapterBoxStyle);
            EditorGUILayout.Space(4);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            EditorGUILayout.BeginVertical("box", GUILayout.Height(AdapterHeaderHeight));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", headerStyle, GUILayout.Width(22));
            EditorGUILayout.LabelField("Adapter", headerStyle, GUILayout.Width(150));
            EditorGUILayout.LabelField("Status", headerStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField("Support", headerStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", headerStyle, GUILayout.Width(86));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            if (_appLovinAdapterInstalling && !string.IsNullOrEmpty(_appLovinAdapterProgress))
                EditorGUILayout.LabelField(_appLovinAdapterProgress, EditorStyles.miniLabel);
            _appLovinAdapterScroll = EditorGUILayout.BeginScrollView(_appLovinAdapterScroll, GUILayout.Height(sectionHeight - AdapterHeaderHeight - 24f));
            foreach (var a in AdSDKDataCache.AppLovinAdapters)
            {
                if (a == null) continue;
                var typeStr = a.Action.ToString();
                var typeColor = a.Action == AppLovinIntegrationBridge.ActionType.Installed ? new Color(0.2f, 0.8f, 0.2f) :
                    a.Action == AppLovinIntegrationBridge.ActionType.Upgrade ? new Color(1f, 0.6f, 0f) : new Color(0.5f, 0.5f, 0.5f);
                var supportStr = string.IsNullOrEmpty(a.AndroidVersion) && string.IsNullOrEmpty(a.IosVersion)
                    ? "Android: _ - iOS: _" : $"Android: {a.AndroidVersion ?? "_"} - iOS: {a.IosVersion ?? "_"}";
                EditorGUILayout.BeginHorizontal(GUILayout.Height(AdapterRowHeight));
                var canInstall = (a.Action == AppLovinIntegrationBridge.ActionType.Install || a.Action == AppLovinIntegrationBridge.ActionType.Upgrade) && a.Network != null;
                var adapterId = a.Name ?? a.DisplayName ?? "";
                var isSelected = _selectedAppLovinAdapterIds.Contains(adapterId);
                if (canInstall)
                {
                    var newSel = EditorGUILayout.Toggle(isSelected, GUILayout.Width(22));
                    if (newSel != isSelected)
                    {
                        if (newSel) _selectedAppLovinAdapterIds.Add(adapterId);
                        else _selectedAppLovinAdapterIds.Remove(adapterId);
                    }
                }
                else
                    EditorGUILayout.LabelField("", GUILayout.Width(22));
                EditorGUILayout.LabelField(a.DisplayName ?? a.Name ?? "-", EditorStyles.label, GUILayout.Width(150));
                var typeStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = typeColor } };
                EditorGUILayout.LabelField(typeStr, typeStyle, GUILayout.Width(72));
                EditorGUILayout.LabelField(supportStr, EditorStyles.label, GUILayout.ExpandWidth(true));
                var anyInstallingAl = _admobAdapterInstalling || _appLovinAdapterInstalling || AdMobAdapterInstaller.IsOperationInProgress;
                EditorGUI.BeginDisabledGroup(anyInstallingAl || !canInstall);
                if (GUILayout.Button(a.Action == AppLovinIntegrationBridge.ActionType.Upgrade ? "Upgrade" : "Install", GUILayout.Height(22), GUILayout.Width(56)))
                {
                    var displayName = a.DisplayName ?? a.Name ?? "AppLovin";
                    _appLovinAdapterInstalling = true;
                    _appLovinAdapterProgress = $"Installing {displayName} (1/1)...";
                    AppLovinIntegrationBridge.InstallAdapters(new List<AppLovinMax.Scripts.IntegrationManager.Editor.Network> { a.Network },
                        (cur, tot) => { _appLovinAdapterProgress = $"Installing {displayName} ({cur}/{tot})..."; Repaint(); },
                        () => { _appLovinAdapterInstalling = false; _appLovinAdapterProgress = null; FetchAndUpdateCache(); Repaint(); },
                        () => { FetchAndUpdateCache(); Repaint(); });
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(anyInstallingAl || !a.IsInstalled || a.Network == null);
                if (GUILayout.Button(GetTrashIconContent(), GUILayout.Height(22), GUILayout.Width(26)))
                {
                    if (EditorUtility.DisplayDialog("Remove Adapter", $"Remove {a.DisplayName ?? a.Name ?? "adapter"}?", "Remove", "Cancel"))
                    {
                        AppLovinIntegrationBridge.RemoveAdapters(new List<AppLovinMax.Scripts.IntegrationManager.Editor.Network> { a.Network });
                        FetchAndUpdateCache();
                        Repaint();
                    }
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
#endif
        private void DrawAdMobAdaptersSection(float width, float sectionHeight)
        {
            var adapterBoxStyle = new GUIStyle(GUI.skin.box) { margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(5, 5, 4, 4) };
            EditorGUILayout.BeginVertical(adapterBoxStyle);
            EditorGUILayout.Space(4);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            EditorGUILayout.BeginVertical("box", GUILayout.Height(AdapterHeaderHeight));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", headerStyle, GUILayout.Width(22));
            EditorGUILayout.LabelField("Adapter", headerStyle, GUILayout.Width(150));
            EditorGUILayout.LabelField("Status", headerStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField("Version", headerStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField("Support", headerStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("", headerStyle, GUILayout.Width(86));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            if (_admobAdapterInstalling && !string.IsNullOrEmpty(_admobAdapterProgress))
                EditorGUILayout.LabelField(_admobAdapterProgress, EditorStyles.miniLabel);
            _admobAdapterScroll = EditorGUILayout.BeginScrollView(_admobAdapterScroll, GUILayout.Height(sectionHeight - AdapterHeaderHeight - 24f));
            foreach (var vm in AdSDKDataCache.AdmobAdapters)
            {
                if (vm?.Def == null) continue;
                if (!_admobVersionCache.ContainsKey(vm.Def.PackageId))
                {
                    var pkgId = vm.Def.PackageId;
                    if (TryLoadAdMobVersionCacheFromEditorPrefs(pkgId))
                        {  }
                    else
                    {
                        AdMobChangelogVersionFetcher.FetchVersionListAsync(vm.Def.IntegrationSlug, (versions, latest, _) =>
                        {
                            if (versions != null)
                            {
                                _admobVersionCache[pkgId] = versions;
                                SaveAdMobVersionCacheToEditorPrefs(pkgId, versions, latest);
                            }
                            if (!string.IsNullOrEmpty(latest)) _admobLatestCache[pkgId] = latest;
                            Repaint();
                        });
                    }
                }
                var versionOptions = _admobVersionCache.TryGetValue(vm.Def.PackageId, out var vers) ? vers : null;
                var latest = _admobLatestCache.TryGetValue(vm.Def.PackageId, out var l) ? l : null;
                var displayVer = vm.IsInstalled ? vm.InstalledVersion : (latest ?? (versionOptions?.Count > 0 ? versionOptions[0] : null));
                if (!_admobSelectedVersionByPackage.TryGetValue(vm.Def.PackageId, out var selectedVer) || string.IsNullOrEmpty(selectedVer))
                    selectedVer = displayVer ?? (versionOptions?.Count > 0 ? versionOptions[0] : null);
                if (vm.IsInstalled && !string.IsNullOrEmpty(vm.InstalledVersion))
                {
                    selectedVer = vm.InstalledVersion;
                    _admobSelectedVersionByPackage[vm.Def.PackageId] = vm.InstalledVersion;
                }
                else if (vm.IsInstalled && string.IsNullOrEmpty(selectedVer) && !string.IsNullOrEmpty(latest))
                {
                    selectedVer = latest;
                    _admobSelectedVersionByPackage[vm.Def.PackageId] = latest;
                }
                LoadAdMobSupportIfNeeded(vm, selectedVer);
                var supportKey = $"{vm.Def.PackageId}|{selectedVer}";
                string supportStr;
                if (_admobSupportVersionCache.TryGetValue(supportKey, out var support))
                    supportStr = $"Android: {support.Android ?? "_"} - iOS: {support.Ios ?? "_"}";
                else
                    supportStr = string.IsNullOrEmpty(selectedVer) ? "_" : "...";
                var admobInstalling = _admobAdapterInstalling || AdMobAdapterInstaller.IsOperationInProgress;
                var canInstall = !vm.IsInstalled && (versionOptions?.Count > 0 || !string.IsNullOrEmpty(latest)) && !admobInstalling;
                var installedVerForUpdate = vm.InstalledVersion ?? "";
                var canUpdate = vm.IsInstalled && !string.IsNullOrEmpty(latest) && VersionComparisonService.Compare(latest, installedVerForUpdate) > 0 && !admobInstalling;
                string statusStr;
                Color statusColor;
                if (vm.IsInstalled)
                {
                    statusStr = canUpdate ? "Upgrade" : "Installed";
                    statusColor = canUpdate ? new Color(1f, 0.6f, 0f) : new Color(0.2f, 0.8f, 0.2f);
                }
                else
                {
                    statusStr = "Install";
                    statusColor = new Color(0.5f, 0.5f, 0.5f);
                }
                EditorGUILayout.BeginHorizontal(GUILayout.Height(AdapterRowHeight));
                var canDoInstallOrUpdateEarly = canInstall || canUpdate;
                var admobSelected = _selectedAdMobAdapterIds.Contains(vm.Def.PackageId);
                if (canDoInstallOrUpdateEarly)
                {
                    var newAdmobSel = EditorGUILayout.Toggle(admobSelected, GUILayout.Width(22));
                    if (newAdmobSel != admobSelected)
                    {
                        if (newAdmobSel) _selectedAdMobAdapterIds.Add(vm.Def.PackageId);
                        else _selectedAdMobAdapterIds.Remove(vm.Def.PackageId);
                    }
                }
                else
                    EditorGUILayout.LabelField("", GUILayout.Width(22));
                EditorGUILayout.LabelField(vm.Def.DisplayName, EditorStyles.label, GUILayout.Width(150));
                var statusStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = statusColor } };
                EditorGUILayout.LabelField(statusStr, statusStyle, GUILayout.Width(72));
                if (vm.IsInstalled)
                {
                    var verToShow = !string.IsNullOrEmpty(selectedVer) ? selectedVer : (vm.InstalledVersion ?? "_");
                    EditorGUILayout.LabelField(verToShow, EditorStyles.label, GUILayout.Width(80));
                }
                else
                {
                    var labels = versionOptions?.Count > 0 ? versionOptions.ToArray() : new[] { selectedVer ?? "_" };
                    var idx = versionOptions?.IndexOf(selectedVer ?? "") ?? -1;
                    if (idx < 0) idx = 0;
                    var newIdx = EditorGUILayout.Popup(idx, labels, GUILayout.Width(80));
                    if (versionOptions != null && versionOptions.Count > 0 && newIdx >= 0 && newIdx < versionOptions.Count)
                    {
                        var newVer = versionOptions[newIdx];
                        if (newVer != selectedVer)
                            _admobSelectedVersionByPackage[vm.Def.PackageId] = newVer;
                    }
                }
                EditorGUILayout.LabelField(supportStr, EditorStyles.label, GUILayout.ExpandWidth(true));
                var canDoInstallOrUpdate = canInstall || canUpdate;
                var installOrUpdateLabel = canUpdate ? "Update" : "Install";
                var installOrUpdateVer = canUpdate ? latest : selectedVer;
                var anyInstallingAm = _admobAdapterInstalling
#if USE_APPLOVIN
                    || _appLovinAdapterInstalling
#endif
                    || AdMobAdapterInstaller.IsOperationInProgress;
                EditorGUI.BeginDisabledGroup(!canDoInstallOrUpdate || anyInstallingAm);
                if (GUILayout.Button(installOrUpdateLabel, GUILayout.Height(22), GUILayout.Width(56)))
                {
                    if (anyInstallingAm)
                        { EditorGUI.EndDisabledGroup(); EditorGUILayout.EndHorizontal(); continue; }
                    if (!canDoInstallOrUpdate)
                    {
                        AdapterInstallDialogService.ShowVersionsLoading();
                        EditorGUI.EndDisabledGroup(); EditorGUILayout.EndHorizontal(); continue;
                    }
                    if (AdMobAdapterInstaller.IsOperationInProgress)
                    {
                        AdapterInstallDialogService.ShowOperationInProgress();
                        EditorGUI.EndDisabledGroup(); EditorGUILayout.EndHorizontal(); continue;
                    }
                    var ver = !string.IsNullOrEmpty(installOrUpdateVer) ? installOrUpdateVer : (versionOptions?.Count > 0 ? versionOptions[0] : null);
                    if (string.IsNullOrEmpty(ver))
                    {
                        AdapterInstallDialogService.ShowInstallFailed("Invalid adapter version.");
                        EditorGUI.EndDisabledGroup(); EditorGUILayout.EndHorizontal(); continue;
                    }
                    var def = vm.Def;
                    var displayName = vm.Def.DisplayName;
                    var pkgId = vm.Def.PackageId;
                    var isUpdate = canUpdate;
                    _admobAdapterInstalling = true;
                    _admobAdapterInstallingPackageId = pkgId;
                    _admobAdapterProgress = "Installing...";
                    EditorApplication.delayCall += Repaint;
                    Repaint();
                    AdMobAdaptersWindow.RunAdMobAdapterInstallForAdSDK(def, ver, (resolvedVersion, precheckError) =>
                    {
                        if (!string.IsNullOrEmpty(precheckError))
                            AdapterInstallDialogService.ShowInstallFailed(precheckError);
                        if (resolvedVersion == null)
                        {
                            _admobAdapterInstalling = false;
                            _admobAdapterProgress = null;
                            _admobAdapterInstallingPackageId = null;
                            Repaint();
                            return;
                        }
                        _admobAdapterProgress = $"{(isUpdate ? "Updating" : "Installing")} {displayName} (1/1)...";
                        Repaint();
                        AdMobAdapterInstaller.Install(pkgId, resolvedVersion, (ok, err) =>
                        {
                            if (ok)
                            {
                                EditorPrefs.SetString(ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + pkgId, resolvedVersion);
                                _admobSelectedVersionByPackage[pkgId] = resolvedVersion;
                                UpdateAdMobAdapterStateInCache(pkgId, true, resolvedVersion);
                            }
                            else
                            {
                                AdapterInstallDialogService.ShowInstallFailed(!string.IsNullOrEmpty(err) ? err : "Install failed.");
                            }
                            _admobAdapterInstalling = false;
                            _admobAdapterProgress = null;
                            _admobAdapterInstallingPackageId = null;
                            Repaint();
                        });
                    });
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(anyInstallingAm || !vm.IsInstalled);
                if (GUILayout.Button(GetTrashIconContent(), GUILayout.Height(22), GUILayout.Width(26)))
                {
                    if (!AdapterInstallDialogService.ConfirmRemoveAdapter(vm.Def.DisplayName))
                    { EditorGUI.EndDisabledGroup(); EditorGUILayout.EndHorizontal(); continue; }
                    AdMobAdapterInstaller.Uninstall(vm.Def.PackageId, (ok, err) =>
                    {
                        if (!ok)
                            AdapterInstallDialogService.ShowUninstallFailed(!string.IsNullOrEmpty(err) ? err : "Uninstall failed.");
                        else
                        {
                            EditorPrefs.DeleteKey(ShionSDKConstants.EditorPrefsKeys.AdMobAdapterPrefix + vm.Def.PackageId);
                            _admobSelectedVersionByPackage.Remove(vm.Def.PackageId);
                            UpdateAdMobAdapterStateInCache(vm.Def.PackageId, false, null);
                        }
                        Repaint();
                    });
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        private GUIContent GetTrashIconContent()
        {
            if (_trashIconContent != null) return _trashIconContent;
            var names = new[] { "TreeEditor.Trash", "d_TreeEditor.Trash", "winbtn_win_delete", "d_winbtn_win_delete" };
            foreach (var n in names)
            {
                var icon = EditorGUIUtility.IconContent(n);
                if (icon != null && icon.image != null)
                {
                    _trashIconContent = new GUIContent(icon) { tooltip = "Remove" };
                    return _trashIconContent;
                }
            }
            _trashIconContent = new GUIContent("×", "Remove");
            return _trashIconContent;
        }
        private void LoadAdMobSupportIfNeeded(AdMobAdapterViewModel vm, string selectedVersion)
        {
            if (string.IsNullOrEmpty(selectedVersion) || selectedVersion == "_" || !selectedVersion.Contains(".")) return;
            var cacheKey = $"{vm.Def.PackageId}|{selectedVersion}";
            if (_admobSupportVersionCache.ContainsKey(cacheKey)) return;
            if (TryLoadAdMobSupportFromEditorPrefs(cacheKey, out var cached))
            {
                _admobSupportVersionCache[cacheKey] = cached;
                return;
            }
            AdMobChangelogVersionFetcher.FetchSupportedVersionsAsync(vm.Def.IntegrationSlug, selectedVersion, (androidVer, iosVer, error) =>
            {
                var support = (error == null ? (androidVer ?? "_") : "_", error == null ? (iosVer ?? "_") : "_");
                _admobSupportVersionCache[cacheKey] = support;
                SaveAdMobSupportToEditorPrefs(cacheKey, support);
                Repaint();
            });
        }
        private const string AdMobVersionCachePrefix = "ShionSDK.AdSDK.AdMobVersions.";
        private const string AdMobLatestCachePrefix = "ShionSDK.AdSDK.AdMobLatest.";
        private const string AdMobSupportCachePrefix = "ShionSDK.AdSDK.AdMobSupport.";
        private const string AdMobSupportKeysListKey = "ShionSDK.AdSDK.AdMobSupportKeys";
        private bool TryLoadAdMobVersionCacheFromEditorPrefs(string packageId)
        {
            var versKey = AdMobVersionCachePrefix + packageId;
            var versStr = EditorPrefs.GetString(versKey, null);
            if (string.IsNullOrEmpty(versStr)) return false;
            var list = versStr.Split(new[] { '\x1f' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (list.Count == 0) return false;
            _admobVersionCache[packageId] = list;
            var latest = EditorPrefs.GetString(AdMobLatestCachePrefix + packageId, null);
            if (!string.IsNullOrEmpty(latest)) _admobLatestCache[packageId] = latest;
            return true;
        }
        private void SaveAdMobVersionCacheToEditorPrefs(string packageId, List<string> versions, string latest)
        {
            if (versions == null || versions.Count == 0) return;
            EditorPrefs.SetString(AdMobVersionCachePrefix + packageId, string.Join("\x1f", versions));
            EditorPrefs.SetString(AdMobLatestCachePrefix + packageId, latest ?? "");
        }
        private bool TryLoadAdMobSupportFromEditorPrefs(string cacheKey, out (string Android, string Ios) support)
        {
            support = default;
            var val = EditorPrefs.GetString(AdMobSupportCachePrefix + cacheKey, null);
            if (string.IsNullOrEmpty(val)) return false;
            var parts = val.Split(new[] { '\x1f' }, 2, StringSplitOptions.None);
            if (parts.Length < 2) return false;
            support = (parts[0], parts[1]);
            return true;
        }
        private void SaveAdMobSupportToEditorPrefs(string cacheKey, (string Android, string Ios) support)
        {
            var fullKey = AdMobSupportCachePrefix + cacheKey;
            EditorPrefs.SetString(fullKey, $"{support.Android}\x1f{support.Ios}");
            var list = EditorPrefs.GetString(AdMobSupportKeysListKey, "").Split(new[] { '\x1f' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!list.Contains(fullKey))
            {
                list.Add(fullKey);
                EditorPrefs.SetString(AdMobSupportKeysListKey, string.Join("\x1f", list));
            }
        }
        private void DrawPluginHeader(string name, bool installed, (string Version, string Android, string Ios) info, bool available, string suggest, string moduleId)
        {
            var titleColor = installed ? new Color(0.15f, 0.7f, 0.25f) : new Color(0.5f, 0.5f, 0.5f);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = titleColor }, fontSize = 12 };
            EditorGUILayout.LabelField(name, titleStyle);
            string selectedVersion = null;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Version:", EditorStyles.miniLabel, GUILayout.Width(50));
            var versions = _pluginBridge?.GetAvailableVersions(moduleId);
            if (installed)
            {
                selectedVersion = info.Version ?? "_";
                EditorGUILayout.LabelField(selectedVersion, EditorStyles.miniLabel);
            }
            else if (versions != null && versions.Count > 0)
            {
                if (!_selectedVersionIndex.TryGetValue(moduleId, out var idx) || idx >= versions.Count)
                    idx = 0;
                var newIdx = EditorGUILayout.Popup(idx, versions.ToArray(), GUILayout.MinWidth(80));
                if (newIdx != idx)
                    _selectedVersionIndex[moduleId] = newIdx;
                selectedVersion = versions[newIdx];
            }
            else
            {
                EditorGUILayout.LabelField(_pluginBridge?.IsVersionsLoading(moduleId) == true ? "Loading..." : "_", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", EditorStyles.miniLabel, GUILayout.Width(50));
            var statusStr = installed ? "Installed" : "Not installed";
            var statusColor = installed ? new Color(0.15f, 0.7f, 0.25f) : new Color(0.6f, 0.6f, 0.6f);
            var statusStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = statusColor } };
            EditorGUILayout.LabelField(statusStr, statusStyle);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Support:", EditorStyles.miniLabel, GUILayout.Width(50));
            var supportStr = GetSupportVersionString(moduleId, installed, info, selectedVersion);
            EditorGUILayout.LabelField(supportStr, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(suggest))
            {
                var suggestStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic, normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };
                EditorGUILayout.LabelField(suggest, suggestStyle);
            }
            var busy = _pluginBridge?.IsBusy(moduleId) ?? false;
            EditorGUI.BeginDisabledGroup(busy);
            EditorGUILayout.BeginHorizontal();
            if (installed)
            {
                if (GUILayout.Button("Uninstall", EditorStyles.miniButton, GUILayout.Width(70)))
                    _pluginBridge?.TryUninstall(moduleId);
            }
            else
            {
                var canInstall = versions != null && versions.Count > 0;
                EditorGUI.BeginDisabledGroup(!canInstall);
                if (GUILayout.Button("Install", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    var selectedIdx = canInstall && _selectedVersionIndex.TryGetValue(moduleId, out var i) && i < versions.Count ? i : 0;
                    var ver = canInstall ? versions[selectedIdx] : null;
                    _pluginBridge?.TryInstall(moduleId, ver);
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
        }
        private string GetSupportVersionString(string moduleId, bool installed, (string Version, string Android, string Ios) info, string selectedVersion)
        {
            if (installed)
                return $"Android: {info.Android ?? "_"} - iOS: {info.Ios ?? "_"}";
            if (string.IsNullOrEmpty(selectedVersion) || selectedVersion == "_") return "Android: _ - iOS: _";
            var cacheKey = $"{moduleId}|{selectedVersion}";
            if (_supportVersionCache.TryGetValue(cacheKey, out var cached))
                return $"Android: {cached.Android ?? "_"} - iOS: {cached.Ios ?? "_"}";
            if (moduleId == "googleadsmobile")
            {
                var pv = AdMobAppLovinCompatibilityService.GetAdMobPluginVersions(selectedVersion);
                if (pv != null)
                {
                    _supportVersionCache[cacheKey] = (pv.AndroidVersion ?? "_", pv.IosVersion ?? "_");
                    return $"Android: {pv.AndroidVersion ?? "_"} - iOS: {pv.IosVersion ?? "_"}";
                }
            }
            else if (moduleId == "applovinsdk")
            {
                var (android, ios) = AdMobAppLovinCompatibilityService.GetAppLovinSdkSupportVersions(selectedVersion);
                if (!string.IsNullOrEmpty(android) || !string.IsNullOrEmpty(ios))
                {
                    var a = android ?? "_";
                    var i = ios ?? "_";
                    _supportVersionCache[cacheKey] = (a, i);
                    return $"Android: {a} - iOS: {i}";
                }
            }
            return "Android: _ - iOS: _";
        }
        private static string ParseAppLovinVersionToSupport(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            var s = tag.Trim().TrimStart('v', 'V');
            if (s.StartsWith("release_", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(8).Replace("_", ".");
            if (s.Length > 0 && char.IsDigit(s[0]))
                return s;
            return null;
        }
    }
    internal sealed class AdSDKPluginInstallBridge
    {
        private readonly Action _repaint;
        private readonly Action _notifyDataDirty;
        private CompanySDKViewModel _vm;
        private CompanySDKPresenter _presenter;
        private bool _versionsLoading;
        public AdSDKPluginInstallBridge(Action repaint, Action notifyDataDirty = null)
        {
            _repaint = repaint ?? (() => { });
            _notifyDataDirty = notifyDataDirty ?? (() => { });
        }
        private void EnsurePresenter()
        {
            if (_presenter != null) return;
            _vm = new CompanySDKViewModel();
            _presenter = new CompanySDKPresenter(_vm, _repaint);
            _presenter.OnEnable();
            _presenter.TryLoadVersionCacheFromEditorPrefs();
        }
        public List<string> GetAvailableVersions(string moduleId)
        {
            if (_vm == null) return null;
            if (_vm.GitInstallableVersionsByModuleId.TryGetValue(moduleId, out var list) && list != null && list.Count > 0)
                return list;
            return null;
        }
        public bool IsVersionsLoading(string moduleId)
        {
            return _versionsLoading;
        }
        public void ForceReloadVersions(System.Action onComplete)
        {
            EnsurePresenter();
            _presenter.OnRefresh();
            EnsureVersionsLoaded(onComplete);
        }
        public void EnsureVersionsLoaded(System.Action onComplete)
        {
            EnsurePresenter();
            if (_vm.GitVersionsScanned)
            {
                onComplete?.Invoke();
                return;
            }
            if (_vm.GitInstallableVersionsByModuleId.Count > 0)
            {
                _vm.GitVersionsScanned = true;
                onComplete?.Invoke();
                return;
            }
            var repo = _presenter.Repository;
            var adModules = new List<Module>();
            var al = repo.Get(new ModuleId("applovinsdk"));
            var gm = repo.Get(new ModuleId("googleadsmobile"));
            if (al != null) adModules.Add(al);
            if (gm != null) adModules.Add(gm);
            if (adModules.Count == 0)
            {
                _vm.GitVersionsScanned = true;
                onComplete?.Invoke();
                return;
            }
            _versionsLoading = true;
            _presenter.EnsureGitInstallableVersionsLoaded(adModules, () =>
            {
                _versionsLoading = false;
                _presenter.SaveVersionCacheToEditorPrefs();
                onComplete?.Invoke();
            });
        }
        public void TryInstall(string moduleId, string version = null)
        {
            EnsurePresenter();
            var module = _presenter.Repository.Get(new ModuleId(moduleId));
            if (module == null) return;
            void DoInstall(string verToUse)
            {
                if (!string.IsNullOrEmpty(verToUse))
                    ModuleVersionSelectionStore.Set(module.Id, verToUse);
                else if (_vm.GitInstallableVersionsByModuleId.TryGetValue(moduleId, out var versions) && versions != null && versions.Count > 0)
                    ModuleVersionSelectionStore.Set(module.Id, versions[0]);
                _presenter.OnInstallClicked(module);
                _notifyDataDirty();
            }
            if (_vm.GitInstallableVersionsByModuleId.TryGetValue(moduleId, out var v) && v != null && v.Count > 0)
            {
                var ver = !string.IsNullOrEmpty(version) && v.Contains(version) ? version : v[0];
                DoInstall(ver);
                return;
            }
            EnsureVersionsLoaded(() =>
            {
                var ver = !string.IsNullOrEmpty(version) ? version : null;
                if (_vm.GitInstallableVersionsByModuleId.TryGetValue(moduleId, out var list) && list != null && list.Count > 0)
                {
                    ver = !string.IsNullOrEmpty(ver) && list.Contains(ver) ? ver : list[0];
                }
                DoInstall(ver);
                _repaint();
            });
        }
        public void TryUninstall(string moduleId)
        {
            EnsurePresenter();
            var module = _presenter.Repository.Get(new ModuleId(moduleId));
            if (module != null)
                _presenter.OnUninstallClicked(module);
        }
        public bool IsBusy(string moduleId)
        {
            if (_vm == null) return false;
            if (_vm.OperationStatus.TryGetValue(moduleId, out var s))
                return s == ModuleOperationStatus.Installing || s == ModuleOperationStatus.Uninstalling || s == ModuleOperationStatus.Waiting;
            return false;
        }
        public void TickQueues()
        {
            if (_presenter == null) return;
            var modules = _presenter.Repository.GetAll().ToList();
            var adIds = new[] { "applovinsdk", "googleadsmobile" };
            var wasBusy = adIds.Any(id => _vm.OperationStatus.TryGetValue(id, out var s) &&
                (s == ModuleOperationStatus.Installing || s == ModuleOperationStatus.Uninstalling || s == ModuleOperationStatus.Waiting));
            _presenter.RefreshInstalledStateCache(modules);
            _presenter.TickQueues();
            if (wasBusy)
            {
                var nowIdle = adIds.All(id => !_vm.OperationStatus.TryGetValue(id, out var s) || s == ModuleOperationStatus.None);
                if (nowIdle)
                    _notifyDataDirty();
            }
        }
    }
    internal static class AdSDKWindowUtil
    {
        public static bool IsGoogleMobileAdsInstalled()
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
        public static string GetAppLovinCompatibleSuggestText()
        {
#if !USE_APPLOVIN
            return null;
#else
            var adapters = AppLovinIntegrationBridge.LoadAdapters();
            if (adapters == null || adapters.Count == 0) return null;
            var installedCount = adapters.Count(a => a != null && a.IsInstalled);
            if (installedCount == 0) return null;
            return $"AppLovin: {installedCount} adapter(s) installed. Ensure AdMob versions match.";
#endif
        }
        public static string GetAdMobCompatibleSuggestText()
        {
            var installed = 0;
            foreach (var def in AdMobAdapterConfig.AllAdapters)
            {
                if (def == null) continue;
                if (AdMobAdapterInstaller.IsInstalled(def.PackageId))
                    installed++;
            }
            if (installed == 0) return null;
            return $"AdMob: {installed} adapter(s) installed. Ensure AppLovin versions match.";
        }
        public static (string Version, string Android, string Ios) GetAppLovinPluginInfo(bool installed)
        {
            if (!installed) return ("_", "_", "_");
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return ("_", "_", "_");
            var editorDir = Path.Combine(projectRoot, "Assets", "MaxSdk", "AppLovin", "Editor");
            if (!Directory.Exists(editorDir)) return ("_", "_", "_");
            var xmlFiles = Directory.GetFiles(editorDir, "*.xml", SearchOption.TopDirectoryOnly);
            foreach (var xmlPath in xmlFiles)
            {
                var content = File.ReadAllText(xmlPath);
                var androidMatch = Regex.Match(content, @"com\.applovin:applovin-sdk:([\d.]+)", RegexOptions.IgnoreCase);
                var iosMatch = Regex.Match(content, @"name\s*=\s*[""']AppLovinSDK[""'][^>]*version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (!iosMatch.Success)
                    iosMatch = Regex.Match(content, @"version\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""']AppLovinSDK[""']", RegexOptions.IgnoreCase);
                var android = androidMatch.Success ? androidMatch.Groups[1].Value.Trim() : null;
                var ios = iosMatch.Success ? iosMatch.Groups[1].Value.Trim() : null;
                if (!string.IsNullOrEmpty(android) || !string.IsNullOrEmpty(ios))
                {
                    var nativeVer = !string.IsNullOrEmpty(android) ? android : ios;
                    var displayVer = ToAppLovinReleaseTag(nativeVer);
                    return (displayVer, android ?? "_", ios ?? "_");
                }
            }
            return ("_", "_", "_");
        }
        private static string ToAppLovinReleaseTag(string nativeVersion)
        {
            if (string.IsNullOrWhiteSpace(nativeVersion)) return nativeVersion ?? "_";
            var s = nativeVersion.Trim().TrimStart('v', 'V');
            if (s.StartsWith("release_", StringComparison.OrdinalIgnoreCase))
                return s;
            if (s.Length > 0 && char.IsDigit(s[0]))
                return "release_" + s.Replace(".", "_");
            return nativeVersion;
        }
        public static (string Version, string Android, string Ios) GetAdMobPluginInfo(bool installed)
        {
            if (!installed) return ("_", "_", "_");
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return ("_", "_", "_");
            string version = "_";
            var changelogPath = Path.Combine(projectRoot, "Assets", "GoogleMobileAds", "CHANGELOG.md");
            if (File.Exists(changelogPath))
            {
                var content = File.ReadAllText(changelogPath);
                var m = Regex.Match(content, @"Version\s+(\d+(?:\.\d+)*)", RegexOptions.IgnoreCase);
                if (m.Success) version = m.Groups[1].Value.Trim();
            }
            string android = "_", ios = "_";
            var depsPath = Path.Combine(projectRoot, "Assets", "GoogleMobileAds", "Editor", "GoogleMobileAdsDependencies.xml");
            if (File.Exists(depsPath))
            {
                var xml = File.ReadAllText(depsPath);
                var am = Regex.Match(xml, @"play-services-ads:([\d.]+)", RegexOptions.IgnoreCase);
                var im = Regex.Match(xml, @"Google-Mobile-Ads-SDK[""'][^>]*version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (!im.Success)
                    im = Regex.Match(xml, @"version\s*=\s*[""']([^""']+)[^""']*[""'][^>]*Google-Mobile-Ads-SDK", RegexOptions.IgnoreCase);
                if (am.Success) android = am.Groups[1].Value.Trim();
                if (im.Success) ios = im.Groups[1].Value.Trim().Replace("~>", "").Replace(">=", "").Replace(">", "").Trim();
            }
            return (version, android, ios);
        }
        public static string NormalizeNetworkToken(string value)
        {
            var s = (value ?? "").Trim().ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
            if (s.EndsWith("sdk", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, Math.Max(0, s.Length - 3));
            if (s.EndsWith("adapter", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, Math.Max(0, s.Length - 7));
            if (s.EndsWith("network", StringComparison.OrdinalIgnoreCase) && s != "metaaudiencenetwork")
                s = s.Substring(0, Math.Max(0, s.Length - 7));
            if (s.EndsWith("mediate", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, Math.Max(0, s.Length - 7          ));
            if (s == "metaaudience" || s == "metaaudiencenetwork" || s == "facebook") return "meta";
            if (s == "google") return "admob";
            if (s == "unity") return "unityads";
            if (s == "fyber") return "dtexchange";
            if (s == "vungle") return "liftoffmonetize";
            if (s == "csj" || s == "bytedance" || s == "toutiao") return "pangle";
            if (s == "bigo") return "bigoads";
            return s;
        }
    }
}