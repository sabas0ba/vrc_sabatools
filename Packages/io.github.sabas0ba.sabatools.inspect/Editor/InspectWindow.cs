// The window. Presentation only: the scan lives in SceneScan and is
// non-destructive, so nothing here records undo or marks a scene dirty.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaTools.Inspect.Editors
{
    public sealed class InspectWindow : EditorWindow
    {
        private GameObject _target;
        private InspectMode _mode = InspectMode.Auto;
        private Vector2 _scroll;
        private ScanResult _result;

        private bool _showStats = true;
        private bool _showTextures;
        private bool _showFindings = true;

        public static InspectWindow Open()
        {
            InspectWindow window = GetWindow<InspectWindow>(false, "SabaTools Inspect", true);
            window.minSize = new Vector2(360f, 320f);
            window.Show();
            return window;
        }

        internal void ScanTarget(GameObject target)
        {
            _target = target;
            RunScan(new[] { target }, target.name);
        }

        private void OnGUI()
        {
            _target = (GameObject)EditorGUILayout.ObjectField(
                "Target", _target, typeof(GameObject), true);
            _mode = (InspectMode)EditorGUILayout.EnumPopup("Mode", _mode);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_target == null))
                {
                    if (GUILayout.Button("Scan Target"))
                    {
                        RunScan(new[] { _target }, _target.name);
                    }
                }
                if (GUILayout.Button("Scan Active Scene"))
                {
                    Scene scene = SceneManager.GetActiveScene();
                    RunScan(scene.GetRootGameObjects(), scene.name);
                }
            }

            if (_result == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a target (an avatar or world root) or scan the active scene. " +
                    "Scanning is read-only: nothing in the scene is modified.",
                    MessageType.Info);
                return;
            }

            InspectionReport report = _result.Report;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                report.TargetName + " — " + report.Mode + " — " +
                report.ErrorCount + " error(s), " + report.WarningCount + " warning(s)",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Markdown"))
                {
                    EditorGUIUtility.systemCopyBuffer = report.ToMarkdown();
                }
                if (GUILayout.Button("Export Markdown..."))
                {
                    ExportMarkdown(report);
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawStats(report);
            DrawTextures();
            DrawFindings(report);
            EditorGUILayout.EndScrollView();
        }

        private void RunScan(GameObject[] roots, string targetName)
        {
            // The only clock reading in the package. SceneScan takes the
            // timestamp as text so its own output stays deterministic.
            string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            _result = SceneScan.Run(roots, targetName, _mode, generatedAt);
        }

        private void DrawStats(InspectionReport report)
        {
            _showStats = EditorGUILayout.Foldout(_showStats, "Statistics");
            if (!_showStats)
            {
                return;
            }
            string group = null;
            foreach (StatRow row in report.Rows)
            {
                if (row.Group != group)
                {
                    group = row.Group;
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                }
                EditorGUILayout.LabelField(row.Label, row.Value);
            }
        }

        private void DrawTextures()
        {
            if (_result.Textures == null || _result.Textures.Count == 0)
            {
                return;
            }
            EditorGUILayout.Space();
            _showTextures = EditorGUILayout.Foldout(
                _showTextures, "Textures (" + _result.Textures.Count + ")");
            if (!_showTextures)
            {
                return;
            }
            foreach (TextureEntry entry in _result.Textures)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        entry.Name + " (" + entry.Detail + ")",
                        TextureMemoryEstimate.FormatBytes(entry.Bytes));
                    if (entry.Texture != null && GUILayout.Button("Ping", GUILayout.Width(48f)))
                    {
                        EditorGUIUtility.PingObject(entry.Texture);
                    }
                }
            }
        }

        private void DrawFindings(InspectionReport report)
        {
            EditorGUILayout.Space();
            _showFindings = EditorGUILayout.Foldout(
                _showFindings, "Findings (" + report.Items.Count + ")");
            if (!_showFindings)
            {
                return;
            }
            if (report.Items.Count == 0)
            {
                EditorGUILayout.HelpBox("No findings.", MessageType.Info);
                return;
            }
            foreach (InspectionItem item in report.Items)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(
                        "[" + item.Category + "] " + item.Message +
                        (item.Path.Length > 0 ? "\n" + item.Path : string.Empty),
                        ToMessageType(item.Severity));

                    if (item.Path.Length > 0
                        && _result.Locations.TryGetValue(item.Path, out UnityEngine.Object location)
                        && location != null
                        && GUILayout.Button("Ping", GUILayout.Width(48f)))
                    {
                        EditorGUIUtility.PingObject(location);
                    }
                }
            }
        }

        private static MessageType ToMessageType(InspectionSeverity severity)
        {
            switch (severity)
            {
                case InspectionSeverity.Error: return MessageType.Error;
                case InspectionSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        private static void ExportMarkdown(InspectionReport report)
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Inspection Report", string.Empty, "inspect-report.md", "md");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            File.WriteAllText(path, report.ToMarkdown());
        }
    }
}
