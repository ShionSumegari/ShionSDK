using UnityEditor;
namespace Shion.SDK.Editor
{
    public static class ShionSDKEditorSettings
    {
        private const string KeyMinUninstallTicksVisible = "ShionSDK.MinUninstallTicksVisible";
        private const int DefaultMinUninstallTicksVisible = 5;
        public static int MinUninstallTicksVisible
        {
            get => EditorPrefs.GetInt(KeyMinUninstallTicksVisible, DefaultMinUninstallTicksVisible);
            set => EditorPrefs.SetInt(KeyMinUninstallTicksVisible, value < 1 ? 1 : value);
        }
    }
}