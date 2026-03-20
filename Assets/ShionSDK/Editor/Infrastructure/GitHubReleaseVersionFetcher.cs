using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
namespace Shion.SDK.Editor
{
    [Serializable]
    internal class GitHubReleaseDto
    {
        public string tag_name;
        public bool prerelease;
    }
    [Serializable]
    internal class GitHubTagDto
    {
        public string name;
    }
    [Serializable]
    internal class GitHubReleasesWrapper
    {
        public GitHubReleaseDto[] items;
    }
    [Serializable]
    internal class GitHubTagsWrapper
    {
        public GitHubTagDto[] items;
    }
    public static class GitHubReleaseVersionFetcher
    {
        private const string GithubApiBase = "https://api.github.com";
        private const int PerPage = 100;
        private static readonly Dictionary<string, bool> GitInstallableCache = new();
        public static bool TryParseGitHubUrl(string url, out string owner, out string repo)
        {
            owner = null;
            repo = null;
            if (string.IsNullOrWhiteSpace(url))
                return false;
            url = url.Trim();
            var sshMatch = Regex.Match(url, @"git@github\.com:([^/]+)/([^/]+?)(?:\.git)?$");
            if (sshMatch.Success)
            {
                owner = sshMatch.Groups[1].Value;
                repo = sshMatch.Groups[2].Value;
                return true;
            }
            var httpsMatch = Regex.Match(url, @"https?://(?:www\.)?github\.com/([^/]+)/([^/]+?)(?:\.git)?/?(?:releases)?/?$", RegexOptions.IgnoreCase);
            if (httpsMatch.Success)
            {
                owner = httpsMatch.Groups[1].Value;
                repo = httpsMatch.Groups[2].Value;
                return true;
            }
            return false;
        }
        public static void FetchVersionsAsync(string gitHubUrl, bool includePrerelease, int maxReleases,
            Action<List<string>, string> onComplete)
        {
            if (onComplete == null)
                return;
            if (!TryParseGitHubUrl(gitHubUrl, out var owner, out var repo))
            {
                onComplete(null, $"Invalid GitHub URL: '{gitHubUrl}'. Expected format: https://github.com/owner/repo");
                return;
            }
            var apiUrl = $"{GithubApiBase}/repos/{owner}/{repo}/releases?per_page={Math.Min(maxReleases, 100)}";
            var request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "ShionSDK-Unity");
            var token = EditorPrefs.GetString("ShionSDK.GitHubToken", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", "Bearer " + token);
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onComplete(null, $"GitHub API request failed: {request.error} (HTTP {(int)request.responseCode})");
                        return;
                    }
                    var json = request.downloadHandler?.text;
                    if (string.IsNullOrEmpty(json))
                    {
                        onComplete(null, "GitHub API returned empty response.");
                        return;
                    }
                    var versionsFromReleases = ParseTagNamesFromJson(json, includePrerelease);
                    FetchTagNamesAsync(owner, repo, tagsSet =>
                    {
                        var result = ResolveToExistingTags(versionsFromReleases, tagsSet);
                        onComplete(result, null);
                    });
                }
                finally
                {
                    request.Dispose();
                }
            };
        }
        public static List<string> FetchVersions(string gitHubUrl, bool includePrerelease, int maxReleases, out string errorMessage)
        {
            List<string> result = null;
            string error = null;
            var done = false;
            FetchVersionsAsync(gitHubUrl, includePrerelease, maxReleases, (versions, err) =>
            {
                result = versions;
                error = err;
                done = true;
            });
            while (!done)
                EditorApplication.QueuePlayerLoopUpdate();
            errorMessage = error;
            return result;
        }
        private const int TagsMaxPages = 5;
        private const int TagsPerPage = 100;
        private static void FetchTagNamesAsync(string owner, string repo, Action<HashSet<string>> onComplete)
        {
            var tags = new HashSet<string>(StringComparer.Ordinal);
            void FetchPage(int page)
            {
                if (page > TagsMaxPages)
                {
                    onComplete(tags);
                    return;
                }
                var apiUrl = $"{GithubApiBase}/repos/{owner}/{repo}/tags?per_page={TagsPerPage}&page={page}";
                var request = UnityWebRequest.Get(apiUrl);
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                request.SetRequestHeader("User-Agent", "ShionSDK-Unity");
                var token = EditorPrefs.GetString("ShionSDK.GitHubToken", "");
                if (!string.IsNullOrEmpty(token))
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                var operation = request.SendWebRequest();
                operation.completed += _ =>
                {
                    try
                    {
                        var count = 0;
                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            var json = request.downloadHandler?.text;
                            if (!string.IsNullOrEmpty(json) && json != "[]")
                            {
                                var tagsJson = json.TrimStart().StartsWith("[") ? "{\"items\":" + json + "}" : json;
                                try
                                {
                                    var data = JsonUtility.FromJson<GitHubTagsWrapper>(tagsJson);
                                    if (data?.items != null)
                                    {
                                        foreach (var t in data.items)
                                        {
                                            if (t != null && !string.IsNullOrEmpty(t.name))
                                            {
                                                tags.Add(t.name);
                                                count++;
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        if (count < TagsPerPage)
                        {
                            onComplete(tags);
                            return;
                        }
                        FetchPage(page + 1);
                    }
                    finally
                    {
                        request.Dispose();
                    }
                };
            }
            FetchPage(1);
        }
        private static List<string> ResolveToExistingTags(List<string> versionsFromReleases, HashSet<string> tagsSet)
        {
            var resolved = new List<string>(versionsFromReleases.Count);
            foreach (var tag in versionsFromReleases)
            {
                if (tagsSet.Contains(tag))
                {
                    resolved.Add(tag);
                    continue;
                }
                var alternate = tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? tag.Substring(1)
                    : "v" + tag;
                if (tagsSet.Contains(alternate))
                {
                    resolved.Add(alternate);
                    continue;
                }
                resolved.Add(tag);
            }
            return resolved;
        }
        public static List<string> FetchVersionsOrEmpty(string gitHubUrl, bool includePrerelease = true)
        {
            var versions = FetchVersions(gitHubUrl, includePrerelease, 100, out var err);
            if (versions == null)
            {
                Debug.LogWarning($"[ShionSDK] GitHubReleaseVersionFetcher: {err}");
                return new List<string>();
            }
            return versions;
        }
        private static List<string> ParseTagNamesFromJson(string json, bool includePrerelease)
        {
            var versions = new List<string>();
            if (string.IsNullOrEmpty(json))
                return versions;
            if (json.TrimStart().StartsWith("["))
                json = "{\"items\":" + json + "}";
            try
            {
                var data = JsonUtility.FromJson<GitHubReleasesWrapper>(json);
                if (data?.items == null)
                    return versions;
                foreach (var r in data.items)
                {
                    if (r == null || string.IsNullOrEmpty(r.tag_name))
                        continue;
                    if (!includePrerelease && r.prerelease)
                        continue;
                    versions.Add(r.tag_name);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShionSDK] ParseTagNamesFromJson failed: {ex.Message}");
            }
            return versions;
        }
        public static bool IsGitVersionInstallable(string upmSourceOrGitUrl, string versionTag, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(upmSourceOrGitUrl) || string.IsNullOrEmpty(versionTag))
            {
                errorMessage = "Empty Git URL or version tag.";
                return false;
            }
            string urlWithoutQuery = upmSourceOrGitUrl;
            string packagePath = null;
            var qm = upmSourceOrGitUrl.IndexOf("?", StringComparison.Ordinal);
            if (qm >= 0)
            {
                urlWithoutQuery = upmSourceOrGitUrl.Substring(0, qm);
                var query = upmSourceOrGitUrl.Substring(qm + 1);
                var m = Regex.Match(query, @"(?:^|&)path=([^&]+)");
                if (m.Success)
                    packagePath = UnityWebRequest.UnEscapeURL(m.Groups[1].Value);
            }
            if (!TryParseGitHubUrl(urlWithoutQuery, out var owner, out var repo))
            {
                errorMessage = $"Invalid GitHub URL: '{upmSourceOrGitUrl}'.";
                return false;
            }
            var normalizedPath = string.IsNullOrEmpty(packagePath)
                ? "package.json"
                : packagePath.Trim('/').Trim('/') + "/package.json";
            var cacheKey = $"{owner}/{repo}|{normalizedPath}|{versionTag}";
            if (GitInstallableCache.TryGetValue(cacheKey, out var cached))
                return cached;
            var apiUrl = $"{GithubApiBase}/repos/{owner}/{repo}/contents/{normalizedPath}?ref={versionTag}";
            var request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "ShionSDK-Unity");
            var token = EditorPrefs.GetString("ShionSDK.GitHubToken", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", "Bearer " + token);
            var op = request.SendWebRequest();
            while (!op.isDone)
                EditorApplication.QueuePlayerLoopUpdate();
            if (request.result == UnityWebRequest.Result.Success)
            {
                request.Dispose();
                GitInstallableCache[cacheKey] = true;
                return true;
            }
            if ((int)request.responseCode == 404)
                errorMessage = $"package.json not found at path '{normalizedPath}' for version '{versionTag}'.";
            else
                errorMessage = $"GitHub Contents API failed for '{owner}/{repo}' ref '{versionTag}': {request.error} (HTTP {(int)request.responseCode})";
            request.Dispose();
            GitInstallableCache[cacheKey] = false;
            return false;
        }
    }
}