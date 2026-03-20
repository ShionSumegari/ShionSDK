using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace Shion.SDK.Editor
{
    public static class AdMobAdapterInstaller
    {
        private const string DownloadsFolder = "Library/ShionSDK/AdMobDownloads";
        private const string InstallMetadataPath = "Library/ShionSDK/AdMobAdapterInstalls.json";
        private static readonly object OperationLock = new object();
        private static bool _isOperationInProgress;
        private static string _currentOperation;
        public static bool IsOperationInProgress
        {
            get
            {
                lock (OperationLock) return _isOperationInProgress;
            }
        }
        [Serializable]
        private class InstallRecord
        {
            public string packageId;
            public string version;
            public string mediationFolder;
            public string zipPath;
            public string extractPath;
        }
        [Serializable]
        private class InstallMetadata
        {
            public List<InstallRecord> installs = new();
        }
        public static void Install(string packageId, string version, Action<bool, string> onComplete)
        {
            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
            {
                Debug.LogWarning("[ShionSDK] AdMob adapter install failed: Invalid package or version");
                onComplete?.Invoke(false, "Invalid package or version");
                return;
            }
            if (!TryBeginOperation($"install:{packageId}@{version}", onComplete))
                return;
            var def = AdMobAdapterConfig.AllAdapters.FirstOrDefault(a => a.PackageId == packageId);
            if (def == null)
            {
                Debug.LogWarning("[ShionSDK] AdMob adapter install failed: Unknown adapter");
                CompleteOperation(onComplete, false, "Unknown adapter");
                return;
            }
            AdMobChangelogVersionFetcher.FetchDownloadUrlAsync(def.IntegrationSlug, version, (downloadUrl, err) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(err) || string.IsNullOrEmpty(downloadUrl))
                    {
                        var msg = !string.IsNullOrEmpty(err) ? err : "Download link not found for this version";
                        Debug.LogWarning($"[ShionSDK] AdMob adapter install failed (fetch URL): {msg}");
                        CompleteOperation(onComplete, false, msg);
                        return;
                    }
                    var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                    var downloadsDir = Path.Combine(projectRoot, DownloadsFolder.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    Directory.CreateDirectory(downloadsDir);
                    var zipName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                    var zipPath = Path.Combine(downloadsDir, zipName);
                    void OnDownloadDone(bool ok, string err2)
                    {
                        if (!ok)
                        {
                            var msg2 = !string.IsNullOrEmpty(err2) ? err2 : "Download failed";
                            CompleteOperation(onComplete, false, msg2);
                            return;
                        }
                        ExtractAndImport(zipPath, def, version, projectRoot, (ok3, err3) =>
                        {
                            CompleteOperation(onComplete, ok3, err3);
                        });
                    }
                    DownloadFile(downloadUrl, zipPath, OnDownloadDone);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    CompleteOperation(onComplete, false, ex.Message);
                }
            });
        }
        private static void DownloadFile(string url, string destPath, Action<bool, string> onComplete)
        {
            var request = UnityEngine.Networking.UnityWebRequest.Get(url);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(destPath);
            var op = request.SendWebRequest();
            void Process()
            {
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.InProgress)
                    return;
                EditorApplication.update -= Process;
                try
                {
                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                        onComplete(true, null);
                    else
                    {
                        var errMsg = !string.IsNullOrEmpty(request.error) ? request.error : $"Download failed (result: {request.result})";
                        Debug.LogWarning($"[ShionSDK] AdMob adapter install failed (download): {errMsg}");
                        onComplete(false, errMsg);
                    }
                }
                finally { request.Dispose(); }
            }
            if (op.isDone) Process();
            else EditorApplication.update += Process;
        }
        private static void ExtractAndImport(string zipPath, AdMobAdapterConfig.AdapterDef def, string version,
            string projectRoot, Action<bool, string> onComplete)
        {
            var extractDir = Path.Combine(Path.GetDirectoryName(zipPath), Path.GetFileNameWithoutExtension(zipPath));
            string unityPackagePath = null;
            try
            {
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                var unityPackages = Directory.GetFiles(extractDir, "*.unitypackage", SearchOption.AllDirectories);
                if (unityPackages.Length == 0)
                {
                    Debug.LogWarning("[ShionSDK] AdMob adapter install failed: No .unitypackage found in zip");
                    onComplete(false, "No .unitypackage found in zip");
                    return;
                }
                unityPackagePath = unityPackages[0];
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShionSDK] AdMob adapter install failed: {ex.Message}");
                onComplete(false, ex.Message);
                return;
            }
            var completed = false;
            void CompleteOnce(bool ok, string err)
            {
                if (completed) return;
                completed = true;
                AssetDatabase.importPackageCompleted -= OnImportDone;
                AssetDatabase.importPackageCancelled -= OnImportCancelled;
                EditorApplication.update -= ImportWatchdog;
                if (ok)
                {
                    AssetDatabase.Refresh();
                    SaveInstallRecord(def.PackageId, version, def.MediationFolderName, zipPath, extractDir);
                    if (string.Equals(def.IntegrationSlug, "applovin", StringComparison.OrdinalIgnoreCase))
                    {
                        RemoveDuplicateAppLovinIosPodDeclaration(projectRoot, def.MediationFolderName);
                    }
                }
                onComplete(ok, err);
            }
            AssetDatabase.importPackageCompleted += OnImportDone;
            AssetDatabase.importPackageCancelled += OnImportCancelled;
            AssetDatabase.ImportPackage(unityPackagePath, false);
            EditorApplication.update += ImportWatchdog;
            void OnImportDone(string _)
            {
                CompleteOnce(true, null);
            }
            void OnImportCancelled(string _)
            {
                Debug.LogWarning("[ShionSDK] AdMob adapter install failed: Import cancelled");
                CompleteOnce(false, "Import cancelled");
            }
            void ImportWatchdog()
            {
                if (completed) return;
                if (IsMediationFolderPresent(projectRoot, def.MediationFolderName))
                {
                    Debug.Log($"[ShionSDK] AdMob adapter install watchdog completed: {def.DisplayName}");
                    CompleteOnce(true, null);
                }
            }
        }
        private static bool IsMediationFolderPresent(string projectRoot, string mediationFolderName)
        {
            var mediationRoot = Path.Combine(projectRoot, "Assets", "GoogleMobileAds", "Mediation");
            if (!Directory.Exists(mediationRoot)) return false;
            foreach (var dir in Directory.GetDirectories(mediationRoot))
            {
                var name = Path.GetFileName(dir);
                if (string.Equals(name, mediationFolderName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        private static void RemoveDuplicateAppLovinIosPodDeclaration(string projectRoot, string mediationFolderName)
        {
            try
            {
                var editorDir = Path.Combine(projectRoot, "Assets", "GoogleMobileAds", "Mediation", mediationFolderName, "Editor");
                if (!Directory.Exists(editorDir)) return;
                var xmlFiles = Directory.GetFiles(editorDir, "*.xml", SearchOption.TopDirectoryOnly);
                if (xmlFiles.Length == 0) return;
                foreach (var xmlPath in xmlFiles)
                {
                    var content = File.ReadAllText(xmlPath);
                    var updated = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"\s*<iosPod[^>]*name\s*=\s*[""']AppLovinSDK[""'][^>]*/>\s*",
                        Environment.NewLine,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (!string.Equals(updated, content, StringComparison.Ordinal))
                    {
                        File.WriteAllText(xmlPath, updated);
                        Debug.Log($"[ShionSDK] Removed duplicate AppLovinSDK pod declaration: {xmlPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShionSDK] Failed to remove duplicate AppLovinSDK pod declaration: {ex.Message}");
            }
        }
        private static void SaveInstallRecord(string packageId, string version, string mediationFolder,
            string zipPath, string extractPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var metaPath = Path.Combine(projectRoot, InstallMetadataPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath));
            var meta = LoadMetadata(metaPath);
            meta.installs.RemoveAll(r => r.packageId == packageId);
            meta.installs.Add(new InstallRecord
            {
                packageId = packageId,
                version = version,
                mediationFolder = mediationFolder,
                zipPath = zipPath,
                extractPath = extractPath
            });
            File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
        }
        private static InstallMetadata LoadMetadata(string path)
        {
            if (!File.Exists(path)) return new InstallMetadata();
            try
            {
                var json = File.ReadAllText(path);
                var meta = JsonUtility.FromJson<InstallMetadata>(json);
                return meta ?? new InstallMetadata();
            }
            catch { return new InstallMetadata(); }
        }
        public static void Uninstall(string packageId, Action<bool, string> onComplete)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                onComplete?.Invoke(false, "Invalid package ID");
                return;
            }
            if (!TryBeginOperation($"uninstall:{packageId}", onComplete))
                return;
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var metaPath = Path.Combine(projectRoot, InstallMetadataPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            var meta = LoadMetadata(metaPath);
            var record = meta.installs.FirstOrDefault(r => r.packageId == packageId);
            var def = AdMobAdapterConfig.AllAdapters.FirstOrDefault(a => a.PackageId == packageId);
            try
            {
                if (record != null)
                {
                    meta.installs.Remove(record);
                    File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
                    if (!string.IsNullOrEmpty(record.zipPath) && File.Exists(record.zipPath))
                    {
                        try { File.Delete(record.zipPath); } catch { }
                    }
                    if (!string.IsNullOrEmpty(record.extractPath) && Directory.Exists(record.extractPath))
                    {
                        try { Directory.Delete(record.extractPath, true); } catch { }
                    }
                }
                if (def != null)
                {
                    var assetsPath = $"Assets/GoogleMobileAds/Mediation/{def.MediationFolderName}";
                    if (AssetDatabase.IsValidFolder("Assets/GoogleMobileAds/Mediation") &&
                        AssetDatabase.IsValidFolder(assetsPath))
                    {
                        AssetDatabase.DeleteAsset(assetsPath);
                    }
                }
                RemoveFromManifest(projectRoot, packageId);
                AssetDatabase.Refresh();
                CompleteOperation(onComplete, true, null);
            }
            catch (Exception ex)
            {
                CompleteOperation(onComplete, false, ex.Message);
            }
        }
        private static bool TryBeginOperation(string operationName, Action<bool, string> onComplete)
        {
            lock (OperationLock)
            {
                if (_isOperationInProgress)
                {
                    var message = $"Another AdMob adapter operation is running: {_currentOperation}";
                    Debug.LogWarning($"[ShionSDK] {message}");
                    onComplete?.Invoke(false, message);
                    return false;
                }
                _isOperationInProgress = true;
                _currentOperation = operationName;
                Debug.Log($"[ShionSDK] Begin AdMob adapter operation: {_currentOperation}");
                return true;
            }
        }
        private static void CompleteOperation(Action<bool, string> onComplete, bool ok, string err)
        {
            string op;
            lock (OperationLock)
            {
                op = _currentOperation;
                _currentOperation = null;
                _isOperationInProgress = false;
            }
            Debug.Log($"[ShionSDK] End AdMob adapter operation: {op ?? "unknown"}, ok={ok}, err={err ?? "none"}");
            onComplete?.Invoke(ok, err);
        }
        private static void RemoveFromManifest(string projectRoot, string packageId)
        {
            var manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(manifestPath)) return;
            try
            {
                var json = File.ReadAllText(manifestPath);
                var depRegex = new System.Text.RegularExpressions.Regex(
                    $"\\s*\"{System.Text.RegularExpressions.Regex.Escape(packageId)}\"\\s*:\\s*\"[^\"]*\"\\s*,?");
                var newJson = depRegex.Replace(json, "");
                newJson = System.Text.RegularExpressions.Regex.Replace(newJson, @",\s*}", "}");
                newJson = System.Text.RegularExpressions.Regex.Replace(newJson, @",\s*,", ",");
                if (newJson != json)
                    File.WriteAllText(manifestPath, newJson);
            }
            catch { }
        }
        public static bool IsInstalled(string packageId)
        {
            var def = AdMobAdapterConfig.AllAdapters.FirstOrDefault(a => a.PackageId == packageId);
            if (def == null) return false;
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var assetsPath = Path.Combine(projectRoot, "Assets", "GoogleMobileAds", "Mediation", def.MediationFolderName);
            if (Directory.Exists(assetsPath)) return true;
            var manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (File.Exists(manifestPath) && File.ReadAllText(manifestPath).Contains($"\"{packageId}\""))
                return true;
            return false;
        }
        public static string GetInstalledVersion(string packageId)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var metaPath = Path.Combine(projectRoot, InstallMetadataPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            var meta = LoadMetadata(metaPath);
            var record = meta.installs.FirstOrDefault(r => r.packageId == packageId);
            if (record != null) return record.version;
            var manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                var json = File.ReadAllText(manifestPath);
                var m = System.Text.RegularExpressions.Regex.Match(
                    json, $"\"{System.Text.RegularExpressions.Regex.Escape(packageId)}\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }
    }
}