using UnityEditor;
namespace Shion.SDK.Editor
{
    public static class AdapterInstallDialogService
    {
        public static void ShowVersionsLoading()
        {
            EditorUtility.DisplayDialog("Versions Loading",
                "Adapter versions are still loading. Please wait a moment or click Refresh to retry.", "OK");
        }
        public static void ShowInstallFailed(string message)
        {
            EditorUtility.DisplayDialog("Install Failed", string.IsNullOrEmpty(message) ? "Install failed." : message, "OK");
        }
        public static bool ConfirmRemoveAdapter(string displayName)
        {
            return EditorUtility.DisplayDialog("Remove Adapter", $"Remove {displayName}?", "Remove", "Cancel");
        }
        public static void ShowOperationInProgress(string message = null)
        {
            EditorUtility.DisplayDialog("Operation In Progress",
                message ?? "Another AdMob adapter operation is running. Please wait for it to complete.", "OK");
        }
        public static void ShowUninstallFailed(string message)
        {
            EditorUtility.DisplayDialog("Uninstall Failed", string.IsNullOrEmpty(message) ? "Uninstall failed." : message, "OK");
        }
    }
}