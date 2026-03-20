using System.Linq;
using System.Collections.Generic;
using Shion.SDK.Core;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class CompanySDKWindow : EditorWindow
    {
        private static bool _sessionHasFetchedGitVersions;
        private CompanySDKViewModel _viewModel;
        private CompanySDKPresenter _presenter;
        [MenuItem("Shion/SDK Manager")]
        public static void Open()
        {
            GetWindow<CompanySDKWindow>("Modules SDK");
        }
        private void OnEnable()
        {
            _viewModel = new CompanySDKViewModel();
            var savedTab = EditorPrefs.GetInt(ShionSDKConstants.EditorPrefsKeys.SelectedTab, (int)ModuleCategory.Sdks);
            _viewModel.SelectedTab = savedTab >= 0 && savedTab <= 2
                ? (ModuleCategory)savedTab
                : ModuleCategory.Sdks;
            _presenter = new CompanySDKPresenter(_viewModel, Repaint);
            _presenter.OnEnable();
            _presenter.TryLoadVersionCacheFromEditorPrefs();
            if (_viewModel.GitInstallableVersionsByModuleId.Count > 0)
            {
                _viewModel.GitVersionsScanned = true;
            }
            else if (!_sessionHasFetchedGitVersions)
            {
                _sessionHasFetchedGitVersions = true;
                _viewModel.GitScanInProgress = true;
                var modulesSnapshot = _presenter.Repository.GetAll().ToList();
                EditorApplication.delayCall += () =>
                {
                    _presenter.EnsureGitInstallableVersionsLoaded(modulesSnapshot, () =>
                    {
                        _presenter.SaveVersionCacheToEditorPrefs();
                        _viewModel.GitScanInProgress = false;
                        Repaint();
                    });
                };
            }
            else
            {
                _viewModel.GitVersionsScanned = true;
            }
        }
        private void OnGUI()
        {
            _presenter.EnsureStyles();
            if (!_viewModel.GitVersionsScanned || _viewModel.GitScanInProgress)
            {
                GUILayout.FlexibleSpace();
                var loadingStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.LabelField(ShionSDKConstants.Messages.LoadingVersions, loadingStyle, GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                return;
            }
            if (!string.IsNullOrEmpty(_viewModel.WarningMessage) && EditorApplication.timeSinceStartup <= _viewModel.WarningExpireTime)
                EditorGUILayout.HelpBox(_viewModel.WarningMessage, MessageType.Warning);
            else
                _viewModel.WarningMessage = null;
            if (_viewModel.PendingBatchUninstall != null && _viewModel.PendingBatchUninstall.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Batch Uninstall", GUILayout.Height(24)))
                    ShowBatchUninstallDialog();
                if (GUILayout.Button("Cancel Batch", GUILayout.Height(24)))
                    _presenter.OnCancelBatch();
                EditorGUILayout.EndHorizontal();
            }
            var modules = _presenter.Repository.GetAll().ToList();
            _presenter.RefreshInstalledStateCache(modules);
            _presenter.TickQueues();
            var tabNames = new[] { "SDKs", "Frequently Used", "Other" };
            var tabCategories = new[] { ModuleCategory.Sdks, ModuleCategory.FrequentlyUsed, ModuleCategory.Other };
            int tabIndex = System.Array.IndexOf(tabCategories, _viewModel.SelectedTab);
            if (tabIndex < 0) tabIndex = 0;
            int newTab = GUILayout.Toolbar(tabIndex, tabNames);
            if (newTab != tabIndex)
            {
                _viewModel.SelectedTab = tabCategories[newTab];
                EditorPrefs.SetInt(ShionSDKConstants.EditorPrefsKeys.SelectedTab, (int)_viewModel.SelectedTab);
            }
            var filteredModules = modules.Where(m => m.Category == _viewModel.SelectedTab).ToList();
            _viewModel.Scroll = EditorGUILayout.BeginScrollView(_viewModel.Scroll);
            foreach (var module in filteredModules)
                DrawModule(module);
            EditorGUILayout.EndScrollView();
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Modules", _viewModel.RefreshButtonStyle, GUILayout.ExpandWidth(true)))
                OnRefreshClicked();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        private void DrawModule(Module module)
        {
            var vm = _viewModel;
            bool installed = !string.IsNullOrEmpty(module.UpmId)
                ? vm.CachedUpmInstalledIds.Contains(module.UpmId)
                : vm.CachedRegistryIds.Contains(module.Id.Value);
            var id = module.Id.Value;
            if (!vm.OperationStatus.TryGetValue(id, out var opStatus))
                opStatus = ModuleOperationStatus.None;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(module.Name, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            bool hasUpm = !string.IsNullOrEmpty(module.UpmId);
            bool hasGit = !string.IsNullOrEmpty(module.GitUrl);
            if (hasUpm && hasGit)
            {
                var method = ModuleInstallMethodStore.Get(module.Id);
                string methodLabel = method switch
                {
                    ModuleInstallMethod.Upm => "Method: UPM (Package Manager)",
                    ModuleInstallMethod.Git => "Method: Git (clone to Assets)",
                    _ => "Method: Auto (prefer UPM)"
                };
                var methodStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fixedHeight = 20
                };
                if (GUILayout.Button(CompanySDKPresenter.TruncateWithEllipsis(methodLabel, 28),
                        methodStyle, GUILayout.Width(200)))
                {
                    ModuleInstallMethod next = method switch
                    {
                        ModuleInstallMethod.Auto => ModuleInstallMethod.Upm,
                        ModuleInstallMethod.Upm => ModuleInstallMethod.Git,
                        ModuleInstallMethod.Git => ModuleInstallMethod.Auto,
                        _ => ModuleInstallMethod.Auto
                    };
                    ModuleInstallMethodStore.Set(module.Id, next);
                }
            }
            var currentVersionCore = _presenter.GetModuleVersionForDisplay(module, installed, opStatus);
            var shortLabel = CompanySDKPresenter.TruncateWithEllipsis(currentVersionCore, (int)ShionSDKConstants.VersionLabelTruncateChars);
            var gitUrl = CompanySDKPresenter.GetGitHubUrlForModule(module);
            bool hasGitHub = !string.IsNullOrEmpty(gitUrl);
            bool versionSelectable = hasGitHub && !installed &&
                opStatus != ModuleOperationStatus.Waiting &&
                opStatus != ModuleOperationStatus.Installing &&
                opStatus != ModuleOperationStatus.Uninstalling;
            const float RightColumnWidth = ShionSDKConstants.RightColumnWidth;
            var versionStyle = new GUIStyle(EditorStyles.popup) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fixedHeight = 20 };
            if (hasGitHub)
            {
                var rect = GUILayoutUtility.GetRect(new GUIContent(shortLabel), versionStyle, GUILayout.Width(RightColumnWidth), GUILayout.ExpandWidth(false));
                if (!versionSelectable)
                {
                    var readOnlyStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
                    GUI.Label(rect, shortLabel, readOnlyStyle);
                }
                else if (GUI.Button(rect, shortLabel, versionStyle))
                {
                    _presenter.ShowVersionSelectionPopup(module, currentVersionCore, rect);
                }
            }
            else
            {
                var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.LabelField(shortLabel, labelStyle, GUILayout.Width(RightColumnWidth), GUILayout.ExpandWidth(false));
            }
            GUILayout.Space(12f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("State:", GUILayout.Width(35));
            var stateStyle = vm.StateStyles.TryGetValue(module.State, out var s) ? s : EditorStyles.label;
            EditorGUILayout.LabelField(module.State.ToString(), stateStyle, GUILayout.ExpandWidth(true));
            GUIStyle statusStyle;
            string statusText;
            switch (opStatus)
            {
                case ModuleOperationStatus.Waiting: statusStyle = vm.StatusWaitingStyle; statusText = "Waiting"; break;
                case ModuleOperationStatus.Installing: statusStyle = vm.StatusInstallingStyle; statusText = "Installing"; break;
                case ModuleOperationStatus.Uninstalling: statusStyle = vm.StatusUninstallingStyle; statusText = "Uninstalling"; break;
                default:
                    statusStyle = installed ? vm.StatusInstalledStyle : vm.StatusUninstalledStyle;
                    statusText = installed ? "Installed" : "Uninstalled";
                    break;
            }
            EditorGUILayout.LabelField(statusText, statusStyle, GUILayout.Width(RightColumnWidth), GUILayout.ExpandWidth(false));
            GUILayout.Space(ShionSDKConstants.SpacingAfterColumn);
            EditorGUILayout.EndHorizontal();
            bool hasDeps = module.Dependencies != null && module.Dependencies.Count > 0;
            if (!vm.DependenciesFoldout.ContainsKey(id))
                vm.DependenciesFoldout[id] = hasDeps;
            vm.DependenciesFoldout[id] = EditorGUILayout.Foldout(
                vm.DependenciesFoldout[id],
                hasDeps ? $"Dependencies: {module.Dependencies.Count}" : "Dependencies: 0",
                true);
            if (vm.DependenciesFoldout[id] && hasDeps)
            {
                var rootVersion = _presenter.GetModuleVersionForDisplay(module, installed, opStatus);
                EditorGUI.indentLevel++;
                foreach (var dep in module.Dependencies)
                {
                    var depModule = _presenter.Repository.Get(dep.Id);
                    string depName = depModule?.Name ?? dep.Id.Value;
                    bool depInstalled = depModule != null && (
                        !string.IsNullOrEmpty(depModule.UpmId)
                            ? vm.CachedUpmInstalledIds.Contains(depModule.UpmId)
                            : vm.CachedRegistryIds.Contains(depModule.Id.Value));
                    var depVersionLabel = _presenter.GetDepVersionLabel(module, dep, depInstalled, installed, rootVersion);
                    bool depVersionSelectable = _presenter.IsDepVersionSelectable(module, dep, depInstalled, installed, rootVersion, opStatus);
                    var depNameStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontStyle = depInstalled ? FontStyle.Normal : FontStyle.Italic,
                        normal = { textColor = depInstalled ? Color.white : Color.gray }
                    };
                    var depVersionStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = depInstalled ? FontStyle.Normal : FontStyle.Italic,
                        normal = { textColor = depInstalled ? Color.white : Color.gray }
                    };
                    var depVersionButtonStyle = depVersionSelectable
                        ? new GUIStyle(EditorStyles.popup) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fixedHeight = 18 }
                        : depVersionStyle;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(depName, depNameStyle, GUILayout.ExpandWidth(true));
                    var shortDepLabel = CompanySDKPresenter.TruncateWithEllipsis(depVersionLabel, (int)ShionSDKConstants.VersionLabelTruncateChars);
                    if (depVersionSelectable)
                    {
                        var depRect = GUILayoutUtility.GetRect(new GUIContent(shortDepLabel), depVersionButtonStyle, GUILayout.Width(RightColumnWidth), GUILayout.ExpandWidth(false));
                        if (GUI.Button(depRect, shortDepLabel, depVersionButtonStyle))
                            _presenter.ShowDepVersionSelectionPopup(module, rootVersion, dep, depVersionLabel, depRect);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(depVersionLabel, depVersionStyle, GUILayout.Width(RightColumnWidth), GUILayout.ExpandWidth(false));
                    }
                    GUILayout.Space(ShionSDKConstants.SpacingAfterColumn);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.BeginHorizontal();
            bool isBusy = opStatus == ModuleOperationStatus.Installing || opStatus == ModuleOperationStatus.Uninstalling || opStatus == ModuleOperationStatus.Waiting;
            EditorGUI.BeginDisabledGroup(isBusy);
            if (!installed)
            {
                if (GUILayout.Button("Install", GUILayout.Width(100)))
                    _presenter.OnInstallClicked(module);
            }
            else
            {
                if (GUILayout.Button("Uninstall", GUILayout.Width(100)))
                    _presenter.OnUninstallClicked(module);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        private void OnRefreshClicked()
        {
            _presenter.OnRefresh();
            _viewModel.GitScanInProgress = true;
            var modules = _presenter.Repository.GetAll().ToList();
            _presenter.EnsureGitInstallableVersionsLoaded(modules, () =>
            {
                _presenter.SaveVersionCacheToEditorPrefs();
                _viewModel.GitScanInProgress = false;
                Repaint();
            });
        }
        private void ShowBatchUninstallDialog()
        {
            if (_viewModel.PendingBatchUninstall == null || _viewModel.PendingBatchUninstall.Count == 0) return;
            var names = string.Join("\n- ", _viewModel.PendingBatchUninstall.Select(m => m.Name));
            var message = "Are you sure you want to batch uninstall these modules?\n\n- " + names + "\n\nAll of the above modules will be uninstalled.";
            var accept = EditorUtility.DisplayDialog("Batch Uninstall", message, "Accept", "Deny");
            if (accept)
                _presenter.OnBatchUninstallAccept();
        }
    }
}