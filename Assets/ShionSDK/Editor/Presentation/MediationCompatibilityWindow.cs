using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class MediationCompatibilityWindow : EditorWindow
    {
        private List<MediationCompatibilityService.MediationMismatchResult> _mismatches = new List<MediationCompatibilityService.MediationMismatchResult>();
        private Vector2 _scroll;
        private bool _fixing;
        private System.Action _onClose;

        public static void Open(System.Action onRefresh = null)
        {
            var w = GetWindow<MediationCompatibilityWindow>(true, "Mediation Compatibility", true);
            w.minSize = new Vector2(520, 320);
            w._onClose = onRefresh;
            w.RefreshMismatches();
            AdSDKWindow.SetCachedCompatibilityMismatchCount(w._mismatches.Count);
        }

        private void RefreshMismatches(bool runPrefetch = true)
        {
            _mismatches.Clear();
            try
            {
                if (runPrefetch)
                {
                    MediationCompatibilityService.PrefetchAllInstalledAdapters(() =>
                    {
                        ScanAndDisplay();
                    });
                }
                else
                {
                    ScanAndDisplay();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShionSDK] Scan compatibility failed: {ex.Message}");
            }
        }
        private void ScanAndDisplay()
        {
            try
            {
                var list = MediationCompatibilityService.ScanAllMismatches();
                if (list != null)
                    _mismatches.AddRange(list);
                AdSDKWindow.SetCachedCompatibilityMismatchCount(_mismatches.Count);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShionSDK] Scan compatibility failed: {ex.Message}");
            }
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Mediation Version Compatibility", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Phát hiện version không khớp giữa adapter AdMob vs AppLovin (cùng network).\n\n" +
                "• Network: Tên ad network (Chartboost, Meta, ...)\n" +
                "• Current: Version đang cài (Android / iOS) từ file cần sửa\n" +
                "• Target: Version nên dùng (từ Support hoặc thấp hơn)\n" +
                "• Support: Version khuyến nghị từ AdMob changelog (nếu có)\n\n" +
                "Fix All: Cập nhật Dependencies.xml theo Target.",
                MessageType.Info);
            GUILayout.Space(4);

            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                RefreshMismatches();

            GUILayout.Space(4);

            if (_mismatches.Count == 0)
            {
                EditorGUILayout.HelpBox("No compatibility issues found.", MessageType.Info);
                if (GUILayout.Button("Close", GUILayout.Width(60)))
                    Close();
                return;
            }

            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Network", headerStyle, GUILayout.Width(90));
            EditorGUILayout.LabelField("Loại", headerStyle, GUILayout.Width(120));
            EditorGUILayout.LabelField("Đang cài (A/I)", headerStyle, GUILayout.Width(140));
            EditorGUILayout.LabelField("Mục tiêu (A/I)", headerStyle, GUILayout.Width(140));
            EditorGUILayout.LabelField("Hỗ trợ (A/I)", headerStyle, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var m in _mismatches)
            {
                if (m == null) continue;
                var kindStr = m.Kind == MediationCompatibilityService.MismatchKind.AdapterVsPlugin ? "Adapter vs SDK" : "AdMob vs AppLovin (cùng network)";
                var srcA = m.Adapter?.AndroidVersion ?? "_";
                var srcI = m.Adapter?.IosVersion ?? "_";
                var curStr = $"{srcA} / {srcI}";
                var targetStr = $"{m.TargetAndroid ?? "_"} / {m.TargetIos ?? "_"}";
                var supportStr = (!string.IsNullOrEmpty(m.SupportAndroid) || !string.IsNullOrEmpty(m.SupportIos))
                    ? $"{(m.SupportAndroid ?? "-")} / {(m.SupportIos ?? "-")}"
                    : "-";
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField(m.DisplayName ?? "-", GUILayout.Width(90));
                EditorGUILayout.LabelField(kindStr, GUILayout.Width(120));
                EditorGUILayout.LabelField(curStr, GUILayout.Width(140));
                EditorGUILayout.LabelField(targetStr, GUILayout.Width(140));
                EditorGUILayout.LabelField(supportStr, GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(_fixing);
            if (GUILayout.Button("Fix All", GUILayout.Height(28), GUILayout.Width(80)))
                FixAll();
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Close", GUILayout.Height(28), GUILayout.Width(60)))
                Close();
            EditorGUILayout.EndHorizontal();
        }

        private void FixAll()
        {
            if (_fixing || _mismatches.Count == 0) return;
            _fixing = true;
            try
            {
                var fixedCount = 0;
                foreach (var m in _mismatches)
                {
                    if (m == null || string.IsNullOrEmpty(m.FileToUpdate) || !File.Exists(m.FileToUpdate))
                        continue;
                    var ok = false;
                    if (m.AdapterInfo != null)
                        ok = MediationCompatibilityService.UpdateToMatchVersion(m.AdapterInfo, m.FileToUpdate, m.TargetAndroid ?? "", m.TargetIos ?? "");
                    else if (m.Config != null)
                        ok = MediationCompatibilityService.UpdateToMatchVersion(m.Config, m.FileToUpdate, m.TargetAndroid ?? "", m.TargetIos ?? "");
                    if (ok)
                    {
                        fixedCount++;
                        AssetDatabase.Refresh();
                    }
                }
                if (fixedCount > 0)
                {
                    RefreshMismatches();
                    AdSDKWindow.SetCachedCompatibilityMismatchCount(_mismatches.Count);
                    _onClose?.Invoke();
                }
            }
            finally
            {
                _fixing = false;
                Repaint();
            }
        }
    }
}
