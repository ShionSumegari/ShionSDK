using UnityEditor;
using UnityEngine;

namespace Shion.SDK.Editor
{
    [CustomEditor(typeof(MediationCompatibilityConfig))]
    public class MediationCompatibilityConfigInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var config = (MediationCompatibilityConfig)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Config này dùng để map adapter AdMob ↔ AppLovin và pattern parse XML.\n\n" +
                "• Network Mappings: Ghép cặp 2 adapter cùng network (vd: meta↔facebook, chartboost↔chartboost)\n" +
                "• AdMob Parse Patterns: Pattern đọc version từ Dependencies.xml khi tên artifact khác chuẩn",
                MessageType.Info);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("1. Network Mappings (AdMob ↔ AppLovin)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Bảng map: AdMob key (slug/folder) → AppLovin NetworkId. Dùng để nhận biết 2 adapter là cùng 1 network.", MessageType.None);
            DrawMappingTable();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("NetworkMappings"), true);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("2. AdMob Parse Patterns", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Khi mặc định (com.google.ads.mediation:{slug}) không parse được, thêm entry ở đây. VD: Meta dùng 'facebook' thay vì 'metaaudiencenetwork'.", MessageType.None);
            DrawParsePatternTable();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AdMobParsePatterns"), true);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMappingTable()
        {
            EditorGUILayout.BeginVertical("box");
            var header = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AdMob Key", header, GUILayout.Width(120));
            EditorGUILayout.LabelField("→", header, GUILayout.Width(20));
            EditorGUILayout.LabelField("AppLovin NetworkId", header, GUILayout.Width(120));
            EditorGUILayout.LabelField("Ý nghĩa", header);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("meta", GUILayout.Width(120));
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            EditorGUILayout.LabelField("facebook", GUILayout.Width(120));
            EditorGUILayout.LabelField("Meta AdMob ↔ Facebook AppLovin", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("chartboost", GUILayout.Width(120));
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            EditorGUILayout.LabelField("chartboost", GUILayout.Width(120));
            EditorGUILayout.LabelField("Tên trùng", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("liftoffmonetize", GUILayout.Width(120));
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            EditorGUILayout.LabelField("vungle", GUILayout.Width(120));
            EditorGUILayout.LabelField("Liftoff AdMob ↔ Vungle AppLovin", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawParsePatternTable()
        {
            EditorGUILayout.BeginVertical("box");
            var header = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Key", header, GUILayout.Width(100));
            EditorGUILayout.LabelField("Android Pattern", header, GUILayout.Width(200));
            EditorGUILayout.LabelField("iOS Pod", header);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("meta", GUILayout.Width(100));
            EditorGUILayout.LabelField("com.google.ads.mediation:facebook", EditorStyles.miniLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField("GoogleMobileAdsMediationFacebook", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }
}
