#if USE_APPLOVIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AppLovinMax.Scripts.IntegrationManager.Editor;
using UnityEditor;
using UnityEngine;
using AppLovinNetwork = AppLovinMax.Scripts.IntegrationManager.Editor.Network;
namespace Shion.SDK.Editor
{
    public class AppLovinAdaptersWindow : EditorWindow
    {
        private const string MaxSdkPath = "Assets/MaxSdk";
        private const string PendingXmlPatchesSessionKey = "ShionSDK.AppLovin.PendingXmlPatches";
        private List<AppLovinIntegrationBridge.AdapterInfo> _adapters = new List<AppLovinIntegrationBridge.AdapterInfo>();
        private Dictionary<string, bool> _selected = new Dictionary<string, bool>();
        private Vector2 _scroll;
        private bool _maxSdkInstalled;
        private string _loadError;
        private bool _isInstalling;
        private string _installProgress;
        private Texture2D _removeIcon;
        private readonly List<PendingXmlPatch> _pendingXmlPatches = new List<PendingXmlPatch>();
        private const double PendingXmlPatchInitialDelaySeconds = 1.5d;
        private bool _isDialogOpen;
        private bool _isApplyingPendingXmlPatches;
        private bool _isPendingXmlPatchApplyQueued;
        private double _pendingXmlPatchApplyAt;
        public static void Open()
        {
            var w = GetWindow<AppLovinAdaptersWindow>(true, "AppLovin Adapters", true);
            w.minSize = new Vector2(600, 380);
        }
        private void OnEnable()
        {
            RestorePendingXmlPatchState();
            RefreshState();
            LoadRemoveIcon();
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
            if (_pendingXmlPatches.Count > 0)
                SchedulePendingXmlPatchApply(PendingXmlPatchInitialDelaySeconds);
        }
        private void OnDisable()
        {
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            EditorApplication.update -= PendingXmlPatchApplyTick;
            _isPendingXmlPatchApplyQueued = false;
            SavePendingXmlPatchState();
        }
        private void OnImportPackageCompleted(string packageName)
        {
            if (_pendingXmlPatches.Count > 0)
                SchedulePendingXmlPatchApply(PendingXmlPatchInitialDelaySeconds);
            if (_isInstalling)
                RefreshState();
            Repaint();
        }
        private void LoadRemoveIcon()
        {
            if (_removeIcon != null) return;
            var path = "Assets/MaxSdk/Resources/Images/uninstall_icon.png";
            _removeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (_removeIcon == null)
            {
                var guids = AssetDatabase.FindAssets("uninstall_icon t:Texture2D");
                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (p.Contains("MaxSdk") && p.Contains("uninstall"))
                    {
                        _removeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                        break;
                    }
                }
            }
        }
        private void RefreshState()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var maxSdkFullPath = Path.Combine(projectRoot, MaxSdkPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            _maxSdkInstalled = Directory.Exists(maxSdkFullPath);
            _adapters.Clear();
            _loadError = null;
            if (!_maxSdkInstalled) return;
            if (AppLovinIntegrationBridge.IsAvailable())
            {
                var list = AppLovinIntegrationBridge.LoadAdapters();
                if (list != null && list.Count > 0)
                {
                    _adapters.AddRange(list);
                    foreach (var a in _adapters)
                    {
                        var key = a.Name ?? a.DisplayName ?? "";
                        if (!string.IsNullOrEmpty(key) && !_selected.ContainsKey(key))
                            _selected[key] = false;
                    }
                    return;
                }
            }
            _loadError = "Could not load adapters from Integration Manager. Open 'Integration Manager' first (it fetches the list), then click Refresh. Ensure internet connection.";
        }
        internal void SetInstallProgress(int current, int total)
        {
            _isInstalling = true;
            _installProgress = $"Installing {current}/{total}...";
        }
        internal void OnInstallComplete()
        {
            _isInstalling = false;
            _installProgress = null;
            RefreshState();
            Repaint();
        }
        internal void RefreshAndRepaint()
        {
            RefreshState();
            Repaint();
        }
        private int GetSelectedForInstallCount()
        {
            int n = 0;
            foreach (var a in _adapters)
            {
                var key = a.Name ?? a.DisplayName ?? "";
                if (_selected.TryGetValue(key, out var v) && v && (a.Action == AppLovinIntegrationBridge.ActionType.Install || a.Action == AppLovinIntegrationBridge.ActionType.Upgrade))
                    n++;
            }
            return n;
        }
        private int GetSelectedForRemoveCount()
        {
            int n = 0;
            foreach (var a in _adapters)
            {
                var key = a.Name ?? a.DisplayName ?? "";
                if (_selected.TryGetValue(key, out var v) && v && a.IsInstalled) n++;
            }
            return n;
        }
        private int GetSelectedForUpdateCount()
        {
            int n = 0;
            foreach (var a in _adapters)
            {
                var key = a.Name ?? a.DisplayName ?? "";
                if (_selected.TryGetValue(key, out var v) && v && a.Action == AppLovinIntegrationBridge.ActionType.Upgrade) n++;
            }
            return n;
        }
        private void OnGUI()
        {
            if (!_maxSdkInstalled)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox("AppLovin MAX SDK is not installed. Install it from Shion SDK Manager (SDKs tab) first.", MessageType.Warning);
                if (GUILayout.Button("Open SDK Manager", GUILayout.Height(28)))
                    CompanySDKWindow.Open();
                return;
            }
            if (!string.IsNullOrEmpty(_loadError))
            {
                EditorGUILayout.HelpBox(_loadError, MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Integration Manager (load adapter list)", GUILayout.Height(22)))
                {
                    if (EditorApplication.ExecuteMenuItem("AppLovin/Integration Manager"))
                        Debug.Log("[ShionSDK] Opened Integration Manager. Return here and click Refresh to load adapters.");
                    else
                        ShowDialogSafe("AppLovin Adapters", "Could not open Integration Manager. Ensure AppLovin MAX SDK is installed.", "OK");
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(22)))
                    RefreshState();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
            EditorGUILayout.LabelField("AppLovin MAX Mediation Adapters", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tick adapters, then click 'Install Selected' or 'Remove Selected'. Type: Install / Upgrade / Installed.",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_isInstalling;
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                RefreshState();
            if (GUILayout.Button("Select All", GUILayout.Width(80)))
            {
                foreach (var a in _adapters)
                {
                    var key = a.Name ?? a.DisplayName ?? "";
                    if (!string.IsNullOrEmpty(key)) _selected[key] = true;
                }
            }
            if (GUILayout.Button("Deselect All", GUILayout.Width(90)))
            {
                foreach (var key in new List<string>(_selected.Keys))
                    _selected[key] = false;
            }
            var hasUpgradeable = false;
            foreach (var a in _adapters)
                if (a.Action == AppLovinIntegrationBridge.ActionType.Upgrade) { hasUpgradeable = true; break; }
            if (hasUpgradeable && !_isInstalling)
            {
                if (GUILayout.Button("Select Upgradeable", GUILayout.Width(140), GUILayout.Height(24)))
                {
                    foreach (var a in _adapters)
                    {
                        var key = a.Name ?? a.DisplayName ?? "";
                        if (!string.IsNullOrEmpty(key) && a.Action == AppLovinIntegrationBridge.ActionType.Upgrade)
                            _selected[key] = true;
                    }
                }
            }
            var selectedUpdateCount = GetSelectedForUpdateCount();
            if (selectedUpdateCount > 0 && !_isInstalling)
            {
                if (GUILayout.Button($"Update ({selectedUpdateCount})", GUILayout.Height(24)))
                {
                    var toUpdate = new List<AppLovinNetwork>();
                    var selectedAdapterInfos = new List<AppLovinIntegrationBridge.AdapterInfo>();
                    foreach (var a in _adapters)
                    {
                        var key = a.Name ?? a.DisplayName ?? "";
                        if (_selected.TryGetValue(key, out var v) && v && a.Network != null && a.Action == AppLovinIntegrationBridge.ActionType.Upgrade)
                        {
                            toUpdate.Add(a.Network);
                            selectedAdapterInfos.Add(a);
                        }
                    }
                    if (toUpdate.Count > 0)
                    {
                        InstallAdaptersWithProgress(selectedAdapterInfos, "Updating");
                    }
                }
            }
            var installCount = GetSelectedForInstallCount();
            var removeCount = GetSelectedForRemoveCount();
            GUI.enabled = installCount > 0 && !_isInstalling;
            if (GUILayout.Button($"Install Selected ({installCount})", GUILayout.Height(24)))
            {
                var toInstall = new List<AppLovinNetwork>();
                var selectedAdapterInfos = new List<AppLovinIntegrationBridge.AdapterInfo>();
                foreach (var a in _adapters)
                {
                    var key = a.Name ?? a.DisplayName ?? "";
                    if (_selected.TryGetValue(key, out var v) && v && a.Network != null &&
                        (a.Action == AppLovinIntegrationBridge.ActionType.Install || a.Action == AppLovinIntegrationBridge.ActionType.Upgrade))
                    {
                        toInstall.Add(a.Network);
                        selectedAdapterInfos.Add(a);
                    }
                }
                if (toInstall.Count > 0)
                {
                    InstallAdaptersWithProgress(selectedAdapterInfos, "Installing");
                }
            }
            GUI.enabled = removeCount > 0 && !_isInstalling;
            if (GUILayout.Button($"Remove Selected ({removeCount})", GUILayout.Height(24)))
            {
                var toRemove = new List<AppLovinNetwork>();
                foreach (var a in _adapters)
                {
                    var key = a.Name ?? a.DisplayName ?? "";
                    if (_selected.TryGetValue(key, out var v) && v && a.Network != null && a.IsInstalled)
                        toRemove.Add(a.Network);
                }
                if (toRemove.Count > 0 && ShowDialogSafe("Remove Adapters", $"Remove {toRemove.Count} adapter(s)?", "Remove", "Cancel"))
                {
                    AppLovinIntegrationBridge.RemoveAdapters(toRemove);
                    RefreshState();
                    Repaint();
                }
            }
            GUI.enabled = !_isInstalling;
            if (GUILayout.Button("Open Integration Manager", GUILayout.Height(24)))
            {
                if (EditorApplication.ExecuteMenuItem("AppLovin/Integration Manager"))
                    Debug.Log("[ShionSDK] Opened AppLovin Integration Manager.");
                else
                    ShowDialogSafe("AppLovin Adapters", "Could not open AppLovin Integration Manager. Make sure AppLovin MAX SDK is properly installed.", "OK");
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            if (_isInstalling && !string.IsNullOrEmpty(_installProgress))
            {
                EditorGUILayout.LabelField(_installProgress, EditorStyles.miniLabel);
            }
            GUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.BeginVertical("box");
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            var iconBtnStyle = new GUIStyle(EditorStyles.miniButton) { fixedWidth = 24, fixedHeight = 20, padding = new RectOffset(2, 2, 2, 2) };
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);
            EditorGUILayout.LabelField("", headerStyle, GUILayout.Width(22));
            EditorGUILayout.LabelField("Adapter", headerStyle, GUILayout.MinWidth(160));
            EditorGUILayout.LabelField("Type", headerStyle, GUILayout.Width(70));
            EditorGUILayout.LabelField("Android", headerStyle, GUILayout.Width(70));
            EditorGUILayout.LabelField("iOS", headerStyle, GUILayout.Width(70));
            EditorGUILayout.LabelField("", headerStyle, GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            var adaptersSnapshot = new List<AppLovinIntegrationBridge.AdapterInfo>(_adapters);
            foreach (var a in adaptersSnapshot)
            {
                var key = a.Name ?? a.DisplayName ?? "";
                if (string.IsNullOrEmpty(key)) continue;
                if (!_selected.ContainsKey(key)) _selected[key] = false;
                var typeStr = a.Action.ToString();
                var typeColor = a.Action == AppLovinIntegrationBridge.ActionType.Installed ? new Color(0.2f, 0.8f, 0.2f) :
                    a.Action == AppLovinIntegrationBridge.ActionType.Upgrade ? new Color(1f, 0.6f, 0f) : Color.gray;
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = !_isInstalling && a.Network != null;
                _selected[key] = EditorGUILayout.Toggle(_selected[key], GUILayout.Width(22));
                GUI.enabled = true;
                EditorGUILayout.LabelField(a.DisplayName ?? a.Name ?? "-", GUILayout.MinWidth(160));
                var typeStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = typeColor } };
                EditorGUILayout.LabelField(typeStr, typeStyle, GUILayout.Width(70));
                EditorGUILayout.LabelField(a.AndroidVersion ?? "-", GUILayout.Width(70));
                EditorGUILayout.LabelField(a.IosVersion ?? "-", GUILayout.Width(70));
                var installBtnLabel = a.Action == AppLovinIntegrationBridge.ActionType.Upgrade ? "Upgrade" : "Install";
                var canInstall = a.Action == AppLovinIntegrationBridge.ActionType.Install || a.Action == AppLovinIntegrationBridge.ActionType.Upgrade;
                var installBtnWidth = EditorStyles.miniButton.CalcSize(new GUIContent(installBtnLabel)).x;
                GUI.enabled = !_isInstalling && canInstall && a.Network != null;
                if (GUILayout.Button(installBtnLabel, EditorStyles.miniButton, GUILayout.Width(installBtnWidth)))
                {
                    InstallAdaptersWithProgress(new List<AppLovinIntegrationBridge.AdapterInfo> { a }, "Installing");
                }
                GUI.enabled = !_isInstalling && a.IsInstalled && a.Network != null;
                if (_removeIcon != null)
                {
                    if (GUILayout.Button(new GUIContent(_removeIcon, "Remove"), iconBtnStyle))
                    {
                        AppLovinIntegrationBridge.RemoveAdapters(new List<AppLovinNetwork> { a.Network });
                        RefreshState();
                    }
                }
                else
                {
                    if (a.IsInstalled && GUILayout.Button("X", iconBtnStyle))
                    {
                        AppLovinIntegrationBridge.RemoveAdapters(new List<AppLovinNetwork> { a.Network });
                        RefreshState();
                    }
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        private void InstallAdaptersWithProgress(List<AppLovinIntegrationBridge.AdapterInfo> adapterInfos, string progressVerb)
        {
            if (_isInstalling)
                return;
            if (adapterInfos == null || adapterInfos.Count == 0)
                return;
            var networks = new List<AppLovinNetwork>();
            foreach (var info in adapterInfos)
            {
                if (info?.Network != null)
                    networks.Add(info.Network);
            }
            if (networks.Count == 0)
                return;
            _isInstalling = true;
            _installProgress = null;
            AppLovinIntegrationBridge.InstallAdapters(networks,
                (current, total) => { _installProgress = $"{progressVerb} {current}/{total}..."; Repaint(); },
                () => { _isInstalling = false; _installProgress = null; RefreshState(); SchedulePendingXmlPatchApply(PendingXmlPatchInitialDelaySeconds); RefreshState(); Repaint(); },
                () => { RefreshState(); Repaint(); });
        }
        private bool ShowDialogSafe(string dialogTitle, string message, string ok, string cancel = null, bool defaultResult = false)
        {
            if (_isDialogOpen)
            {
                Debug.LogWarning($"[ShionSDK] Skip overlapping dialog: {dialogTitle}");
                return defaultResult;
            }
            _isDialogOpen = true;
            try
            {
                return string.IsNullOrEmpty(cancel)
                    ? EditorUtility.DisplayDialog(dialogTitle, message, ok)
                    : EditorUtility.DisplayDialog(dialogTitle, message, ok, cancel);
            }
            finally
            {
                _isDialogOpen = false;
            }
        }
        private bool CanApplyPendingXmlPatchesNow()
        {
            if (_isInstalling)
                return false;
            if (AdMobAdapterInstaller.IsOperationInProgress)
                return false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;
            return true;
        }
        private void SchedulePendingXmlPatchApply(double delaySeconds)
        {
            if (_pendingXmlPatches.Count == 0)
                return;
            var executeAt = EditorApplication.timeSinceStartup + Math.Max(0.1d, delaySeconds);
            if (_isPendingXmlPatchApplyQueued)
            {
                if (executeAt > _pendingXmlPatchApplyAt)
                    _pendingXmlPatchApplyAt = executeAt;
                return;
            }
            _isPendingXmlPatchApplyQueued = true;
            _pendingXmlPatchApplyAt = executeAt;
            EditorApplication.update -= PendingXmlPatchApplyTick;
            EditorApplication.update += PendingXmlPatchApplyTick;
        }
        private void PendingXmlPatchApplyTick()
        {
            if (!_isPendingXmlPatchApplyQueued)
            {
                EditorApplication.update -= PendingXmlPatchApplyTick;
                return;
            }
            if (EditorApplication.timeSinceStartup < _pendingXmlPatchApplyAt)
                return;
            EditorApplication.update -= PendingXmlPatchApplyTick;
            _isPendingXmlPatchApplyQueued = false;
            if (_pendingXmlPatches.Count == 0)
                return;
            if (!CanApplyPendingXmlPatchesNow())
            {
                SchedulePendingXmlPatchApply(1.0d);
                return;
            }
            ApplyPendingXmlPatches();
        }
        private void ApplyPendingXmlPatches()
        {
            if (_isApplyingPendingXmlPatches)
                return;
            if (_pendingXmlPatches.Count == 0)
            {
                SavePendingXmlPatchState();
                return;
            }
            if (!CanApplyPendingXmlPatchesNow())
            {
                SchedulePendingXmlPatchApply(1.0d);
                return;
            }
            _isApplyingPendingXmlPatches = true;
            try
            {
                var hasRemaining = false;
                for (var idx = _pendingXmlPatches.Count - 1; idx >= 0; idx--)
                {
                    var patch = _pendingXmlPatches[idx];
                    if (patch == null || string.IsNullOrEmpty(patch.NetworkToken))
                    {
                        _pendingXmlPatches.RemoveAt(idx);
                        continue;
                    }
                    var target = new AppLovinIntegrationBridge.AdapterInfo
                    {
                        Name = patch.NetworkToken,
                        DisplayName = patch.NetworkToken
                    };
                    var xmlPath = FindAppLovinAdapterDependenciesPath(target);
                    if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                    {
                        patch.RetryCount++;
                        if (patch.RetryCount == 1 || patch.RetryCount % 10 == 0)
                            Debug.LogWarning($"[ShionSDK] Pending XML patch waiting (xml not found): {patch.NetworkToken}, retry:{patch.RetryCount}");
                        hasRemaining = true;
                        continue;
                    }
                    if (IsXmlAlreadyAtOrBelowTarget(xmlPath, patch.AndroidTarget, patch.IosTarget))
                    {
                        Debug.Log($"[ShionSDK] Pending XML patch already satisfied: {xmlPath}");
                        _pendingXmlPatches.RemoveAt(idx);
                        continue;
                    }
                    var updated = TryUpdateAppLovinXmlWithService(target, xmlPath, patch.AndroidTarget, patch.IosTarget) ||
                                  TryDowngradeAppLovinXml(xmlPath, patch.AndroidTarget, patch.IosTarget);
                    if (updated)
                    {
                        Debug.Log($"[ShionSDK] Applied pending XML patch: {xmlPath}");
                        _pendingXmlPatches.RemoveAt(idx);
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        patch.RetryCount++;
                        if (patch.RetryCount == 1 || patch.RetryCount % 10 == 0)
                            Debug.LogWarning($"[ShionSDK] Pending XML patch not applied yet: {xmlPath}, retry:{patch.RetryCount}");
                        hasRemaining = true;
                    }
                }
                if (hasRemaining)
                    SchedulePendingXmlPatchApply(1.0d);
            }
            finally
            {
                _isApplyingPendingXmlPatches = false;
                SavePendingXmlPatchState();
            }
        }
        private static bool IsXmlAlreadyAtOrBelowTarget(string xmlPath, string androidTarget, string iosTarget)
        {
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                return false;
            var content = File.ReadAllText(xmlPath);
            var androidCurrent = "";
            var iosCurrent = "";
            var mA = Regex.Match(content, @"com\.applovin\.mediation:[a-z0-9\-]+-adapter:\[?([\d.]+)\]?", RegexOptions.IgnoreCase);
            if (mA.Success)
                androidCurrent = VersionComparisonService.Normalize(mA.Groups[1].Value ?? "");
            var mI = Regex.Match(content, @"name\s*=\s*[""']AppLovinMediation[^""']*Adapter[""'][^>]*version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (!mI.Success)
                mI = Regex.Match(content, @"version\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""']AppLovinMediation[^""']*Adapter[""']", RegexOptions.IgnoreCase);
            if (mI.Success)
                iosCurrent = VersionComparisonService.Normalize(mI.Groups[1].Value ?? "");
            var androidOk = string.IsNullOrEmpty(androidTarget) || string.IsNullOrEmpty(androidCurrent) || VersionComparisonService.Compare(androidCurrent, androidTarget) <= 0;
            var iosOk = string.IsNullOrEmpty(iosTarget) || string.IsNullOrEmpty(iosCurrent) || VersionComparisonService.Compare(iosCurrent, iosTarget) <= 0;
            return androidOk && iosOk;
        }
        private static string FindAppLovinAdapterDependenciesPath(AppLovinIntegrationBridge.AdapterInfo target)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return null;
            var token = NormalizeNetworkToken(target?.Name ?? target?.DisplayName);
            if (string.IsNullOrEmpty(token))
                return null;
            var mediationDir = Path.Combine(projectRoot, "Assets", "MaxSdk", "Mediation");
            if (!Directory.Exists(mediationDir))
                return null;
            foreach (var networkDir in Directory.GetDirectories(mediationDir))
            {
                var editorDir = Path.Combine(networkDir, "Editor");
                if (!Directory.Exists(editorDir))
                    continue;
                var xmlFiles = Directory.GetFiles(editorDir, "*.xml", SearchOption.TopDirectoryOnly);
                foreach (var xml in xmlFiles)
                {
                    var content = File.ReadAllText(xml);
                    var m = Regex.Match(content, @"com\.applovin\.mediation:([a-z0-9\-]+)-adapter:\[?[\d.]+\]?", RegexOptions.IgnoreCase);
                    if (!m.Success)
                        continue;
                    var id = NormalizeNetworkToken(m.Groups[1].Value);
                    if (string.Equals(id, token, StringComparison.OrdinalIgnoreCase) || id.Contains(token) || token.Contains(id))
                        return xml;
                }
            }
            return null;
        }
        private static bool TryDowngradeAppLovinXml(string filePath, string androidTarget, string iosTarget)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
            var content = File.ReadAllText(filePath);
            var updated = content;
            if (!string.IsNullOrEmpty(androidTarget))
            {
                var androidPattern = @"(com\.applovin\.mediation:[a-z0-9\-]+-adapter:\[?)([\d.]+)(\]?)";
                var m = Regex.Match(updated, androidPattern, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var current = VersionComparisonService.Normalize(m.Groups[2].Value ?? "");
                    if (VersionComparisonService.Compare(androidTarget, current) > 0)
                        return false;
                    updated = Regex.Replace(updated, androidPattern, "${1}" + androidTarget + "${3}", RegexOptions.IgnoreCase);
                }
            }
            if (!string.IsNullOrEmpty(iosTarget))
            {
                var podPattern = @"(name\s*=\s*[""']AppLovinMediation[^""']*Adapter[""'][^>]*version\s*=\s*[""'])([^""']+)([""'])";
                var m = Regex.Match(updated, podPattern, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var current = VersionComparisonService.Normalize(m.Groups[2].Value ?? "");
                    if (VersionComparisonService.Compare(iosTarget, current) > 0)
                        return false;
                    updated = Regex.Replace(updated, podPattern, "${1}" + iosTarget + "${3}", RegexOptions.IgnoreCase);
                }
            }
            if (updated == content)
                return false;
            File.WriteAllText(filePath, updated);
            return true;
        }
        private static bool TryUpdateAppLovinXmlWithService(
            AppLovinIntegrationBridge.AdapterInfo target,
            string xmlPath,
            string androidTarget,
            string iosTarget)
        {
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                return false;
            var targetToken = NormalizeNetworkToken(target?.Name ?? target?.DisplayName);
            DiscoveredAdapterInfo matched = null;
            foreach (var info in MediationCompatibilityService.DiscoverAllAppLovinAdapters())
            {
                if (info == null)
                    continue;
                var infoToken = NormalizeNetworkToken(info.NetworkId);
                if (string.Equals(infoToken, targetToken, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((info.FilePath ?? "").Replace("\\", "/"), xmlPath.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase))
                {
                    matched = info;
                    break;
                }
            }
            if (matched == null)
                return false;
            return MediationCompatibilityService.UpdateToMatchVersion(
                matched,
                xmlPath,
                androidTarget,
                iosTarget);
        }
        [Serializable]
        private class PendingXmlPatch
        {
            public string NetworkToken;
            public string AndroidTarget;
            public string IosTarget;
            public int RetryCount;
        }
        [Serializable]
        private class PendingXmlPatchState
        {
            public List<PendingXmlPatch> Items = new List<PendingXmlPatch>();
        }
        private void SavePendingXmlPatchState()
        {
            var state = new PendingXmlPatchState();
            foreach (var patch in _pendingXmlPatches)
            {
                if (patch == null || string.IsNullOrEmpty(patch.NetworkToken))
                    continue;
                state.Items.Add(new PendingXmlPatch
                {
                    NetworkToken = patch.NetworkToken,
                    AndroidTarget = patch.AndroidTarget,
                    IosTarget = patch.IosTarget,
                    RetryCount = patch.RetryCount
                });
            }
            var json = JsonUtility.ToJson(state);
            SessionState.SetString(PendingXmlPatchesSessionKey, json);
        }
        private void RestorePendingXmlPatchState()
        {
            _pendingXmlPatches.Clear();
            var json = SessionState.GetString(PendingXmlPatchesSessionKey, "");
            if (string.IsNullOrEmpty(json))
                return;
            PendingXmlPatchState state = null;
            try
            {
                state = JsonUtility.FromJson<PendingXmlPatchState>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShionSDK] Failed to restore pending XML patch state: {ex.Message}");
            }
            if (state?.Items == null || state.Items.Count == 0)
                return;
            foreach (var patch in state.Items)
            {
                if (patch == null || string.IsNullOrEmpty(patch.NetworkToken))
                    continue;
                _pendingXmlPatches.Add(patch);
            }
        }
        private static string NormalizeNetworkToken(string value)
        {
            var s = (value ?? "").Trim().ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
            if (s.EndsWith("sdk", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 3);
            if (s.EndsWith("adapter", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - "adapter".Length);
            if (s.EndsWith("network", StringComparison.OrdinalIgnoreCase) && s != "metaaudiencenetwork")
                s = s.Substring(0, s.Length - "network".Length);
            if (s.EndsWith("mediate", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - "mediate".Length);
            if (s == "metaaudience" || s == "metaaudiencenetwork")
                s = "meta";
            else if (s == "facebook")
                s = "meta";
            else if (s == "google")
                s = "admob";
            else if (s == "unity")
                s = "unityads";
            else if (s == "fyber")
                s = "dtexchange";
            else if (s == "vungle")
                s = "liftoffmonetize";
            return s;
        }
    }
}
#endif