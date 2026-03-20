using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
namespace Shion.SDK.Editor
{
    public enum AdMobInstallDialogResult
    {
        Cancel,
        AcceptAndUpdateDependencies,
        InstallWithoutUpdate
    }
    public class AdMobAdapterVersions
    {
        public string AndroidVersion;
        public string IosVersion;
        public string DependenciesXmlPath;
    }
    public class AdMobPluginVersions
    {
        public string UnityVersion;
        public string AndroidVersion;
        public string IosVersion;
    }
    public static class AdMobAppLovinCompatibilityService
    {
        private const string PlayServicesAdsPrefix = "com.google.android.gms:play-services-ads";
        private const string IosPodName = "Google-Mobile-Ads-SDK";
        private const string AndroidAdapterSpecPrefix = "com.applovin.mediation:google-adapter:";
        private const string IosAdapterPodName = "AppLovinMediationGoogleAdapter";
        private const string ChangeLogUrl = "https://raw.githubusercontent.com/googleads/googleads-mobile-unity/main/ChangeLog.txt";
        private static readonly string[] AdapterDependenciesPaths = new[]
        {
            "Assets/MaxSdk/Mediation/Google/Editor",
            "Assets/MaxSdk/Mediation/AdMob/Editor"
        };
        private static readonly string[] AdMobPluginDependenciesPaths = new[]
        {
            "Assets/GoogleMobileAds/Editor/GoogleMobileAdsDependencies.xml"
        };
        private static List<AdMobVersionMapping> _mappings;
        private static double _lastFetchTime;
        private const double CacheSeconds = 3600;
        public static void ClearCache()
        {
            _mappings = null;
            _lastFetchTime = 0;
            AdMobChangelogVersionFetcher.ClearCache();
        }
        private const string AdMobSupportEditorPrefsPrefix = "ShionSDK.AdSDK.AdMobSupport.";
        public static (string Android, string Ios)? TryGetAdMobSupportVersions(string packageId, string mediationVersion)
        {
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(mediationVersion) || !mediationVersion.Contains("."))
                return null;
            var slug = GetIntegrationSlugForPackage(packageId);
            if (string.IsNullOrEmpty(slug)) return null;
            var support = AdMobChangelogVersionFetcher.GetCachedSupportedVersions(slug, mediationVersion);
            if (support.HasValue) return support;
            var prefKey = AdMobSupportEditorPrefsPrefix + packageId + "|" + mediationVersion;
            var val = EditorPrefs.GetString(prefKey, null);
            if (string.IsNullOrEmpty(val)) return null;
            var parts = val.Split(new[] { '\x1f' }, 2, StringSplitOptions.None);
            if (parts.Length < 2) return null;
            return (parts[0], parts[1]);
        }
        private static string GetIntegrationSlugForPackage(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return "";
            var def = AdMobAdapterConfig.AllAdapters.FirstOrDefault(a => string.Equals(a?.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(def?.IntegrationSlug)) return def.IntegrationSlug;
            var prefix = "com.google.ads.mobile.mediation.";
            return packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? packageId.Substring(prefix.Length) : packageId;
        }
        private static string NormalizeVersion(string v) => VersionComparisonService.Normalize(v ?? "");
        private class AdMobVersionMapping { public string Unity; public string Android; public string Ios; }
        private static string FetchChangeLogFromGit()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(ChangeLogUrl);
                request.Timeout = 10000;
                request.UserAgent = "Unity-ShionSDK";
                request.Method = "GET";
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ShionSDKConstants.LogPrefix} Failed to fetch AdMob ChangeLog from GitHub: {ex.Message}");
                return null;
            }
        }
        private static List<AdMobVersionMapping> ParseChangeLog(string changelog)
        {
            var result = new List<AdMobVersionMapping>();
            if (string.IsNullOrEmpty(changelog)) return result;
            var versionRegex = new Regex(@"^\s*Version\s+(\d+(?:\.\d+)*)\s*$", RegexOptions.Multiline);
            var androidRegex = new Regex(@"(?:Updated the GMA Android SDK dependency version to|Google Mobile Ads Android SDK)\s+(\d+(?:\.\d+)*)", RegexOptions.IgnoreCase);
            var iosRegex = new Regex(@"(?:Updated the GMA iOS SDK dependency version to|Google Mobile Ads iOS SDK)\s+(\d+(?:\.\d+)*)", RegexOptions.IgnoreCase);
            var versionMatches = versionRegex.Matches(changelog);
            for (int i = 0; i < versionMatches.Count; i++)
            {
                var unityVer = versionMatches[i].Groups[1].Value.Trim();
                var start = versionMatches[i].Index + versionMatches[i].Length;
                var end = i + 1 < versionMatches.Count ? versionMatches[i + 1].Index : changelog.Length;
                var block = changelog.Substring(start, end - start);
                var androidM = androidRegex.Match(block);
                var iosM = iosRegex.Match(block);
                var android = androidM.Success ? androidM.Groups[1].Value.Trim() : null;
                var ios = iosM.Success ? iosM.Groups[1].Value.Trim() : null;
                if (!string.IsNullOrEmpty(android) || !string.IsNullOrEmpty(ios))
                {
                    result.Add(new AdMobVersionMapping
                    {
                        Unity = NormalizeVersion(unityVer),
                        Android = android ?? "",
                        Ios = ios ?? ""
                    });
                }
            }
            return result;
        }
        private static void EnsureMappingsLoaded()
        {
            var now = EditorApplication.timeSinceStartup;
            if (_mappings != null && _mappings.Count > 0 && (now - _lastFetchTime) < CacheSeconds)
                return;
            _mappings = new List<AdMobVersionMapping>();
            try
            {
                var changelog = FetchChangeLogFromGit();
                if (!string.IsNullOrEmpty(changelog))
                {
                    _mappings = ParseChangeLog(changelog);
                    _lastFetchTime = now;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ShionSDKConstants.LogPrefix} Failed to parse AdMob ChangeLog: {ex.Message}");
            }
        }
        public static AdMobPluginVersions GetAdMobPluginVersions(string unityVersion)
        {
            EnsureMappingsLoaded();
            var normalized = NormalizeVersion(unityVersion ?? "");
            if (string.IsNullOrEmpty(normalized) || _mappings == null) return null;
            var m = _mappings.FirstOrDefault(x => string.Equals(x.Unity, normalized, StringComparison.OrdinalIgnoreCase));
            if (m == null)
            {
                var prefix = normalized.Split('.')[0];
                m = _mappings.Where(x => x.Unity.StartsWith(prefix + ".")).OrderByDescending(x => x.Unity).FirstOrDefault();
            }
            if (m == null) return null;
            return new AdMobPluginVersions
            {
                UnityVersion = m.Unity,
                AndroidVersion = m.Android,
                IosVersion = m.Ios
            };
        }
        private const string AppLovinRepoBase = "https://raw.githubusercontent.com/AppLovin/AppLovin-MAX-Unity-Plugin";
        private static readonly Dictionary<string, (string Android, string Ios)> AppLovinSupportCache = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        public static (string Android, string Ios) GetAppLovinSdkSupportVersions(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return (null, null);
            var key = tag.Trim();
            if (AppLovinSupportCache.TryGetValue(key, out var cached))
                return cached;
            var path = "DemoApp/Assets/MaxSdk/AppLovin/Editor/Dependencies.xml";
            var url = $"{AppLovinRepoBase}/{key}/{path}";
            try
            {
                var request = UnityWebRequest.Get(url);
                request.timeout = 8;
                request.SetRequestHeader("User-Agent", "Unity-ShionSDK");
                var op = request.SendWebRequest();
                while (!op.isDone)
                    EditorApplication.QueuePlayerLoopUpdate();
                if (request.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    var content = request.downloadHandler.text;
                    request.Dispose();
                    var androidM = Regex.Match(content, @"com\.applovin:applovin-sdk:([\d.]+)", RegexOptions.IgnoreCase);
                    var iosM = Regex.Match(content, @"name\s*=\s*[""']AppLovinSDK[""'][^>]*version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    if (!iosM.Success)
                        iosM = Regex.Match(content, @"version\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""']AppLovinSDK[""']", RegexOptions.IgnoreCase);
                    var android = androidM.Success ? androidM.Groups[1].Value.Trim() : null;
                    var ios = iosM.Success ? iosM.Groups[1].Value.Trim() : null;
                    if (!string.IsNullOrEmpty(android) || !string.IsNullOrEmpty(ios))
                    {
                        var result = (android ?? "", ios ?? "");
                        AppLovinSupportCache[key] = result;
                        return result;
                    }
                }
                else
                {
                    request.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ShionSDKConstants.LogPrefix} AppLovin support fetch failed for {key}: {ex.Message}");
            }
            var parsed = ParseAppLovinTagToVersion(tag);
            if (!string.IsNullOrEmpty(parsed))
            {
                var fallback = (parsed, parsed);
                AppLovinSupportCache[key] = fallback;
                return fallback;
            }
            return (null, null);
        }
        public static void ClearAppLovinSupportCache() => AppLovinSupportCache.Clear();
        private static string ParseAppLovinTagToVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var s = tag.Trim();
            if (s.StartsWith("release_", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(8).Replace("_", ".");
            else if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase) && s.Length > 1 && (char.IsDigit(s[1]) || s[1] == '.'))
                s = s.Substring(1);
            if (s.Length > 0 && char.IsDigit(s[0]))
            {
                var match = Regex.Match(s, @"^(\d+(?:\.\d+)*)");
                return match.Success ? match.Groups[1].Value : s;
            }
            return null;
        }
        public static bool IsAppLovinGoogleAdapterPresent()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return false;
            foreach (var relPath in AdapterDependenciesPaths)
            {
                var fullPath = Path.Combine(projectRoot, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (Directory.Exists(fullPath)) return true;
            }
            return false;
        }
        public static string GetAdapterDependenciesPathForUpdate()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return null;
            foreach (var relPath in AdapterDependenciesPaths)
            {
                var fullPath = Path.Combine(projectRoot, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (!Directory.Exists(fullPath)) continue;
                var depsPath = Path.Combine(fullPath, "Dependencies.xml");
                if (File.Exists(depsPath)) return depsPath;
            }
            return null;
        }
        public static AdMobAdapterVersions FindAndParseAdapterDependencies()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return null;
            foreach (var relPath in AdapterDependenciesPaths)
            {
                var fullPath = Path.Combine(projectRoot, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (!Directory.Exists(fullPath)) continue;
                var files = Directory.GetFiles(fullPath, "*.xml", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    if (!file.EndsWith("Dependencies.xml", StringComparison.OrdinalIgnoreCase)) continue;
                    var content = File.ReadAllText(file);
                    var result = ParseDependenciesVersions(content);
                    if (result != null)
                    {
                        result.DependenciesXmlPath = file;
                        return result;
                    }
                }
            }
            foreach (var relPath in AdMobPluginDependenciesPaths)
            {
                var fullPath = Path.Combine(projectRoot, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (!File.Exists(fullPath)) continue;
                var content = File.ReadAllText(fullPath);
                var result = ParseDependenciesVersions(content);
                if (result != null)
                {
                    result.DependenciesXmlPath = fullPath;
                    return result;
                }
            }
            var packagesPath = Path.Combine(projectRoot, "Packages");
            if (Directory.Exists(packagesPath))
            {
                foreach (var dir in Directory.GetDirectories(packagesPath))
                {
                    var name = Path.GetFileName(dir);
                    if (name == null || (!name.Contains("google", StringComparison.OrdinalIgnoreCase) && !name.Contains("admob", StringComparison.OrdinalIgnoreCase))) continue;
                    var editorPath = Path.Combine(dir, "Editor");
                    if (!Directory.Exists(editorPath)) continue;
                    var xmlFiles = Directory.GetFiles(editorPath, "*Dependencies.xml", SearchOption.TopDirectoryOnly);
                    foreach (var f in xmlFiles)
                    {
                        var content = File.ReadAllText(f);
                        var result = ParseDependenciesVersions(content);
                        if (result != null)
                        {
                            result.DependenciesXmlPath = f;
                            return result;
                        }
                    }
                }
            }
            return null;
        }
        private static AdMobAdapterVersions ParseDependenciesVersions(string xmlContent)
        {
            if (string.IsNullOrEmpty(xmlContent)) return null;
            var androidVer = ParseAndroidVersion(xmlContent);
            var iosVer = ParseIosVersion(xmlContent);
            if (string.IsNullOrEmpty(androidVer) && string.IsNullOrEmpty(iosVer)) return null;
            return new AdMobAdapterVersions { AndroidVersion = androidVer ?? "", IosVersion = iosVer ?? "" };
        }
        private static string ParseAndroidVersion(string xml)
        {
            var m = Regex.Match(xml, Regex.Escape(PlayServicesAdsPrefix) + @":([\d.]+)", RegexOptions.IgnoreCase);
            return m.Success && m.Groups.Count >= 2 ? m.Groups[1].Value.Trim() : null;
        }
        private static string ParseIosVersion(string xml)
        {
            var m = Regex.Match(xml, @"<iosPod[^>]*name\s*=\s*[""']" + Regex.Escape(IosPodName) + @"[""'][^>]*version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (!m.Success)
                m = Regex.Match(xml, @"version\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""']" + Regex.Escape(IosPodName) + @"[""']", RegexOptions.IgnoreCase);
            return m.Success && m.Groups.Count >= 2 ? m.Groups[1].Value.Trim() : null;
        }
        private static string ParseAdapterAndroidVersion(string xml)
        {
            var m = Regex.Match(xml, Regex.Escape(AndroidAdapterSpecPrefix) + @"\[(?<v>[\d.]+)\]", RegexOptions.IgnoreCase);
            return m.Success ? (m.Groups["v"].Value ?? "").Trim() : null;
        }
        private static string ParseAdapterIosVersion(string xml)
        {
            var m = Regex.Match(xml, @"name\s*=\s*[""']" + Regex.Escape(IosAdapterPodName) + @"[""'][^>]*version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (!m.Success)
                m = Regex.Match(xml, @"version\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""']" + Regex.Escape(IosAdapterPodName) + @"[""']", RegexOptions.IgnoreCase);
            return m.Success && m.Groups.Count >= 2 ? m.Groups[1].Value.Trim() : null;
        }
        public static bool UpdateAdapterDependenciesXml(string adapterFilePath, string androidVersion, string iosVersion)
        {
            if (string.IsNullOrEmpty(adapterFilePath) || !File.Exists(adapterFilePath)) return false;
            var content = File.ReadAllText(adapterFilePath);
            var modified = false;
            var currentAndroid = ParseAdapterAndroidVersion(content);
            var currentIos = ParseAdapterIosVersion(content);
            if (!string.IsNullOrEmpty(androidVersion))
            {
                var targetAndroid = ToAdapterVersionFormat(androidVersion, 4);
                if (!string.IsNullOrEmpty(currentAndroid) && VersionComparisonService.Compare(targetAndroid, currentAndroid) > 0)
                    return false;
            }
            if (!string.IsNullOrEmpty(iosVersion))
            {
                var targetIos = ToAdapterVersionFormat(iosVersion, 4);
                if (!string.IsNullOrEmpty(currentIos) && VersionComparisonService.Compare(targetIos, currentIos) > 0)
                    return false;
            }
            if (!string.IsNullOrEmpty(androidVersion))
            {
                var adapterAndroidVer = ToAdapterVersionFormat(androidVersion, 4);
                var pattern = Regex.Escape(AndroidAdapterSpecPrefix) + @"\[[\d.]+\]";
                var replacement = AndroidAdapterSpecPrefix + "[" + adapterAndroidVer + "]";
                var newContent = Regex.Replace(content, pattern, replacement, RegexOptions.IgnoreCase);
                if (newContent != content) { content = newContent; modified = true; }
            }
            if (!string.IsNullOrEmpty(iosVersion))
            {
                var adapterIosVer = ToAdapterVersionFormat(iosVersion, 4);
                var pattern = @"(name\s*=\s*[""']" + Regex.Escape(IosAdapterPodName) + @"[""'][^>]*version\s*=\s*[""'])[^""']*([""'])";
                var newContent = Regex.Replace(content, pattern, "${1}" + adapterIosVer + "${2}", RegexOptions.IgnoreCase);
                if (newContent == content)
                {
                    pattern = @"(version\s*=\s*[""'])[^""']*([""'][^>]*name\s*=\s*[""']" + Regex.Escape(IosAdapterPodName) + @"[""'])";
                    newContent = Regex.Replace(content, pattern, "${1}" + adapterIosVer + "${2}", RegexOptions.IgnoreCase);
                }
                if (newContent != content) { content = newContent; modified = true; }
            }
            if (modified)
                File.WriteAllText(adapterFilePath, content);
            return modified;
        }
        private static string ToAdapterVersionFormat(string version, int targetSegments)
        {
            if (string.IsNullOrWhiteSpace(version)) return version ?? "";
            var parts = version.Trim().Split('.');
            var list = new List<string>(parts);
            while (list.Count < targetSegments)
                list.Add("0");
            if (list.Count > targetSegments)
                list = list.Take(targetSegments).ToList();
            return string.Join(".", list);
        }
        private static bool VersionsDiffer(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return false;
            return !VersionComparisonService.IsEqual(a ?? "", b ?? "");
        }
        public static bool HasVersionMismatch(AdMobPluginVersions plugin, AdMobAdapterVersions adapter)
        {
            if (plugin == null || adapter == null) return false;
            return VersionsDiffer(plugin.AndroidVersion, adapter.AndroidVersion) || VersionsDiffer(plugin.IosVersion, adapter.IosVersion);
        }
    }
    public class MediationVersionSource
    {
        public string FilePath;
        public string AndroidVersion;
        public string IosVersion;
        public bool IsAdapter;
        public string NetworkId;
    }
    public class MediationNetworkConfig
    {
        public string NetworkId;
        public string DisplayName;
        public string[] PluginPaths;
        public string AndroidArtifactPattern;
        public string IosPodName;
        public bool PluginUsesUnderlyingSdk;
        public string AppLovinAdapterPath;
        public string AppLovinAndroidAdapterPrefix;
        public string AppLovinIosAdapterName;
        public int AdapterVersionSegments = 4;
        public bool AlwaysUpdateAdapterOnly;
    }
    public class DiscoveredAdapterInfo
    {
        public string NetworkId;
        public string DisplayName;
        public string FilePath;
        public string AndroidVersion;
        public string IosVersion;
        public string AndroidAdapterPrefix;
        public string IosAdapterName;
    }
    public class CounterpartSearchConfig
    {
        public string[] SearchPaths;
        public string AndroidArtifactPattern;
        public string IosPodName;
        public bool UsesUnderlyingSdk;
    }
    public static class MediationCompatibilityService
    {
        private static readonly Dictionary<string, CounterpartSearchConfig> CounterpartConfigs = new Dictionary<string, CounterpartSearchConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["google"] = new CounterpartSearchConfig
            {
                SearchPaths = new[] { "Assets/GoogleMobileAds/Editor/GoogleMobileAdsDependencies.xml" },
                AndroidArtifactPattern = "com.google.android.gms:play-services-ads",
                IosPodName = "Google-Mobile-Ads-SDK",
                UsesUnderlyingSdk = true
            },
            ["admob"] = new CounterpartSearchConfig
            {
                SearchPaths = new[] { "Assets/MaxSdk/Mediation/AdMob/Editor" },
                AndroidArtifactPattern = "com.applovin.mediation:google-adapter:",
                IosPodName = "AppLovinMediationGoogleAdapter",
                UsesUnderlyingSdk = false
            },
            ["facebook"] = new CounterpartSearchConfig
            {
                SearchPaths = new[] { "Assets/MaxSdk/Mediation/Facebook/Editor", "Packages/com.google.ads.mobile.mediation.metaaudiencenetwork" },
                AndroidArtifactPattern = "com.facebook.android:audience-network-sdk",
                IosPodName = "FBAudienceNetwork",
                UsesUnderlyingSdk = true
            },
            ["inmobi"] = new CounterpartSearchConfig
            {
                SearchPaths = new[] { "Packages/com.google.ads.mobile.mediation.inmobi", "Packages/com.applovin.mediation.inmobi" },
                AndroidArtifactPattern = "com.applovin.mediation:inmobi-adapter:",
                IosPodName = "AppLovinMediationInMobiAdapter",
                UsesUnderlyingSdk = false
            },
            ["unityads"] = new CounterpartSearchConfig
            {
                SearchPaths = new[] { "Packages/com.google.ads.mobile.mediation.unityads", "Packages/com.unity.ads" },
                AndroidArtifactPattern = "com.applovin.mediation:unity-adapter:",
                IosPodName = "AppLovinMediationUnityAdsAdapter",
                UsesUnderlyingSdk = false
            },
            ["unity"] = new CounterpartSearchConfig
            {
                SearchPaths = new[] { "Packages/com.google.ads.mobile.mediation.unity", "Packages/com.unity.ads" },
                AndroidArtifactPattern = "com.applovin.mediation:unity-adapter:",
                IosPodName = "AppLovinMediationUnityAdsAdapter",
                UsesUnderlyingSdk = false
            },
            ["chartboost"] = new CounterpartSearchConfig
            {
                SearchPaths = new[] { "Assets/GoogleMobileAds/Mediation/Chartboost/Editor", "Packages/com.google.ads.mobile.mediation.chartboost" },
                AndroidArtifactPattern = "com.google.ads.mediation:chartboost",
                IosPodName = "GoogleMobileAdsMediationChartboost",
                UsesUnderlyingSdk = false
            }
        };
        public static void AddCounterpartConfig(string networkId, CounterpartSearchConfig config)
        {
            if (!string.IsNullOrEmpty(networkId) && config != null)
                CounterpartConfigs[networkId] = config;
        }
        private static (string AndroidPattern, string IosPattern) GetAdMobParsePatterns(AdMobAdapterConfig.AdapterDef def)
        {
            if (def == null) return (null, null);
            if (!string.IsNullOrEmpty(def.AndroidParsePattern) || !string.IsNullOrEmpty(def.IosParsePattern))
                return (def.AndroidParsePattern ?? "", def.IosParsePattern ?? "");
            var so = MediationCompatibilityConfig.Instance;
            if (so?.AdMobParsePatterns != null)
            {
                foreach (var p in so.AdMobParsePatterns)
                {
                    if (string.IsNullOrEmpty(p.Key)) continue;
                    var key = p.Key.Trim();
                    if (string.Equals(key, def.IntegrationSlug, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, def.MediationFolderName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, (def.PackageId ?? "").Replace("com.google.ads.mobile.mediation.", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))
                        return (p.AndroidPattern ?? "", p.IosPodName ?? "");
                }
            }
            var pkgSuffix = (def.PackageId ?? "").Replace("com.google.ads.mobile.mediation.", "", StringComparison.OrdinalIgnoreCase);
            var androidPattern = string.IsNullOrEmpty(pkgSuffix) ? null : "com.google.ads.mediation:" + pkgSuffix;
            var iosPattern = "GoogleMobileAdsMediation" + (def.MediationFolderName ?? "");
            return (androidPattern, iosPattern);
        }
        private static bool AdMobDefMatchesAppLovin(AdMobAdapterConfig.AdapterDef admobDef, string appLovinNetworkId)
        {
            var al = (appLovinNetworkId ?? "").Trim();
            if (string.IsNullOrEmpty(al)) return false;
            if (!string.IsNullOrEmpty(admobDef.AppLovinNetworkId))
                return string.Equals(admobDef.AppLovinNetworkId, al, StringComparison.OrdinalIgnoreCase);
            var so = MediationCompatibilityConfig.Instance;
            if (so?.NetworkMappings != null)
            {
                foreach (var m in so.NetworkMappings)
                {
                    if (string.IsNullOrEmpty(m.AppLovinNetworkId)) continue;
                    if (!string.Equals(m.AppLovinNetworkId, al, StringComparison.OrdinalIgnoreCase)) continue;
                    var key = (m.AdMobKey ?? "").Trim();
                    if (string.Equals(key, admobDef.IntegrationSlug, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, admobDef.MediationFolderName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, (admobDef.PackageId ?? "").Replace("com.google.ads.mobile.mediation.", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return string.Equals(admobDef.IntegrationSlug, al, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(admobDef.MediationFolderName, al, StringComparison.OrdinalIgnoreCase);
        }
        public static List<(AdMobAdapterConfig.AdapterDef Def, string FilePath, string AndroidVersion, string IosVersion)> DiscoverInstalledAdMobAdapters()
        {
            var result = new List<(AdMobAdapterConfig.AdapterDef, string, string, string)>();
            if (AdMobAdapterConfig.AllAdapters == null) return result;
            foreach (var def in AdMobAdapterConfig.AllAdapters)
            {
                var path = FindAdMobAdapterDependenciesPath(def);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                var (androidPat, iosPat) = GetAdMobParsePatterns(def);
                if (string.IsNullOrEmpty(androidPat) && string.IsNullOrEmpty(iosPat)) continue;
                var content = File.ReadAllText(path);
                var av = MediationParseAndroid(content, androidPat ?? "");
                var iv = MediationParseIos(content, iosPat ?? "");
                if (string.IsNullOrEmpty(av) && string.IsNullOrEmpty(iv)) continue;
                result.Add((def, path, av ?? "", iv ?? ""));
            }
            return result;
        }
        private static string MediationNormalizeVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return "";
            return v.Trim().TrimStart('v', 'V').Trim('[', ']');
        }
        private static int MediationCompareVersions(string a, string b)
        {
            var na = MediationNormalizeVersion(a ?? "");
            var nb = MediationNormalizeVersion(b ?? "");
            if (string.IsNullOrEmpty(na) && string.IsNullOrEmpty(nb)) return 0;
            if (string.IsNullOrEmpty(na)) return 1;
            if (string.IsNullOrEmpty(nb)) return -1;
            var pa = na.Split('.').Select(x => int.TryParse(x, out var i) ? i : 0).ToArray();
            var pb = nb.Split('.').Select(x => int.TryParse(x, out var i) ? i : 0).ToArray();
            for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                var va = i < pa.Length ? pa[i] : 0;
                var vb = i < pb.Length ? pb[i] : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }
        private static string MediationParseAndroid(string xml, string pattern)
        {
            if (string.IsNullOrEmpty(xml) || string.IsNullOrEmpty(pattern)) return null;
            var m = Regex.Match(xml, Regex.Escape(pattern) + @"[:\[]?([\d.]+)\]?", RegexOptions.IgnoreCase);
            return m.Success && m.Groups.Count >= 2 ? m.Groups[1].Value.Trim() : null;
        }
        private static string MediationParseIos(string xml, string podName)
        {
            if (string.IsNullOrEmpty(xml) || string.IsNullOrEmpty(podName)) return null;
            var m = Regex.Match(xml, @"name\s*=\s*[""']" + Regex.Escape(podName) + @"[""'][^>]*version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (!m.Success)
                m = Regex.Match(xml, @"version\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""']" + Regex.Escape(podName) + @"[""']", RegexOptions.IgnoreCase);
            return m.Success && m.Groups.Count >= 2 ? m.Groups[1].Value.Trim() : null;
        }
        private static string MediationToAdapterFormat(string version, int segs)
        {
            if (string.IsNullOrWhiteSpace(version)) return version ?? "";
            var parts = version.Trim().Trim('[', ']').Split('.');
            var list = new List<string>(parts);
            while (list.Count < segs) list.Add("0");
            if (list.Count > segs) list = list.Take(segs).ToList();
            return string.Join(".", list);
        }
        public static List<DiscoveredAdapterInfo> DiscoverAllAppLovinAdapters()
        {
            var result = new List<DiscoveredAdapterInfo>();
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return result;
            var mediationDir = Path.Combine(projectRoot, "Assets", "MaxSdk", "Mediation");
            if (!Directory.Exists(mediationDir)) return result;
            var applovinAndroidRegex = new Regex(@"com\.applovin\.mediation:([a-z0-9\-]+)-adapter:\[?([\d.]+)\]?", RegexOptions.IgnoreCase);
            var applovinIosRegex1 = new Regex(@"name\s*=\s*[""'](AppLovinMediation[A-Za-z0-9]+Adapter)[""'][^>]*version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            var applovinIosRegex2 = new Regex(@"version\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""'](AppLovinMediation[A-Za-z0-9]+Adapter)[""']", RegexOptions.IgnoreCase);
            foreach (var networkDir in Directory.GetDirectories(mediationDir))
            {
                var networkName = Path.GetFileName(networkDir);
                if (string.IsNullOrEmpty(networkName)) continue;
                var editorDir = Path.Combine(networkDir, "Editor");
                if (!Directory.Exists(editorDir)) continue;
                var xmlFiles = Directory.GetFiles(editorDir, "*.xml", SearchOption.TopDirectoryOnly);
                if (xmlFiles.Length != 1) continue;
                var depsPath = xmlFiles[0];
                var content = File.ReadAllText(depsPath);
                var androidM = applovinAndroidRegex.Match(content);
                var iosM1 = applovinIosRegex1.Match(content);
                var iosM2 = applovinIosRegex2.Match(content);
                if (!androidM.Success && !iosM1.Success && !iosM2.Success) continue;
                var androidPrefix = androidM.Success ? "com.applovin.mediation:" + androidM.Groups[1].Value + "-adapter:" : "";
                var androidVer = androidM.Success ? androidM.Groups[2].Value.Trim() : "";
                string iosName = "";
                string iosVer = "";
                if (iosM1.Success)
                {
                    iosName = iosM1.Groups[1].Value;
                    iosVer = iosM1.Groups[2].Value.Trim();
                }
                else if (iosM2.Success)
                {
                    iosVer = iosM2.Groups[1].Value.Trim();
                    iosName = iosM2.Groups[2].Value;
                }
                if (string.IsNullOrEmpty(androidVer) && string.IsNullOrEmpty(iosVer)) continue;
                var displayName = ToTitleCase(networkName);
                result.Add(new DiscoveredAdapterInfo
                {
                    NetworkId = networkName,
                    DisplayName = displayName,
                    FilePath = depsPath,
                    AndroidVersion = androidVer,
                    IosVersion = iosVer,
                    AndroidAdapterPrefix = androidPrefix,
                    IosAdapterName = iosName
                });
            }
            return result;
        }
        private static string ToTitleCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Equals("inmobi", StringComparison.OrdinalIgnoreCase)) return "InMobi";
            if (s.Equals("unityads", StringComparison.OrdinalIgnoreCase)) return "Unity Ads";
            return char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1).ToLowerInvariant() : "");
        }
        private static MediationVersionSource FindCounterpart(DiscoveredAdapterInfo adapter)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return null;
            string androidPattern = null;
            string iosPattern = null;
            var paths = new List<string>();
            var isAdapter = true;
            if (CounterpartConfigs.TryGetValue(adapter.NetworkId, out var cfg))
            {
                androidPattern = cfg.AndroidArtifactPattern;
                iosPattern = cfg.IosPodName;
                paths.AddRange(cfg.SearchPaths);
                isAdapter = !cfg.UsesUnderlyingSdk;
            }
            else
            {
                androidPattern = adapter.AndroidAdapterPrefix.TrimEnd(':');
                iosPattern = adapter.IosAdapterName;
                paths.Add("Packages");
                paths.Add($"Assets/GoogleMobileAds/Mediation/{adapter.NetworkId}/Editor");
            }
            foreach (var relPath in paths)
            {
                var fullPath = Path.Combine(projectRoot, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(fullPath) && fullPath != adapter.FilePath)
                {
                    var content = File.ReadAllText(fullPath);
                    var av = MediationParseAndroid(content, androidPattern);
                    var iv = MediationParseIos(content, iosPattern);
                    if (!string.IsNullOrEmpty(av) || !string.IsNullOrEmpty(iv))
                        return new MediationVersionSource { FilePath = fullPath, AndroidVersion = av ?? "", IosVersion = iv ?? "", IsAdapter = isAdapter, NetworkId = adapter.NetworkId };
                }
                if (Directory.Exists(fullPath))
                {
                    var xmlFiles = Directory.GetFiles(fullPath, "*.xml", SearchOption.TopDirectoryOnly);
                    if (xmlFiles.Length == 1 && xmlFiles[0] != adapter.FilePath)
                    {
                        var content = File.ReadAllText(xmlFiles[0]);
                        var av = MediationParseAndroid(content, androidPattern);
                        var iv = MediationParseIos(content, iosPattern);
                        if (!string.IsNullOrEmpty(av) || !string.IsNullOrEmpty(iv))
                            return new MediationVersionSource { FilePath = xmlFiles[0], AndroidVersion = av ?? "", IosVersion = iv ?? "", IsAdapter = isAdapter, NetworkId = adapter.NetworkId };
                    }
                }
            }
            var pkgDir = Path.Combine(projectRoot, "Packages");
            if (!Directory.Exists(pkgDir)) return null;
            var networkLower = adapter.NetworkId.ToLowerInvariant();
            foreach (var dir in Directory.GetDirectories(pkgDir))
            {
                var n = Path.GetFileName(dir)?.ToLowerInvariant();
                if (n == null || !n.Contains(networkLower) && !n.Contains("mediation")) continue;
                var ed = Path.Combine(dir, "Editor");
                if (!Directory.Exists(ed)) continue;
                var xmlFiles = Directory.GetFiles(ed, "*.xml", SearchOption.TopDirectoryOnly);
                if (xmlFiles.Length == 1 && xmlFiles[0] != adapter.FilePath)
                {
                    var content = File.ReadAllText(xmlFiles[0]);
                    var av = MediationParseAndroid(content, androidPattern ?? adapter.AndroidAdapterPrefix.TrimEnd(':'));
                    var iv = MediationParseIos(content, iosPattern ?? adapter.IosAdapterName);
                    if (!string.IsNullOrEmpty(av) || !string.IsNullOrEmpty(iv))
                        return new MediationVersionSource { FilePath = xmlFiles[0], AndroidVersion = av ?? "", IosVersion = iv ?? "", IsAdapter = isAdapter, NetworkId = adapter.NetworkId };
                }
            }
            return null;
        }
        public static MediationVersionSource FindPluginVersions(MediationNetworkConfig config)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return null;
            foreach (var relPath in config.PluginPaths)
            {
                var fullPath = Path.Combine(projectRoot, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                string content = null;
                string resolved = null;
                if (File.Exists(fullPath)) { content = File.ReadAllText(fullPath); resolved = fullPath; }
                else if (Directory.Exists(fullPath))
                {
                    var xmlFiles = Directory.GetFiles(fullPath, "*.xml", SearchOption.TopDirectoryOnly);
                    if (xmlFiles.Length == 1) { content = File.ReadAllText(xmlFiles[0]); resolved = xmlFiles[0]; }
                }
                if (string.IsNullOrEmpty(content)) continue;
                var av = MediationParseAndroid(content, config.AndroidArtifactPattern);
                var iv = MediationParseIos(content, config.IosPodName);
                if (string.IsNullOrEmpty(av) && string.IsNullOrEmpty(iv)) continue;
                return new MediationVersionSource { FilePath = resolved, AndroidVersion = av ?? "", IosVersion = iv ?? "", IsAdapter = !config.PluginUsesUnderlyingSdk, NetworkId = config.NetworkId };
            }
            var pkg = Path.Combine(projectRoot, "Packages");
            if (!Directory.Exists(pkg)) return null;
            foreach (var dir in Directory.GetDirectories(pkg))
            {
                var n = Path.GetFileName(dir);
                if (n == null || (!n.Contains("mediation", StringComparison.OrdinalIgnoreCase) && !n.Contains("google", StringComparison.OrdinalIgnoreCase) && !n.Contains("meta", StringComparison.OrdinalIgnoreCase))) continue;
                var ed = Path.Combine(dir, "Editor");
                if (!Directory.Exists(ed)) continue;
                var singleXml = Directory.GetFiles(ed, "*.xml", SearchOption.TopDirectoryOnly);
                if (singleXml.Length == 1)
                {
                    var content = File.ReadAllText(singleXml[0]);
                    var av = MediationParseAndroid(content, config.AndroidArtifactPattern);
                    var iv = MediationParseIos(content, config.IosPodName);
                    if (!string.IsNullOrEmpty(av) || !string.IsNullOrEmpty(iv))
                        return new MediationVersionSource { FilePath = singleXml[0], AndroidVersion = av ?? "", IosVersion = iv ?? "", IsAdapter = !config.PluginUsesUnderlyingSdk, NetworkId = config.NetworkId };
                }
            }
            return null;
        }
        private static void AddSingleXmlFromEditor(List<string> pathsToTry, string editorDir)
        {
            if (string.IsNullOrEmpty(editorDir) || !Directory.Exists(editorDir)) return;
            var xmlFiles = Directory.GetFiles(editorDir, "*.xml", SearchOption.TopDirectoryOnly);
            if (xmlFiles.Length == 1)
                pathsToTry.Add(xmlFiles[0]);
        }
        public static MediationVersionSource FindAppLovinAdapterVersions(MediationNetworkConfig config)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return null;
            var pathsToTry = new List<string>();
            var adapterEditorDir = Path.Combine(projectRoot, config.AppLovinAdapterPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            AddSingleXmlFromEditor(pathsToTry, adapterEditorDir);
            if (config.NetworkId?.Equals("applovin", StringComparison.OrdinalIgnoreCase) == true)
            {
                AddSingleXmlFromEditor(pathsToTry, Path.Combine(projectRoot, "Packages", "com.google.ads.mobile.mediation.applovin", "Editor"));
                var gmaMediation = Path.Combine(projectRoot, "Assets", "GoogleMobileAds", "Mediation", "AppLovin");
                if (Directory.Exists(gmaMediation))
                {
                    foreach (var subDir in Directory.GetDirectories(gmaMediation, "*", SearchOption.AllDirectories))
                    {
                        if (subDir.EndsWith("Editor", StringComparison.OrdinalIgnoreCase))
                            AddSingleXmlFromEditor(pathsToTry, subDir);
                    }
                }
            }
            var patterns = new[] { config.AppLovinAndroidAdapterPrefix?.TrimEnd(':'), config.AndroidArtifactPattern };
            foreach (var deps in pathsToTry)
            {
                if (!File.Exists(deps)) continue;
                var content = File.ReadAllText(deps);
                string av = null, iv = null;
                foreach (var p in patterns)
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    av = MediationParseAndroid(content, p);
                    if (!string.IsNullOrEmpty(av)) break;
                }
                if (string.IsNullOrEmpty(av) && config.NetworkId?.Equals("applovin", StringComparison.OrdinalIgnoreCase) == true)
                    av = MediationParseAndroid(content, "applovin-sdk");
                iv = MediationParseIos(content, config.AppLovinIosAdapterName);
                if (string.IsNullOrEmpty(iv)) iv = MediationParseIos(content, config.IosPodName);
                if (string.IsNullOrEmpty(iv) && config.NetworkId?.Equals("applovin", StringComparison.OrdinalIgnoreCase) == true)
                    iv = MediationParseIos(content, "AppLovinSDK");
                if (string.IsNullOrEmpty(av) && string.IsNullOrEmpty(iv)) continue;
                return new MediationVersionSource { FilePath = deps, AndroidVersion = av ?? "", IosVersion = iv ?? "", IsAdapter = true, NetworkId = config.NetworkId };
            }
            return null;
        }
        public static (MediationNetworkConfig Config, MediationVersionSource Plugin, MediationVersionSource Adapter, string TargetAndroid, string TargetIos, string FileToUpdate)? DetectMismatchAndResolve(MediationNetworkConfig config)
        {
            var plugin = FindPluginVersions(config);
            var adapter = FindAppLovinAdapterVersions(config);
            if (plugin == null || adapter == null) return null;
            var mismatch = MediationCompareVersions(plugin.AndroidVersion ?? "", adapter.AndroidVersion ?? "") != 0 || MediationCompareVersions(plugin.IosVersion ?? "", adapter.IosVersion ?? "") != 0;
            if (!mismatch) return null;
            string ta, ti, fileToUpdate;
            if (config.AlwaysUpdateAdapterOnly)
            {
                fileToUpdate = adapter.FilePath;
                ta = plugin.AndroidVersion ?? adapter.AndroidVersion ?? "";
                ti = plugin.IosVersion ?? adapter.IosVersion ?? "";
            }
            else
            {
                ta = string.IsNullOrEmpty(plugin.AndroidVersion) ? adapter.AndroidVersion : (string.IsNullOrEmpty(adapter.AndroidVersion) ? plugin.AndroidVersion : (MediationCompareVersions(plugin.AndroidVersion, adapter.AndroidVersion) <= 0 ? plugin.AndroidVersion : adapter.AndroidVersion));
                ti = string.IsNullOrEmpty(plugin.IosVersion) ? adapter.IosVersion : (string.IsNullOrEmpty(adapter.IosVersion) ? plugin.IosVersion : (MediationCompareVersions(plugin.IosVersion, adapter.IosVersion) <= 0 ? plugin.IosVersion : adapter.IosVersion));
                fileToUpdate = (plugin.IsAdapter && adapter.IsAdapter && (MediationCompareVersions(plugin.AndroidVersion, adapter.AndroidVersion) > 0 || MediationCompareVersions(plugin.IosVersion, adapter.IosVersion) > 0)) ? plugin.FilePath : adapter.FilePath;
            }
            if (string.IsNullOrEmpty(fileToUpdate) || !File.Exists(fileToUpdate)) return null;
            return (config, plugin, adapter, ta, ti, fileToUpdate);
        }
        public static bool UpdateToMatchVersion(DiscoveredAdapterInfo adapterInfo, string filePath, string androidVersion, string iosVersion)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) || adapterInfo == null) return false;
            var pathNorm = filePath?.Replace("\\", "/") ?? "";
            var current = MediationReadCurrentVersionsForUpdate(pathNorm, filePath, adapterInfo.AndroidAdapterPrefix, adapterInfo.IosAdapterName);
            if (MediationIsUpgradeTarget(androidVersion, current.Android) || MediationIsUpgradeTarget(iosVersion, current.Ios))
                return false;
            if (pathNorm.IndexOf("GoogleMobileAds/Mediation", StringComparison.OrdinalIgnoreCase) >= 0)
                return MediationUpdateAdMobAdapterGeneric(filePath, androidVersion, iosVersion);
            if (pathNorm.IndexOf("GoogleMobileAds", StringComparison.OrdinalIgnoreCase) >= 0)
                return MediationUpdateGmaFile(filePath, androidVersion, iosVersion);
            return MediationUpdateAdapterFileGeneric(filePath, androidVersion, iosVersion, adapterInfo.AndroidAdapterPrefix, adapterInfo.IosAdapterName);
        }
        private static bool MediationUpdateAdMobAdapterGeneric(string filePath, string androidVersion, string iosVersion)
        {
            var content = File.ReadAllText(filePath);
            var modified = false;
            if (!string.IsNullOrEmpty(androidVersion))
            {
                var ver = MediationToAdapterFormat(androidVersion, 4);
                var m = Regex.Match(content, @"(androidPackage[^>]*spec\s*=\s*[""'])([^""']*?)(:\[?)([\d.]+)(\]?[""'])", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var rep = m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value + ver + m.Groups[5].Value;
                    content = content.Substring(0, m.Index) + rep + content.Substring(m.Index + m.Length);
                    modified = true;
                }
            }
            if (!string.IsNullOrEmpty(iosVersion))
            {
                var ver = MediationToAdapterFormat(iosVersion, 4);
                var m = Regex.Match(content, @"(iosPod[^>]*version\s*=\s*[""'])([\d.]+)([""'])", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var rep = m.Groups[1].Value + ver + m.Groups[3].Value;
                    content = content.Substring(0, m.Index) + rep + content.Substring(m.Index + m.Length);
                    modified = true;
                }
                if (!modified && Regex.IsMatch(content, @"iosPod[^>]*name\s*=", RegexOptions.IgnoreCase))
                {
                    m = Regex.Match(content, @"(version\s*=\s*[""'])([\d.]+)([""'][^>]*name\s*=\s*[""'][^""']+[""'])", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var rep = m.Groups[1].Value + ver + m.Groups[3].Value;
                        content = content.Substring(0, m.Index) + rep + content.Substring(m.Index + m.Length);
                        modified = true;
                    }
                }
            }
            if (modified) File.WriteAllText(filePath, content);
            return modified;
        }
        public static bool UpdateToMatchVersion(MediationNetworkConfig config, string filePath, string androidVersion, string iosVersion)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
            var pathNorm = filePath?.Replace("\\", "/") ?? "";
            var current = MediationReadCurrentVersionsForUpdate(
                pathNorm,
                filePath,
                config?.AppLovinAndroidAdapterPrefix ?? config?.AndroidArtifactPattern,
                config?.AppLovinIosAdapterName ?? config?.IosPodName);
            if (MediationIsUpgradeTarget(androidVersion, current.Android) || MediationIsUpgradeTarget(iosVersion, current.Ios))
                return false;
            if (pathNorm.IndexOf("MaxSdk/Mediation", StringComparison.OrdinalIgnoreCase) >= 0)
                return MediationUpdateAdapterFile(config, filePath, androidVersion, iosVersion);
            if (pathNorm.IndexOf("GoogleMobileAds/Mediation", StringComparison.OrdinalIgnoreCase) >= 0)
                return MediationUpdateAdMobAdapterGeneric(filePath, androidVersion, iosVersion);
            if (config.AlwaysUpdateAdapterOnly && pathNorm.IndexOf("GoogleMobileAds", StringComparison.OrdinalIgnoreCase) >= 0)
                return MediationUpdateAdapterFileGeneric(filePath, androidVersion, iosVersion, config.AppLovinAndroidAdapterPrefix ?? config.AndroidArtifactPattern, config.AppLovinIosAdapterName ?? config.IosPodName);
            if (config.PluginUsesUnderlyingSdk && pathNorm.IndexOf("GoogleMobileAds", StringComparison.OrdinalIgnoreCase) >= 0)
                return MediationUpdateGmaFile(filePath, androidVersion, iosVersion);
            return MediationUpdateAdapterFile(config, filePath, androidVersion, iosVersion);
        }
        private static bool MediationUpdateAdapterFileGeneric(string filePath, string androidVersion, string iosVersion, string androidPrefix, string iosPodName)
        {
            if (string.IsNullOrEmpty(androidPrefix) && string.IsNullOrEmpty(iosPodName)) return false;
            var content = File.ReadAllText(filePath);
            var modified = false;
            if (!string.IsNullOrEmpty(androidVersion) && !string.IsNullOrEmpty(androidPrefix))
            {
                var ver = MediationToAdapterFormat(androidVersion, 4);
                var prefix = androidPrefix.TrimEnd(':');
                var pat = Regex.Escape(prefix) + @"[:\[]?[\d.]+\]?";
                var rep = prefix + ":[" + ver + "]";
                var nc = Regex.Replace(content, pat, rep, RegexOptions.IgnoreCase);
                if (nc != content) { content = nc; modified = true; }
            }
            if (!string.IsNullOrEmpty(iosVersion) && !string.IsNullOrEmpty(iosPodName))
            {
                var ver = MediationToAdapterFormat(iosVersion, 4);
                var pat = @"(name\s*=\s*[""']" + Regex.Escape(iosPodName) + @"[""'][^>]*version\s*=\s*[""'])[^""']*([""'])";
                var nc = Regex.Replace(content, pat, "${1}" + ver + "${2}", RegexOptions.IgnoreCase);
                if (nc == content)
                {
                    pat = @"(version\s*=\s*[""'])[^""']*([""'][^>]*name\s*=\s*[""']" + Regex.Escape(iosPodName) + @"[""'])";
                    nc = Regex.Replace(content, pat, "${1}" + ver + "${2}", RegexOptions.IgnoreCase);
                }
                if (nc != content) { content = nc; modified = true; }
            }
            if (modified) File.WriteAllText(filePath, content);
            return modified;
        }
        private static bool MediationIsUpgradeTarget(string target, string current)
        {
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(current))
                return false;
            return MediationCompareVersions(target, current) > 0;
        }
        private static (string Android, string Ios) MediationReadCurrentVersionsForUpdate(
            string pathNorm,
            string filePath,
            string androidPrefix,
            string iosPodName)
        {
            var content = File.ReadAllText(filePath);
            if (pathNorm.IndexOf("GoogleMobileAds/Mediation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var android = MediationParseAndroid(content, "com.applovin.mediation");
                if (string.IsNullOrEmpty(android))
                    android = MediationParseAndroid(content, "com.google.android.gms:play-services-ads");
                var ios = MediationParseIos(content, "AppLovinMediation");
                if (string.IsNullOrEmpty(ios))
                    ios = MediationParseIos(content, "Google-Mobile-Ads-SDK");
                return (android ?? "", ios ?? "");
            }
            if (pathNorm.IndexOf("GoogleMobileAds", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var android = MediationParseAndroid(content, "com.google.android.gms:play-services-ads");
                var ios = MediationParseIos(content, "Google-Mobile-Ads-SDK");
                return (android ?? "", ios ?? "");
            }
            var normalizedPrefix = (androidPrefix ?? "").Trim().TrimEnd(':');
            var a = string.IsNullOrEmpty(normalizedPrefix) ? "" : MediationParseAndroid(content, normalizedPrefix);
            var i = string.IsNullOrEmpty(iosPodName) ? "" : MediationParseIos(content, iosPodName);
            return (a ?? "", i ?? "");
        }
        private static bool MediationUpdateAdapterFile(MediationNetworkConfig config, string filePath, string androidVersion, string iosVersion)
        {
            var content = File.ReadAllText(filePath);
            var modified = false;
            if (!string.IsNullOrEmpty(androidVersion))
            {
                var ver = MediationToAdapterFormat(androidVersion, config.AdapterVersionSegments);
                var pat = Regex.Escape(config.AppLovinAndroidAdapterPrefix) + @"\[[\d.]+\]";
                var rep = config.AppLovinAndroidAdapterPrefix + "[" + ver + "]";
                var nc = Regex.Replace(content, pat, rep, RegexOptions.IgnoreCase);
                if (nc != content) { content = nc; modified = true; }
            }
            if (!string.IsNullOrEmpty(iosVersion))
            {
                var ver = MediationToAdapterFormat(iosVersion, config.AdapterVersionSegments);
                var pat = @"(name\s*=\s*[""']" + Regex.Escape(config.AppLovinIosAdapterName) + @"[""'][^>]*version\s*=\s*[""'])[^""']*([""'])";
                var nc = Regex.Replace(content, pat, "${1}" + ver + "${2}", RegexOptions.IgnoreCase);
                if (nc == content)
                {
                    pat = @"(version\s*=\s*[""'])[^""']*([""'][^>]*name\s*=\s*[""']" + Regex.Escape(config.AppLovinIosAdapterName) + @"[""'])";
                    nc = Regex.Replace(content, pat, "${1}" + ver + "${2}", RegexOptions.IgnoreCase);
                }
                if (nc != content) { content = nc; modified = true; }
            }
            if (modified) File.WriteAllText(filePath, content);
            return modified;
        }
        private static bool MediationUpdateGmaFile(string filePath, string androidVersion, string iosVersion)
        {
            const string ps = "com.google.android.gms:play-services-ads";
            const string ios = "Google-Mobile-Ads-SDK";
            var content = File.ReadAllText(filePath);
            var modified = false;
            if (!string.IsNullOrEmpty(androidVersion))
            {
                var nc = Regex.Replace(content, Regex.Escape(ps) + @":[\d.]+", ps + ":" + androidVersion, RegexOptions.IgnoreCase);
                if (nc != content) { content = nc; modified = true; }
            }
            if (!string.IsNullOrEmpty(iosVersion))
            {
                var pat = @"(name\s*=\s*[""']" + Regex.Escape(ios) + @"[""'][^>]*version\s*=\s*[""'])[^""']*([""'])";
                var nc = Regex.Replace(content, pat, "${1}" + iosVersion + "${2}", RegexOptions.IgnoreCase);
                if (nc == content)
                {
                    pat = @"(version\s*=\s*[""'])[^""']*([""'][^>]*name\s*=\s*[""']" + Regex.Escape(ios) + @"[""'])";
                    nc = Regex.Replace(content, pat, "${1}" + iosVersion + "${2}", RegexOptions.IgnoreCase);
                }
                if (nc != content) { content = nc; modified = true; }
            }
            if (modified) File.WriteAllText(filePath, content);
            return modified;
        }
        public enum MismatchKind
        {
            AdapterVsPlugin,
            AdMobVsAppLovin
        }
        public class MediationMismatchResult
        {
            public string DisplayName;
            public MismatchKind Kind;
            public MediationVersionSource Plugin;
            public MediationVersionSource Adapter;
            public string TargetAndroid;
            public string TargetIos;
            public string FileToUpdate;
            public DiscoveredAdapterInfo AdapterInfo;
            public MediationNetworkConfig Config;
            public string SupportAndroid;
            public string SupportIos;
        }
        public static List<MediationMismatchResult> ScanAllMismatches()
        {
            var result = new List<MediationMismatchResult>();
            var admobInstalled = DiscoverInstalledAdMobAdapters();
            var applovinInstalled = DiscoverAllAppLovinAdapters();
            foreach (var (admobDef, admobPath, admobA, admobI) in admobInstalled)
            {
                var applovin = applovinInstalled.FirstOrDefault(a => AdMobDefMatchesAppLovin(admobDef, a.NetworkId));
                if (applovin == null) continue;
                var counterpart = new MediationVersionSource { FilePath = admobPath, AndroidVersion = admobA, IosVersion = admobI, IsAdapter = true, NetworkId = admobDef.IntegrationSlug };
                var support = TryGetSupportForCounterpart(counterpart);
                string ta, ti;
                bool mismatch;
                string fileToUpdate;
                if (support.HasValue && (!string.IsNullOrEmpty(support.Value.Android) || !string.IsNullOrEmpty(support.Value.Ios)))
                {
                    ta = support.Value.Android ?? "";
                    ti = support.Value.Ios ?? "";
                    var adapterDiffers = MediationCompareVersions(applovin.AndroidVersion ?? "", ta) != 0 || MediationCompareVersions(applovin.IosVersion ?? "", ti) != 0;
                    var counterpartDiffers = MediationCompareVersions(admobA ?? "", ta) != 0 || MediationCompareVersions(admobI ?? "", ti) != 0;
                    mismatch = adapterDiffers || counterpartDiffers;
                    if (!mismatch) continue;
                    if (counterpartDiffers && !adapterDiffers)
                        fileToUpdate = counterpart.FilePath;
                    else if (adapterDiffers && !counterpartDiffers)
                        fileToUpdate = applovin.FilePath;
                    else
                        fileToUpdate = MediationCompareVersions(admobA ?? "", ta) > 0 || MediationCompareVersions(admobI ?? "", ti) > 0
                            ? counterpart.FilePath
                            : applovin.FilePath;
                }
                else
                {
                    ta = PickLower(applovin.AndroidVersion, admobA);
                    ti = PickLower(applovin.IosVersion, admobI);
                    mismatch = MediationCompareVersions(applovin.AndroidVersion ?? "", admobA ?? "") != 0 ||
                              MediationCompareVersions(applovin.IosVersion ?? "", admobI ?? "") != 0;
                    if (!mismatch) continue;
                    if (MediationCompareVersions(admobA ?? "", applovin.AndroidVersion ?? "") > 0 || MediationCompareVersions(admobI ?? "", applovin.IosVersion ?? "") > 0)
                        fileToUpdate = counterpart.FilePath;
                    else
                        fileToUpdate = applovin.FilePath;
                }
                if (string.IsNullOrEmpty(fileToUpdate) || !File.Exists(fileToUpdate)) continue;
                result.Add(new MediationMismatchResult
                {
                    DisplayName = admobDef.DisplayName,
                    Kind = MismatchKind.AdMobVsAppLovin,
                    Plugin = counterpart,
                    Adapter = new MediationVersionSource { FilePath = applovin.FilePath, AndroidVersion = applovin.AndroidVersion, IosVersion = applovin.IosVersion, IsAdapter = true, NetworkId = applovin.NetworkId },
                    TargetAndroid = ta,
                    TargetIos = ti,
                    FileToUpdate = fileToUpdate,
                    AdapterInfo = applovin,
                    Config = null,
                    SupportAndroid = support.HasValue ? support.Value.Android : null,
                    SupportIos = support.HasValue ? support.Value.Ios : null
                });
            }
            foreach (var config in GetLegacyNetworkConfigs())
            {
                var r = DetectMismatchAndResolve(config);
                if (!r.HasValue) continue;
                var (c, plugin, adapter, ta, ti, fileToUpdate) = r.Value;
                if (result.Any(x => x.FileToUpdate == fileToUpdate)) continue;
                result.Add(new MediationMismatchResult
                {
                    DisplayName = c.DisplayName,
                    Kind = MismatchKind.AdapterVsPlugin,
                    Plugin = plugin,
                    Adapter = adapter,
                    TargetAndroid = ta,
                    TargetIos = ti,
                    FileToUpdate = fileToUpdate,
                    AdapterInfo = null,
                    Config = c
                });
            }
            return result;
        }
        private static string PickLower(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? "";
            if (string.IsNullOrEmpty(b)) return a ?? "";
            return MediationCompareVersions(a, b) <= 0 ? a : b;
        }
        private static string GetPackageIdFromAdMobPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;
            var p = filePath.Replace("\\", "/");
            var packagesIdx = p.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase);
            if (packagesIdx >= 0)
            {
                var after = p.Substring(packagesIdx + 9);
                var end = after.IndexOf('/');
                return end >= 0 ? after.Substring(0, end) : after;
            }
            var mediationIdx = p.IndexOf("/Mediation/", StringComparison.OrdinalIgnoreCase);
            if (mediationIdx < 0) return null;
            var folderStart = mediationIdx + 11;
            var folderEnd = p.IndexOf('/', folderStart);
            var folder = folderEnd >= 0 ? p.Substring(folderStart, folderEnd - folderStart) : p.Substring(folderStart);
            var def = AdMobAdapterConfig.AllAdapters?.FirstOrDefault(a => string.Equals(a?.MediationFolderName, folder, StringComparison.OrdinalIgnoreCase));
            return def?.PackageId;
        }
        private static string ToMediationVersion(string adapterVersion)
        {
            if (string.IsNullOrEmpty(adapterVersion) || !adapterVersion.Contains(".")) return adapterVersion ?? "";
            var parts = adapterVersion.Trim().Split('.');
            return string.Join(".", parts.Take(Math.Min(3, parts.Length)));
        }
        private static (string Android, string Ios)? TryGetSupportForCounterpart(MediationVersionSource counterpart)
        {
            if (counterpart == null || string.IsNullOrEmpty(counterpart.FilePath)) return null;
            var packageId = GetPackageIdFromAdMobPath(counterpart.FilePath);
            if (string.IsNullOrEmpty(packageId)) return null;
            var ver = ToMediationVersion(counterpart.AndroidVersion ?? counterpart.IosVersion);
            if (string.IsNullOrEmpty(ver)) return null;
            return AdMobAppLovinCompatibilityService.TryGetAdMobSupportVersions(packageId, ver);
        }
        /// <summary>Prefetch all installed AdMob adapters (1 fetch per slug). Call before ScanAllMismatches for cache-first flow.</summary>
        public static void PrefetchAllInstalledAdapters(Action onComplete = null)
        {
            var admobInstalled = DiscoverInstalledAdMobAdapters();
            var slugs = admobInstalled.Select(x => x.Def?.IntegrationSlug).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            AdMobChangelogVersionFetcher.PrefetchAllAdaptersAsync(slugs, onComplete);
        }
        public static void PrefetchSupportForMismatches(List<MediationMismatchResult> mismatches, Action onComplete = null)
        {
            if (mismatches == null || mismatches.Count == 0) { onComplete?.Invoke(); return; }
            var pending = 0;
            foreach (var m in mismatches)
            {
                if (m?.Plugin == null || m.Kind != MismatchKind.AdMobVsAppLovin) continue;
                var path = m.Plugin.FilePath ?? "";
                if (path.IndexOf("GoogleMobileAds", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!string.IsNullOrEmpty(m.SupportAndroid) || !string.IsNullOrEmpty(m.SupportIos)) continue;
                var packageId = GetPackageIdFromAdMobPath(path);
                if (string.IsNullOrEmpty(packageId)) continue;
                var ver = ToMediationVersion(m.Plugin.AndroidVersion ?? m.Plugin.IosVersion);
                if (string.IsNullOrEmpty(ver)) continue;
                var def = AdMobAdapterConfig.AllAdapters?.FirstOrDefault(a => string.Equals(a?.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
                var slug = def?.IntegrationSlug ?? (packageId.StartsWith("com.google.ads.mobile.mediation.", StringComparison.OrdinalIgnoreCase) ? packageId.Substring(34) : packageId);
                pending++;
                AdMobChangelogVersionFetcher.FetchSupportedVersionsAsync(slug, ver, (android, ios, _) =>
                {
                    if (!string.IsNullOrEmpty(android) || !string.IsNullOrEmpty(ios))
                    {
                        m.SupportAndroid = android;
                        m.SupportIos = ios;
                    }
                    pending--;
                    if (pending <= 0) onComplete?.Invoke();
                });
            }
            if (pending <= 0) onComplete?.Invoke();
        }
        private static readonly Dictionary<string, string> AdMobToAppLovinFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["unity"] = "UnityAds",
            ["unityads"] = "UnityAds"
        };
        public static string FindAdMobAdapterDependenciesPath(AdMobAdapterConfig.AdapterDef adapterDef)
        {
            if (adapterDef == null) return null;
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return null;
            var folder = (adapterDef.MediationFolderName ?? "").Trim();
            var pkgId = (adapterDef.PackageId ?? "").Trim();
            var editorDirs = new List<string>
            {
                Path.Combine(projectRoot, "Assets", "GoogleMobileAds", "Mediation", folder, "Editor")
            };
            if (!string.IsNullOrEmpty(pkgId))
            {
                editorDirs.Add(Path.Combine(projectRoot, "Packages", pkgId, "Editor"));
                var pkgCache = Path.Combine(projectRoot, "Library", "PackageCache");
                if (Directory.Exists(pkgCache))
                {
                    foreach (var d in Directory.GetDirectories(pkgCache, pkgId + "@*"))
                        editorDirs.Add(Path.Combine(d, "Editor"));
                }
            }
            foreach (var editorDir in editorDirs)
            {
                if (!Directory.Exists(editorDir)) continue;
                var xmlFiles = Directory.GetFiles(editorDir, "*.xml", SearchOption.TopDirectoryOnly);
                if (xmlFiles.Length == 1)
                    return xmlFiles[0];
            }
            var mediationDir = Path.Combine(projectRoot, "Assets", "GoogleMobileAds", "Mediation", folder);
            if (Directory.Exists(mediationDir))
            {
                foreach (var subDir in Directory.GetDirectories(mediationDir, "*", SearchOption.AllDirectories))
                {
                    if (!subDir.EndsWith("Editor", StringComparison.OrdinalIgnoreCase)) continue;
                    var xmlFiles = Directory.GetFiles(subDir, "*.xml", SearchOption.TopDirectoryOnly);
                    if (xmlFiles.Length == 1)
                        return xmlFiles[0];
                }
            }
            return null;
        }
        public static MediationMismatchResult CheckAdMobAdapterCompatibility(AdMobAdapterConfig.AdapterDef adapterDef,
            string androidSupportVersion = null, string iosSupportVersion = null)
        {
            if (adapterDef == null) return null;
            var slug = (adapterDef.IntegrationSlug ?? "").Trim().ToLowerInvariant();
            var folder = (adapterDef.MediationFolderName ?? "").Trim().ToLowerInvariant();
            var useSupportVersions = !string.IsNullOrEmpty(androidSupportVersion) || !string.IsNullOrEmpty(iosSupportVersion);
            MediationNetworkConfig config = null;
            foreach (var c in GetLegacyNetworkConfigs())
            {
                var id = (c.NetworkId ?? "").ToLowerInvariant();
                if (id == slug || id == folder) { config = c; break; }
            }
            if (config != null)
            {
                var plugin = FindPluginVersions(config);
                var adapterSource = FindAppLovinAdapterVersions(config);
                var admobVerA = useSupportVersions ? (androidSupportVersion ?? "") : (adapterSource?.AndroidVersion ?? "");
                var admobVerI = useSupportVersions ? (iosSupportVersion ?? "") : (adapterSource?.IosVersion ?? "");
                if (plugin == null) return null;
                if (string.IsNullOrEmpty(plugin.AndroidVersion) && string.IsNullOrEmpty(plugin.IosVersion)) return null;
                if (!useSupportVersions && adapterSource == null) return null;
                var mismatch = MediationCompareVersions(admobVerA, plugin.AndroidVersion ?? "") != 0 ||
                               MediationCompareVersions(admobVerI, plugin.IosVersion ?? "") != 0;
                if (!mismatch) return null;
                var fileToUpdate = adapterSource?.FilePath ?? FindAdMobAdapterDependenciesPath(adapterDef);
                string ta, ti;
                if (config.AlwaysUpdateAdapterOnly)
                {
                    ta = plugin.AndroidVersion ?? admobVerA;
                    ti = plugin.IosVersion ?? admobVerI;
                }
                else
                {
                    ta = MediationCompareVersions(admobVerA, plugin.AndroidVersion ?? "") > 0 ? admobVerA : plugin.AndroidVersion ?? admobVerA;
                    ti = MediationCompareVersions(admobVerI, plugin.IosVersion ?? "") > 0 ? admobVerI : plugin.IosVersion ?? admobVerI;
                }
                if (string.IsNullOrEmpty(ta)) ta = plugin.AndroidVersion ?? "";
                if (string.IsNullOrEmpty(ti)) ti = plugin.IosVersion ?? "";
                return new MediationMismatchResult
                {
                    DisplayName = config.DisplayName,
                    Kind = MismatchKind.AdapterVsPlugin,
                    Plugin = plugin,
                    Adapter = new MediationVersionSource { FilePath = fileToUpdate, AndroidVersion = admobVerA, IosVersion = admobVerI, IsAdapter = true, NetworkId = config.NetworkId },
                    TargetAndroid = ta,
                    TargetIos = ti,
                    FileToUpdate = fileToUpdate,
                    AdapterInfo = null,
                    Config = config
                };
            }
            var appLovinFolder = !string.IsNullOrEmpty(adapterDef.AppLovinNetworkId) ? adapterDef.AppLovinNetworkId : (AdMobToAppLovinFolder.TryGetValue(folder, out var mapped) ? mapped : adapterDef.MediationFolderName);
            foreach (var alAdapter in DiscoverAllAppLovinAdapters())
            {
                var alId = (alAdapter.NetworkId ?? "").ToLowerInvariant();
                if (alId != folder && alId != slug && alId != (appLovinFolder ?? "").ToLowerInvariant()) continue;
                var counterpart = FindCounterpart(alAdapter);
                if (counterpart == null) continue;
                var pathNorm = counterpart.FilePath?.Replace("\\", "/") ?? "";
                if (pathNorm.IndexOf("GoogleMobileAds", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var admobA = useSupportVersions ? (androidSupportVersion ?? "") : (counterpart.AndroidVersion ?? "");
                var admobI = useSupportVersions ? (iosSupportVersion ?? "") : (counterpart.IosVersion ?? "");
                var mismatch = MediationCompareVersions(alAdapter.AndroidVersion ?? "", admobA) != 0 ||
                               MediationCompareVersions(alAdapter.IosVersion ?? "", admobI) != 0;
                if (!mismatch) continue;
                var ta = MediationCompareVersions(alAdapter.AndroidVersion ?? "", admobA) > 0 ? alAdapter.AndroidVersion : admobA;
                var ti = MediationCompareVersions(alAdapter.IosVersion ?? "", admobI) > 0 ? alAdapter.IosVersion : admobI;
                if (string.IsNullOrEmpty(ta)) ta = counterpart.AndroidVersion ?? alAdapter.AndroidVersion ?? "";
                if (string.IsNullOrEmpty(ti)) ti = counterpart.IosVersion ?? alAdapter.IosVersion ?? "";
                return new MediationMismatchResult
                {
                    DisplayName = adapterDef.DisplayName,
                    Kind = MismatchKind.AdMobVsAppLovin,
                    Plugin = new MediationVersionSource { FilePath = alAdapter.FilePath, AndroidVersion = alAdapter.AndroidVersion, IosVersion = alAdapter.IosVersion, IsAdapter = true, NetworkId = alAdapter.NetworkId },
                    Adapter = new MediationVersionSource { FilePath = counterpart.FilePath, AndroidVersion = admobA, IosVersion = admobI, IsAdapter = true, NetworkId = adapterDef.IntegrationSlug },
                    TargetAndroid = ta ?? "",
                    TargetIos = ti ?? "",
                    FileToUpdate = counterpart.FilePath,
                    AdapterInfo = alAdapter,
                    Config = null
                };
            }
            return null;
        }
        private static List<MediationNetworkConfig> GetLegacyNetworkConfigs()
        {
            return new List<MediationNetworkConfig>
            {
                new MediationNetworkConfig
                {
                    NetworkId = "applovin",
                    DisplayName = "AppLovin",
                    PluginPaths = new[] { "Assets/MaxSdk/AppLovin/Editor" },
                    AndroidArtifactPattern = "com.applovin:applovin-sdk",
                    IosPodName = "AppLovinSDK",
                    PluginUsesUnderlyingSdk = true,
                    AppLovinAdapterPath = "Assets/GoogleMobileAds/Mediation/AppLovin/Editor",
                    AppLovinAndroidAdapterPrefix = "com.applovin:applovin-sdk:",
                    AppLovinIosAdapterName = "AppLovinSDK",
                    AdapterVersionSegments = 4,
                    AlwaysUpdateAdapterOnly = true
                },
                new MediationNetworkConfig
                {
                    NetworkId = "google",
                    DisplayName = "Google/AdMob",
                    PluginPaths = new[] { "Assets/GoogleMobileAds/Editor/GoogleMobileAdsDependencies.xml" },
                    AndroidArtifactPattern = "com.google.android.gms:play-services-ads",
                    IosPodName = "Google-Mobile-Ads-SDK",
                    PluginUsesUnderlyingSdk = true,
                    AppLovinAdapterPath = "Assets/MaxSdk/Mediation/Google/Editor",
                    AppLovinAndroidAdapterPrefix = "com.applovin.mediation:google-adapter:",
                    AppLovinIosAdapterName = "AppLovinMediationGoogleAdapter",
                    AdapterVersionSegments = 4
                },
                new MediationNetworkConfig
                {
                    NetworkId = "facebook",
                    DisplayName = "Facebook/Meta",
                    PluginPaths = new[] { "Assets/MaxSdk/Mediation/Facebook/Editor", "Packages/com.google.ads.mobile.mediation.metaaudiencenetwork" },
                    AndroidArtifactPattern = "com.facebook.android:audience-network-sdk",
                    IosPodName = "FBAudienceNetwork",
                    PluginUsesUnderlyingSdk = true,
                    AppLovinAdapterPath = "Assets/MaxSdk/Mediation/Facebook/Editor",
                    AppLovinAndroidAdapterPrefix = "com.applovin.mediation:facebook-adapter:",
                    AppLovinIosAdapterName = "AppLovinMediationFacebookAdapter",
                    AdapterVersionSegments = 4
                }
            };
        }
    }
}