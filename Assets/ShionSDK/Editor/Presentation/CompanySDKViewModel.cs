using System.Collections.Generic;
using Shion.SDK.Core;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    internal class CompanySDKViewModel
    {
        public ModuleCategory SelectedTab = ModuleCategory.Sdks;
        public string WarningMessage;
        public double WarningExpireTime;
        public List<Module> PendingBatchUninstall;
        public readonly Dictionary<string, ModuleOperationStatus> OperationStatus = new();
        public HashSet<string> CachedUpmInstalledIds;
        public HashSet<string> CachedRegistryIds;
        public Dictionary<ModuleState, GUIStyle> StateStyles;
        public GUIStyle StatusInstallingStyle;
        public GUIStyle StatusUninstallingStyle;
        public GUIStyle StatusWaitingStyle;
        public GUIStyle StatusInstalledStyle;
        public GUIStyle StatusUninstalledStyle;
        public GUIStyle RefreshButtonStyle;
        public Vector2 Scroll;
        public readonly Dictionary<string, string> SelectedVersionOverrides = new();
        public readonly Dictionary<string, bool> DependenciesFoldout = new();
        public bool GitVersionsScanned;
        public bool GitScanInProgress;
        public readonly Dictionary<string, List<string>> GitInstallableVersionsByModuleId = new();
    }
}