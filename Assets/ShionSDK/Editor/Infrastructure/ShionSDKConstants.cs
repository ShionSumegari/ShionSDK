using UnityEngine;
namespace Shion.SDK.Editor
{
    internal static class ShionSDKConstants
    {
        public const string LogPrefix = "[ShionSDK]";
        public static class Paths
        {
            public const string LockFile = "Assets/ShionSDK/TextAsset/shion-lock.json";
            public const string VersionCompatibilityFile = "Assets/ShionSDK/TextAsset/version-compatibility.json";
            public static string VersionCompatibilityFullPath => System.IO.Path.Combine(Application.dataPath, "ShionSDK", "TextAsset", "version-compatibility.json");
        }
        public const float RightColumnWidth = 100f;
        public const float VersionLabelTruncateChars = 12;
        public const float SpacingAfterColumn = 12f;
        public const double WarningDisplaySeconds = 10.0;
        public const string VersionPlaceholder = "0.0.0";
        public const int MaxReleasesPerModule = 50;
        public const int MaxReleasesForPopup = 100;
        public static class EditorPrefsKeys
        {
            public const string GitHubToken = "ShionSDK.GitHubToken";
            public const string SelectedTab = "ShionSDK.SelectedTab";
            public const string VersionCachePrefix = "ShionSDK.VersionCache.";
            public const string VersionCacheModules = VersionCachePrefix + "Modules";
            public const string InstallVersionPrefix = "ShionSDK.InstallVersion.";
            public const string InstallMethodPrefix = "ShionSDK.InstallMethod.";
            public const string AdMobAdapterPrefix = "ShionSDK.AdMobAdapter.";
            public const string PendingAdMobQueue = "ShionSDK.AdSDK.PendingAdMobQueue";
            public const string PendingAdMobIndex = "ShionSDK.AdSDK.PendingAdMobIndex";
        }
        public static class ModuleIds
        {
            public const string GoogleAdsMobile = "googleadsmobile";
            public const string AppLovinSdk = "applovinsdk";
        }
        public static class Window
        {
            public const float MinAdSDKWidth = 1500f;
            public const float InitialAdSDKWidth = 1600f;
            public const float InitialAdSDKHeight = 600f;
            public const float MinAdSDKHeight = 420f;
        }
        public static class Messages
        {
            public const string LoadingVersions = "Checking versions from GitHub...";
        }
    }
}