using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class ShionSDKSettingsWindow : EditorWindow
    {
        private const string PrefsKeyToken = "ShionSDK.GitHubToken";
        private string _tokenField = "";
        private bool _tokenVisible;
        [MenuItem("Shion/SDK Settings")]
        public static void OpenFromMenu()
        {
            Open();
        }
        public static void Open()
        {
            var w = GetWindow<ShionSDKSettingsWindow>(true, "Shion SDK Settings", true);
            w.minSize = new Vector2(360, 120);
        }
        private void OnEnable()
        {
            _tokenField = EditorPrefs.GetString(PrefsKeyToken, "");
        }
        private void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("GitHub Personal Access Token", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Optional. Set a token to avoid 403 / rate limit when fetching release versions. Leave empty to use unauthenticated requests.",
                MessageType.Info);
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            _tokenField = _tokenVisible
                ? EditorGUILayout.TextField("Token", _tokenField)
                : EditorGUILayout.PasswordField("Token", _tokenField);
            if (GUILayout.Button(_tokenVisible ? "Hide" : "Show", GUILayout.Width(50)))
                _tokenVisible = !_tokenVisible;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Height(24)))
            {
                EditorPrefs.SetString(PrefsKeyToken, _tokenField ?? "");
                if (string.IsNullOrEmpty(_tokenField))
                    Debug.Log("[ShionSDK] GitHub token cleared.");
                else
                    Debug.Log("[ShionSDK] GitHub token saved.");
                Close();
            }
            if (GUILayout.Button("Clear", GUILayout.Height(24)))
            {
                _tokenField = "";
                EditorPrefs.DeleteKey(PrefsKeyToken);
                Debug.Log("[ShionSDK] GitHub token cleared.");
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(24)))
                Close();
            EditorGUILayout.EndHorizontal();
        }
    }
}