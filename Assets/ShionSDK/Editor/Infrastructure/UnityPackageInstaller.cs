using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Shion.SDK.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
namespace Shion.SDK.Editor
{
    public class UnityPackageInstaller : IModuleInstaller
    {
        public void Install(Module module)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(module.UnityPackageLocalPath))
            {
                InstallFromLocalPath(module);
                return;
            }
            if (string.IsNullOrEmpty(module.UnityPackageRepo))
            {
                Debug.LogError($"[ShionSDK] UnityPackageInstaller: Module '{module.Name}' has no UnityPackageRepo or UnityPackageLocalPath configured.");
                return;
            }
            if (!ModuleVersionSelectionStore.TryGet(module.Id, out var tag) || string.IsNullOrEmpty(tag))
            {
                Debug.LogError($"[ShionSDK] UnityPackageInstaller: No version selected for '{module.Name}'. Please select a version first.");
                return;
            }
            string owner;
            string repo;
            var repoSpec = module.UnityPackageRepo.Trim();
            if (GitHubReleaseVersionFetcher.TryParseGitHubUrl(repoSpec, out owner, out repo))
            { }
            else
            {
                var split = repoSpec.Split('/');
                if (split.Length != 2)
                {
                    Debug.LogError($"[ShionSDK] UnityPackageInstaller: Invalid UnityPackageRepo '{module.UnityPackageRepo}'. Expected 'owner/repo' or a GitHub URL.");
                    return;
                }
                owner = split[0].Trim();
                repo = split[1].Trim();
            }
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
            var request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "ShionSDK-Unity");
            var token = EditorPrefs.GetString("ShionSDK.GitHubToken", "");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", "Bearer " + token);
            var op = request.SendWebRequest();
            while (!op.isDone)
                EditorApplication.QueuePlayerLoopUpdate();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ShionSDK] UnityPackageInstaller: Failed to fetch release for '{module.Name}' tag '{tag}': {request.error} (HTTP {(int)request.responseCode})");
                request.Dispose();
                return;
            }
            var json = request.downloadHandler?.text;
            request.Dispose();
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[ShionSDK] UnityPackageInstaller: Empty release response for '{module.Name}' tag '{tag}'.");
                return;
            }
            var assetName = FindUnityPackageAssetName(json);
            if (string.IsNullOrEmpty(assetName))
            {
                Debug.LogError($"[ShionSDK] UnityPackageInstaller: No .unitypackage asset found for '{module.Name}' tag '{tag}'.");
                return;
            }
            var downloadUrl = $"https://github.com/{owner}/{repo}/releases/download/{tag}/{assetName}";
            var tempDir = Path.Combine(Path.GetTempPath(), "ShionSDK_UnityPackages");
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);
            var tempPath = Path.Combine(tempDir, assetName);
            Debug.Log($"[ShionSDK] Downloading unitypackage for '{module.Name}' from '{downloadUrl}'...");
            var dl = UnityWebRequest.Get(downloadUrl);
            dl.downloadHandler = new DownloadHandlerFile(tempPath);
            dl.SetRequestHeader("User-Agent", "ShionSDK-Unity");
            if (!string.IsNullOrEmpty(token))
                dl.SetRequestHeader("Authorization", "Bearer " + token);
            var dlOp = dl.SendWebRequest();
            while (!dlOp.isDone)
                EditorApplication.QueuePlayerLoopUpdate();
            if (dl.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ShionSDK] UnityPackageInstaller: Download failed for '{module.Name}': {dl.error} (HTTP {(int)dl.responseCode})");
                dl.Dispose();
                return;
            }
            dl.Dispose();
            Debug.Log($"[ShionSDK] Importing unitypackage for '{module.Name}' from '{tempPath}'...");
            AssetDatabase.ImportPackage(tempPath, false);
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                if (Directory.Exists(tempDir) && Directory.GetFiles(tempDir).Length == 0 && Directory.GetDirectories(tempDir).Length == 0)
                    Directory.Delete(tempDir, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShionSDK] Failed to clean up temp unitypackage '{tempPath}': {e.Message}");
            }
#else
            Debug.LogWarning("[ShionSDK] UnityPackage install is only supported inside the Unity Editor.");
#endif
        }
        public void Uninstall(Module module)
        {
#if UNITY_EDITOR
            if (module.UnityPackageRootFolders == null || module.UnityPackageRootFolders.Count == 0)
                return;
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var root in module.UnityPackageRootFolders)
            {
                if (string.IsNullOrEmpty(root)) continue;
                var normalized = root.Replace("\\", "/");
                var path = normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(projectRoot, normalized)
                    : Path.Combine(projectRoot, "Assets", normalized.TrimStart('/'));
                if (Directory.Exists(path))
                {
                    Debug.Log($"[ShionSDK] Removing unitypackage folder '{path}' for module '{module.Name}'.");
                    try
                    {
                        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
                        {
                            if (Directory.Exists(dir))
                                Directory.Delete(dir, true);
                        }
                        if (Directory.Exists(path))
                            Directory.Delete(path, true);
                        var meta = path + ".meta";
                        if (File.Exists(meta))
                            File.Delete(meta);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ShionSDK] Failed to fully remove folder '{path}' for module '{module.Name}': {e.Message}");
                    }
                }
                else if (File.Exists(path))
                {
                    Debug.Log($"[ShionSDK] Removing unitypackage file '{path}' for module '{module.Name}'.");
                    File.Delete(path);
                }
            }
            AssetDatabase.Refresh();
#else
            Debug.LogWarning("[ShionSDK] UnityPackage uninstall is only supported inside the Unity Editor.");
#endif
        }
        private static void InstallFromLocalPath(Module module)
        {
            var localPath = module.UnityPackageLocalPath.Trim().Replace("\\", "/");
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var fullPath = Path.IsPathRooted(localPath)
                ? localPath
                : Path.Combine(projectRoot, localPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ? localPath : "Assets/" + localPath.TrimStart('/'));
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[ShionSDK] UnityPackageInstaller: Local package file not found for '{module.Name}': '{fullPath}'");
                return;
            }
            if (!fullPath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[ShionSDK] UnityPackageInstaller: Path is not a .unitypackage file: '{fullPath}'");
                return;
            }
            Debug.Log($"[ShionSDK] Importing local unitypackage for '{module.Name}' from '{fullPath}'...");
            AssetDatabase.ImportPackage(fullPath, false);
        }
        private static string FindUnityPackageAssetName(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            var matches = Regex.Matches(json, "\"name\"\\s*:\\s*\"([^\"]+\\.unitypackage)\"");
            if (matches.Count == 0)
                return null;
            return matches[0].Groups[1].Value;
        }
    }
}