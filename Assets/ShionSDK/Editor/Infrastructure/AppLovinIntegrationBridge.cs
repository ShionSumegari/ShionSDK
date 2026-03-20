#if USE_APPLOVIN
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using AppLovinMax.Scripts.IntegrationManager.Editor;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    [InitializeOnLoad]
    internal static class AppLovinIntegrationBridge
    {
        static AppLovinIntegrationBridge()
        {
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += TryResumePendingInstalls;
            };
        }
        private static void TryResumePendingInstalls()
        {
            var pending = EditorPrefs.GetString(PendingInstallKey, "");
            if (string.IsNullOrEmpty(pending)) return;
            var pluginData = GetPluginData();
            if (pluginData?.MediatedNetworks == null) return;
            var arr = pending.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var stillPending = new List<string>();
            foreach (var name in arr)
            {
                var installed = false;
                foreach (var net in pluginData.MediatedNetworks)
                {
                    if (net != null && net.Name == name && net.CurrentVersions != null && !string.IsNullOrEmpty(net.CurrentVersions.Unity))
                    {
                        installed = true;
                        break;
                    }
                }
                if (!installed) stillPending.Add(name);
            }
            if (stillPending.Count == 0)
            {
                EditorPrefs.DeleteKey(PendingInstallKey);
                return;
            }
            EditorPrefs.SetString(PendingInstallKey, string.Join(",", stillPending));
            var networks = new List<AppLovinMax.Scripts.IntegrationManager.Editor.Network>();
            foreach (var name in stillPending)
            {
                foreach (var net in pluginData.MediatedNetworks)
                {
                    if (net != null && net.Name == name)
                    {
                        networks.Add(net);
                        break;
                    }
                }
            }
            if (networks.Count > 0)
            {
                var w = FindAppLovinAdaptersWindow();
                if (w != null)
                    InstallAdapters(networks,
                        (cur, tot) => { w.SetInstallProgress(cur, tot); w.Repaint(); },
                        () => { w.OnInstallComplete(); },
                        () => { w.RefreshAndRepaint(); });
                else
                    InstallAdapters(networks, (cur, tot) => { }, () => { });
            }
        }
        public enum ActionType
        {
            Install,
            Upgrade,
            Installed
        }
        public class AdapterInfo
        {
            public string Name;
            public string DisplayName;
            public string AndroidVersion;
            public string IosVersion;
            public bool IsInstalled;
            public ActionType Action;
            public AppLovinMax.Scripts.IntegrationManager.Editor.Network Network;
        }
        public static bool IsAvailable()
        {
            return true;
        }
        public static List<AdapterInfo> LoadAdapters()
        {
            var list = new List<AdapterInfo>();
            try
            {
                var pluginData = GetPluginData();
                if (pluginData?.MediatedNetworks == null) return list;
                AssetDatabase.Refresh();
                var updateMethod = typeof(AppLovinPackageManager).GetMethod("UpdateCurrentVersions", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var network in pluginData.MediatedNetworks)
                {
                    if (network == null) continue;
                    updateMethod?.Invoke(null, new object[] { network });
                    list.Add(CreateAdapterInfo(network));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[ShionSDK] Failed to load adapters: " + e.Message);
            }
            return list;
        }
        private const string PendingInstallKey = "ShionSDK.AppLovin.PendingInstall";
        private static AppLovinAdaptersWindow FindAppLovinAdaptersWindow()
        {
            var arr = Resources.FindObjectsOfTypeAll<AppLovinAdaptersWindow>();
            return arr != null && arr.Length > 0 ? arr[0] : null;
        }
        public static void InstallAdapters(IList<AppLovinMax.Scripts.IntegrationManager.Editor.Network> networks, Action<int, int> onProgress, Action onAllComplete, Action onEachComplete = null)
        {
            if (networks == null || networks.Count == 0)
            {
                onAllComplete?.Invoke();
                return;
            }
            var names = new List<string>();
            foreach (var n in networks)
            {
                if (n != null && !string.IsNullOrEmpty(n.Name))
                    names.Add(n.Name);
            }
            if (names.Count == 0) { onAllComplete?.Invoke(); return; }
            var addNetworkMethod = typeof(AppLovinPackageManager).GetMethod("AddNetwork", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (addNetworkMethod == null)
            {
                onAllComplete?.Invoke();
                return;
            }
            var pendingStr = string.Join(",", names);
            UnityEditor.EditorPrefs.SetString(PendingInstallKey, pendingStr);
            void InstallNext(int index)
            {
                var currentNames = UnityEditor.EditorPrefs.GetString(PendingInstallKey, "");
                if (string.IsNullOrEmpty(currentNames))
                {
                    UnityEditor.EditorPrefs.DeleteKey(PendingInstallKey);
                    onAllComplete?.Invoke();
                    return;
                }
                var arr = currentNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (index >= arr.Length)
                {
                    UnityEditor.EditorPrefs.DeleteKey(PendingInstallKey);
                    onAllComplete?.Invoke();
                    return;
                }
                var pluginData = GetPluginData();
                if (pluginData?.MediatedNetworks == null)
                {
                    UnityEditor.EditorPrefs.DeleteKey(PendingInstallKey);
                    onAllComplete?.Invoke();
                    return;
                }
                AppLovinMax.Scripts.IntegrationManager.Editor.Network target = null;
                foreach (var net in pluginData.MediatedNetworks)
                {
                    if (net != null && net.Name == arr[index])
                    {
                        target = net;
                        break;
                    }
                }
                if (target == null)
                {
                    var remaining = new List<string>();
                    for (int i = index + 1; i < arr.Length; i++) remaining.Add(arr[i]);
                    UnityEditor.EditorPrefs.SetString(PendingInstallKey, string.Join(",", remaining));
                    EditorApplication.delayCall += () => InstallNext(0);
                    return;
                }
                onProgress?.Invoke(index + 1, arr.Length);
                IEnumerator AfterInstall()
                {
                    var en = addNetworkMethod.Invoke(null, new object[] { target, false }) as IEnumerator;
                    if (en != null) yield return en;
                    onEachComplete?.Invoke();
                    var nextIndex = index + 1;
                    var remaining = new List<string>();
                    for (int i = nextIndex; i < arr.Length; i++) remaining.Add(arr[i]);
                    if (remaining.Count == 0)
                        UnityEditor.EditorPrefs.DeleteKey(PendingInstallKey);
                    else
                        UnityEditor.EditorPrefs.SetString(PendingInstallKey, string.Join(",", remaining));
                    EditorApplication.delayCall += () =>
                    {
                        if (remaining.Count == 0)
                        {
                            EditorApplication.delayCall += () => onAllComplete?.Invoke();
                        }
                        else
                            InstallNext(0);
                    };
                }
                AppLovinEditorCoroutine.StartCoroutine(AfterInstall());
            }
            InstallNext(0);
        }
        public static void RemoveAdapters(IList<AppLovinMax.Scripts.IntegrationManager.Editor.Network> networks)
        {
            if (networks == null || networks.Count == 0) return;
            var removeMethod = typeof(AppLovinPackageManager).GetMethod("RemoveNetwork", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var updateMethod = typeof(AppLovinPackageManager).GetMethod("UpdateCurrentVersions", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (removeMethod == null) return;
            foreach (var network in networks)
            {
                if (network == null) continue;
                removeMethod.Invoke(null, new object[] { network });
                updateMethod?.Invoke(null, new object[] { network });
            }
        }
        private static PluginData GetPluginData()
        {
            var prop = typeof(AppLovinPackageManager).GetProperty("PluginData", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var cached = prop?.GetValue(null) as PluginData;
            if (cached != null) return cached;
            return AppLovinIntegrationManager.LoadPluginDataSync();
        }
        private static AdapterInfo CreateAdapterInfo(AppLovinMax.Scripts.IntegrationManager.Editor.Network network)
        {
            var name = network.Name ?? "";
            var displayName = network.DisplayName ?? name;
            var latest = network.LatestVersions;
            var androidVer = latest?.Android ?? "";
            var iosVer = latest?.Ios ?? "";
            var current = network.CurrentVersions;
            var isInstalled = current != null && !string.IsNullOrEmpty(current.Unity);
            if (isInstalled)
            {
                var currentAndroid = current?.Android ?? "";
                var currentIos = current?.Ios ?? "";
                if (TryReadInstalledSupportVersions(name, out var xmlAndroid, out var xmlIos))
                {
                    if (!string.IsNullOrEmpty(xmlAndroid)) currentAndroid = xmlAndroid;
                    if (!string.IsNullOrEmpty(xmlIos)) currentIos = xmlIos;
                }
                if (!string.IsNullOrEmpty(currentAndroid)) androidVer = currentAndroid;
                if (!string.IsNullOrEmpty(currentIos)) iosVer = currentIos;
            }
            var action = ActionType.Install;
            if (isInstalled)
            {
                var cmp = network.CurrentToLatestVersionComparisonResult;
                action = cmp == MaxSdkUtils.VersionComparisonResult.Lesser ? ActionType.Upgrade : ActionType.Installed;
            }
            return new AdapterInfo
            {
                Name = name,
                DisplayName = displayName,
                AndroidVersion = androidVer,
                IosVersion = iosVer,
                IsInstalled = isInstalled,
                Action = action,
                Network = network
            };
        }
        private static bool TryReadInstalledSupportVersions(string networkName, out string androidVersion, out string iosVersion)
        {
            androidVersion = "";
            iosVersion = "";
            if (string.IsNullOrEmpty(networkName)) return false;
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return false;
            var xmlPath = Path.Combine(projectRoot, "Assets", "MaxSdk", "Mediation", networkName, "Editor", "Dependencies.xml");
            if (!File.Exists(xmlPath)) return false;
            var content = File.ReadAllText(xmlPath);
            var androidMatch = Regex.Match(content,
                @"com\.applovin\.mediation:[a-z0-9\-]+-adapter:\[?([\d.]+)\]?",
                RegexOptions.IgnoreCase);
            var iosMatch = Regex.Match(content,
                @"name\s*=\s*[""'](AppLovinMediation[A-Za-z0-9]+Adapter)[""'][^>]*version\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);
            if (!iosMatch.Success)
            {
                iosMatch = Regex.Match(content,
                    @"version\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""'](AppLovinMediation[A-Za-z0-9]+Adapter)[""']",
                    RegexOptions.IgnoreCase);
                if (iosMatch.Success) iosVersion = iosMatch.Groups[1].Value.Trim();
            }
            else
            {
                iosVersion = iosMatch.Groups[2].Value.Trim();
            }
            if (androidMatch.Success)
                androidVersion = androidMatch.Groups[1].Value.Trim();
            return !string.IsNullOrEmpty(androidVersion) || !string.IsNullOrEmpty(iosVersion);
        }
    }
}
#endif