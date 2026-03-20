using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
namespace Shion.SDK.Editor
{
    public static class AdMobChangelogVersionFetcher
    {
        private static readonly Dictionary<string, CachedResult> Cache = new();
        private static readonly Dictionary<string, FullAdapterCache> FullAdapterCaches = new(StringComparer.OrdinalIgnoreCase);
        private const double CacheSeconds = 3600;
        internal struct CachedResult
        {
            public string AndroidVersion;
            public string IosVersion;
            public string DownloadUrl;
            public double FetchedAt;
        }
        private struct FullAdapterCache
        {
            public Dictionary<string, CachedResult> Versions;
            public double FetchedAt;
        }
        private static readonly Dictionary<string, List<Action<Dictionary<string, CachedResult>, string>>> PendingFetches = new(StringComparer.OrdinalIgnoreCase);
        public static (string Android, string Ios)? GetCachedSupportedVersions(string integrationSlug, string mediationVersion)
        {
            if (string.IsNullOrWhiteSpace(integrationSlug) || string.IsNullOrWhiteSpace(mediationVersion))
                return null;
            var slug = integrationSlug.ToLowerInvariant();
            var targetVer = mediationVersion.Trim();
            var cacheKey = $"{slug}|{targetVer}";
            if (Cache.TryGetValue(cacheKey, out var cached) &&
                EditorApplication.timeSinceStartup - cached.FetchedAt < CacheSeconds)
            {
                if (!string.IsNullOrEmpty(cached.AndroidVersion) || !string.IsNullOrEmpty(cached.IosVersion))
                    return (cached.AndroidVersion ?? "", cached.IosVersion ?? "");
            }
            if (FullAdapterCaches.TryGetValue(slug, out var full) &&
                EditorApplication.timeSinceStartup - full.FetchedAt < CacheSeconds &&
                full.Versions != null)
            {
                var match = FindVersionMatch(full.Versions, targetVer);
                if (match.HasValue && (!string.IsNullOrEmpty(match.Value.AndroidVersion) || !string.IsNullOrEmpty(match.Value.IosVersion)))
                    return (match.Value.AndroidVersion ?? "", match.Value.IosVersion ?? "");
            }
            return null;
        }
        private static CachedResult? FindVersionMatch(Dictionary<string, CachedResult> versions, string targetVersion)
        {
            if (versions == null || string.IsNullOrEmpty(targetVersion)) return null;
            if (versions.TryGetValue(targetVersion, out var exact))
                return exact;
            foreach (var kv in versions)
            {
                if (MatchesVersion(kv.Key, targetVersion))
                    return kv.Value;
            }
            return null;
        }
        /// <summary>Resolve Unity mediation version from support versions (Android/iOS in Dependencies.xml). Returns null if not in cache.</summary>
        public static string ResolveUnityVersionFromSupportVersions(string integrationSlug, string androidVersion, string iosVersion)
        {
            if (string.IsNullOrWhiteSpace(integrationSlug)) return null;
            var slug = integrationSlug.ToLowerInvariant();
            var android = (androidVersion ?? "").Trim();
            var ios = (iosVersion ?? "").Trim();
            if (string.IsNullOrEmpty(android) && string.IsNullOrEmpty(ios)) return null;
            if (!FullAdapterCaches.TryGetValue(slug, out var full) || full.Versions == null) return null;
            foreach (var kv in full.Versions)
            {
                var a = (kv.Value.AndroidVersion ?? "").Trim();
                var i = (kv.Value.IosVersion ?? "").Trim();
                var matchA = string.IsNullOrEmpty(android) || string.IsNullOrEmpty(a) || a == android || a.StartsWith(android + ".") || android.StartsWith(a + ".");
                var matchI = string.IsNullOrEmpty(ios) || string.IsNullOrEmpty(i) || i == ios || i.StartsWith(ios + ".") || ios.StartsWith(i + ".");
                if (matchA && matchI) return kv.Key;
            }
            return null;
        }
        private static CachedResult? GetCachedResult(string integrationSlug, string mediationVersion)
        {
            if (string.IsNullOrWhiteSpace(integrationSlug) || string.IsNullOrWhiteSpace(mediationVersion))
                return null;
            var slug = integrationSlug.ToLowerInvariant();
            var targetVer = mediationVersion.Trim();
            var cacheKey = $"{slug}|{targetVer}";
            if (Cache.TryGetValue(cacheKey, out var cached) &&
                EditorApplication.timeSinceStartup - cached.FetchedAt < CacheSeconds)
                return cached;
            if (FullAdapterCaches.TryGetValue(slug, out var full) &&
                EditorApplication.timeSinceStartup - full.FetchedAt < CacheSeconds &&
                full.Versions != null)
                return FindVersionMatch(full.Versions, targetVer);
            return null;
        }
        public static void FetchSupportedVersionsAsync(
            string integrationSlug,
            string mediationVersion,
            Action<string, string, string> onComplete)
        {
            if (onComplete == null || string.IsNullOrWhiteSpace(integrationSlug) || string.IsNullOrWhiteSpace(mediationVersion))
            {
                onComplete?.Invoke(null, null, "Invalid parameters");
                return;
            }
            var slug = integrationSlug.ToLowerInvariant();
            var targetVer = mediationVersion.Trim();
            var cached = GetCachedSupportedVersions(integrationSlug, mediationVersion);
            if (cached.HasValue)
            {
                onComplete(cached.Value.Android, cached.Value.Ios, null);
                return;
            }
            void InvokeFromFull(Dictionary<string, CachedResult> versions)
            {
                var match = FindVersionMatch(versions, targetVer);
                if (match.HasValue)
                {
                    onComplete(match.Value.AndroidVersion, match.Value.IosVersion, null);
                    return;
                }
                onComplete(null, null, null);
            }
            if (FullAdapterCaches.TryGetValue(slug, out var full) &&
                EditorApplication.timeSinceStartup - full.FetchedAt < CacheSeconds &&
                full.Versions != null)
            {
                InvokeFromFull(full.Versions);
                return;
            }
            FetchFullAdapterPageAsync(slug, (versions, err) =>
            {
                if (err != null || versions == null)
                {
                    onComplete(null, null, err);
                    return;
                }
                InvokeFromFull(versions);
            });
        }
        /// <summary>Fetch full adapter page once, parse all versions, fill cache. Used by PrefetchAllAdaptersAsync.</summary>
        internal static void FetchFullAdapterPageAsync(string integrationSlug, Action<Dictionary<string, CachedResult>, string> onComplete)
        {
            if (onComplete == null || string.IsNullOrWhiteSpace(integrationSlug))
            {
                onComplete?.Invoke(null, "Invalid parameters");
                return;
            }
            var slug = integrationSlug.Trim().ToLowerInvariant();
            if (FullAdapterCaches.TryGetValue(slug, out var full) &&
                EditorApplication.timeSinceStartup - full.FetchedAt < CacheSeconds &&
                full.Versions != null && full.Versions.Count > 0)
            {
                onComplete(full.Versions, null);
                return;
            }
            if (PendingFetches.TryGetValue(slug, out var list))
            {
                list.Add((versions, err) => onComplete(versions, err));
                return;
            }
            var callbacks = new List<Action<Dictionary<string, CachedResult>, string>>();
            PendingFetches[slug] = callbacks;
            callbacks.Add((versions, err) => onComplete(versions, err));
            var url = $"{AdMobAdapterConfig.IntegrateAdSourcesBaseUrl}{integrationSlug}";
            var request = UnityWebRequest.Get(url);
            var op = request.SendWebRequest();
            void ProcessResponse()
            {
                PendingFetches.Remove(slug);
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        var err = request.error ?? "Request failed";
                        Debug.LogWarning($"[ShionSDK] AdMob changelog fetch failed ({slug}): {err}");
                        foreach (var cb in callbacks)
                            cb(null, err);
                        return;
                    }
                    var html = request.downloadHandler?.text;
                    if (string.IsNullOrEmpty(html))
                    {
                        foreach (var cb in callbacks)
                            cb(null, "Empty response");
                        return;
                    }
                    var versions = ParseChangelogAllVersions(html);
                    var now = EditorApplication.timeSinceStartup;
                    FullAdapterCaches[slug] = new FullAdapterCache { Versions = versions, FetchedAt = now };
                    foreach (var kv in versions)
                    {
                        var key = $"{slug}|{kv.Key}";
                        Cache[key] = new CachedResult
                        {
                            AndroidVersion = kv.Value.AndroidVersion,
                            IosVersion = kv.Value.IosVersion,
                            DownloadUrl = kv.Value.DownloadUrl,
                            FetchedAt = now
                        };
                    }
                    foreach (var cb in callbacks)
                        cb(versions, null);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ShionSDK] Parse changelog failed ({slug}): {e.Message}");
                    foreach (var cb in callbacks)
                        cb(null, e.Message);
                }
                finally
                {
                    request.Dispose();
                }
            }
            if (op.isDone)
                ProcessResponse();
            else
            {
                void OnUpdate()
                {
                    if (!op.isDone) return;
                    EditorApplication.update -= OnUpdate;
                    ProcessResponse();
                }
                EditorApplication.update += OnUpdate;
            }
        }
        /// <summary>Prefetch all adapters by slug. Fetch 1 request per slug, parse all versions, fill cache.</summary>
        public static void PrefetchAllAdaptersAsync(IEnumerable<string> integrationSlugs, Action onComplete = null)
        {
            var slugs = integrationSlugs?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();
            if (slugs == null || slugs.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }
            var pending = slugs.Count;
            foreach (var slug in slugs)
            {
                FetchFullAdapterPageAsync(slug, (_, _) =>
                {
                    if (--pending <= 0)
                        onComplete?.Invoke();
                });
            }
        }
        private static Dictionary<string, CachedResult> ParseChangelogAllVersions(string html)
        {
            var result = new Dictionary<string, CachedResult>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(html)) return result;
            var versionHeaderRegex = new Regex(
                @"id=""version-([0-9]+\.[0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)(?:-[^""]*)?""",
                RegexOptions.IgnoreCase);
            var versionHeaderFallback = new Regex(
                @"(?:####\s+Version|>\s*)Version\s+([0-9]+\.[0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)\s*(?:\([^)]*\))?",
                RegexOptions.IgnoreCase);
            var androidRegex = new Regex(
                @"Android\s+adapter\s+version\s+([0-9]+\.[0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)",
                RegexOptions.IgnoreCase);
            var iosRegex = new Regex(
                @"iOS\s+adapter\s+version\s+([0-9]+\.[0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)",
                RegexOptions.IgnoreCase);
            var sectionMatches = versionHeaderRegex.Matches(html);
            if (sectionMatches.Count == 0)
                sectionMatches = versionHeaderFallback.Matches(html);
            for (var i = 0; i < sectionMatches.Count; i++)
            {
                var m = sectionMatches[i];
                var sectionVersion = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(sectionVersion) || sectionVersion.Contains("in-progress")) continue;
                var majorParts = sectionVersion.Split('.');
                if (majorParts.Length > 0 && int.TryParse(majorParts[0], out var major) && major >= 8) continue;
                var startIdx = m.Index + m.Length;
                var endIdx = i + 1 < sectionMatches.Count
                    ? sectionMatches[i + 1].Index
                    : html.Length;
                var block = html.Substring(startIdx, Math.Min(endIdx - startIdx, 3000));
                var androidMatch = androidRegex.Match(block);
                var iosMatch = iosRegex.Match(block);
                var zipMatch = Regex.Match(block, @"href=""(https?://[^""]+\.zip)""", RegexOptions.IgnoreCase);
                var android = androidMatch.Success ? androidMatch.Groups[1].Value : null;
                var ios = iosMatch.Success ? iosMatch.Groups[1].Value : null;
                var downloadUrl = zipMatch.Success ? zipMatch.Groups[1].Value : null;
                if (!string.IsNullOrEmpty(android) || !string.IsNullOrEmpty(ios) || !string.IsNullOrEmpty(downloadUrl))
                {
                    result[sectionVersion] = new CachedResult
                    {
                        AndroidVersion = android,
                        IosVersion = ios,
                        DownloadUrl = downloadUrl,
                        FetchedAt = EditorApplication.timeSinceStartup
                    };
                }
            }
            return result;
        }
        private static readonly Dictionary<string, CachedVersionList> VersionListCache = new();
        private struct CachedVersionList
        {
            public List<string> Versions;
            public string Latest;
            public double FetchedAt;
        }
        public static void FetchVersionListAsync(string integrationSlug, Action<List<string>, string, string> onComplete)
        {
            if (onComplete == null || string.IsNullOrWhiteSpace(integrationSlug))
            {
                onComplete?.Invoke(null, null, "Invalid parameters");
                return;
            }
            var key = integrationSlug.ToLowerInvariant();
            if (VersionListCache.TryGetValue(key, out var cached) &&
                EditorApplication.timeSinceStartup - cached.FetchedAt < CacheSeconds &&
                cached.Versions != null && cached.Versions.Count > 0)
            {
                onComplete(cached.Versions, cached.Latest, null);
                return;
            }
            var url = $"{AdMobAdapterConfig.IntegrateAdSourcesBaseUrl}{integrationSlug}";
            var request = UnityWebRequest.Get(url);
            var op = request.SendWebRequest();
            void Process()
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onComplete(null, null, request.error);
                        return;
                    }
                    var html = request.downloadHandler?.text;
                    if (string.IsNullOrEmpty(html))
                    {
                        onComplete(null, null, "Empty response");
                        return;
                    }
                    ParseVersionList(html, out var versions, out var latest);
                    VersionListCache[key] = new CachedVersionList
                    {
                        Versions = versions,
                        Latest = latest,
                        FetchedAt = EditorApplication.timeSinceStartup
                    };
                    onComplete(versions, latest, null);
                }
                finally { request.Dispose(); }
            }
            if (op.isDone) Process();
            else
            {
                void OnUpdate()
                {
                    if (!op.isDone) return;
                    EditorApplication.update -= OnUpdate;
                    Process();
                }
                EditorApplication.update += OnUpdate;
            }
        }
        /// <summary>Parse adapter versions (Unity mediation) from changelog. Excludes support versions (9.x, 11.x = Android/iOS).</summary>
        private static void ParseVersionList(string html, out List<string> versions, out string latest)
        {
            versions = new List<string>();
            latest = null;
            var versionHeaderRegex = new Regex(
                @"id=""version-([0-9]+\.[0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)(?:-[^""]*)?""",
                RegexOptions.IgnoreCase);
            var fallback = new Regex(
                @"(?:####\s+Version|>\s*)Version\s+([0-9]+\.[0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)\s*(?:\([^)]*\))?",
                RegexOptions.IgnoreCase);
            var matches = versionHeaderRegex.Matches(html);
            if (matches.Count == 0) matches = fallback.Matches(html);
            var seen = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var v = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(v) || v.Contains("in-progress")) continue;
                var parts = v.Split('.');
                if (parts.Length > 0 && int.TryParse(parts[0], out var major) && major >= 8) continue;
                if (seen.Add(v)) versions.Add(v);
            }
            VersionComparisonService.SortDescending(versions);
            if (versions.Count > 0) latest = versions[0];
        }
        public static void FetchDownloadUrlAsync(string integrationSlug, string mediationVersion,
            Action<string, string> onComplete)
        {
            if (onComplete == null || string.IsNullOrWhiteSpace(integrationSlug) || string.IsNullOrWhiteSpace(mediationVersion))
            {
                onComplete?.Invoke(null, "Invalid parameters");
                return;
            }
            var cached = GetCachedResult(integrationSlug, mediationVersion);
            if (cached.HasValue && !string.IsNullOrEmpty(cached.Value.DownloadUrl))
            {
                onComplete(cached.Value.DownloadUrl, null);
                return;
            }
            var slug = integrationSlug.ToLowerInvariant();
            FetchFullAdapterPageAsync(slug, (versions, err) =>
            {
                if (err != null || versions == null)
                {
                    onComplete(null, err ?? "Fetch failed");
                    return;
                }
                var match = FindVersionMatch(versions, mediationVersion.Trim());
                if (match.HasValue && !string.IsNullOrEmpty(match.Value.DownloadUrl))
                    onComplete(match.Value.DownloadUrl, null);
                else
                    onComplete(null, "Download link not found");
            });
        }
        private static bool MatchesVersion(string sectionVersion, string targetVersion)
        {
            if (string.IsNullOrEmpty(sectionVersion) || string.IsNullOrEmpty(targetVersion))
                return false;
            if (string.Equals(sectionVersion, targetVersion, StringComparison.OrdinalIgnoreCase))
                return true;
            if (sectionVersion.StartsWith(targetVersion + ".", StringComparison.OrdinalIgnoreCase))
                return true;
            if (targetVersion.StartsWith(sectionVersion + ".", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
        public static void ClearCache(string integrationSlug = null, string mediationVersion = null)
        {
            if (string.IsNullOrEmpty(integrationSlug) && string.IsNullOrEmpty(mediationVersion))
            {
                Cache.Clear();
                VersionListCache.Clear();
                FullAdapterCaches.Clear();
                PendingFetches.Clear();
                return;
            }
            var toRemove = new List<string>();
            foreach (var key in Cache.Keys)
            {
                var parts = key.Split('|');
                if (parts.Length != 2) continue;
                var matchSlug = string.IsNullOrEmpty(integrationSlug) ||
                    string.Equals(parts[0], integrationSlug, StringComparison.OrdinalIgnoreCase);
                var matchVer = string.IsNullOrEmpty(mediationVersion) ||
                    string.Equals(parts[1], mediationVersion, StringComparison.Ordinal);
                if (matchSlug && matchVer)
                    toRemove.Add(key);
            }
            foreach (var k in toRemove)
                Cache.Remove(k);
            if (!string.IsNullOrEmpty(integrationSlug))
            {
                VersionListCache.Remove(integrationSlug.ToLowerInvariant());
                FullAdapterCaches.Remove(integrationSlug.ToLowerInvariant());
            }
        }
    }
}