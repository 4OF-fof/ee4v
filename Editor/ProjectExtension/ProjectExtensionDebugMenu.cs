using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.ProjectExtension.Data;
using _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension {
    public static class ProjectExtensionDebugMenu {
        [MenuItem("Debug/Add Workspace Tab")]
        private static void AddWorkspaceTab() {
            AddWorkspaceTabWindow.ShowWindow();
        }
    }

    public class AddWorkspaceTabWindow : EditorWindow {
        private string _workspaceName = "Workspace";
        private Vector2 _scrollPosition;
        private List<string> _allLabels = new List<string>();
        private string _searchFilter = "";

        public static void ShowWindow() {
            var window = GetWindow<AddWorkspaceTabWindow>("Add Workspace Tab");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable() {
            RefreshLabels();
        }

        private void RefreshLabels() {
            // すべてのアセットのラベルを取得
            var allAssetPaths = AssetDatabase.GetAllAssetPaths();
            var labelSet = new HashSet<string>();

            foreach (var path in allAssetPaths) {
                if (!path.StartsWith("Assets/")) continue;
                
                var labels = AssetDatabase.GetLabels(AssetDatabase.LoadAssetAtPath<Object>(path));
                foreach (var label in labels) {
                    labelSet.Add(label);
                }
            }

            _allLabels = labelSet.OrderBy(l => l).ToList();
        }

        private void OnGUI() {
            GUILayout.Space(10);

            // 名前の入力フィールド
            EditorGUILayout.LabelField("Workspace Name", EditorStyles.boldLabel);
            _workspaceName = EditorGUILayout.TextField(_workspaceName);

            GUILayout.Space(10);

            // カスタム名前でタブを追加するボタン
            if (GUILayout.Button("Add Workspace Tab with Custom Name", GUILayout.Height(30))) {
                if (!string.IsNullOrWhiteSpace(_workspaceName)) {
                    CreateWorkspaceTab(_workspaceName);
                } else {
                    EditorUtility.DisplayDialog("Error", "Workspace name cannot be empty.", "OK");
                }
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(10);

            // 既存のラベル一覧
            EditorGUILayout.LabelField("Existing Labels", EditorStyles.boldLabel);
            
            // 検索フィルター
            GUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search:", _searchFilter);
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) {
                RefreshLabels();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (_allLabels.Count == 0) {
                EditorGUILayout.HelpBox("No labels found in the project.", MessageType.Info);
            } else {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                var filteredLabels = string.IsNullOrEmpty(_searchFilter)
                    ? _allLabels
                    : _allLabels.Where(l => l.ToLower().Contains(_searchFilter.ToLower())).ToList();

                foreach (var label in filteredLabels) {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(label);
                    if (GUILayout.Button("Add", GUILayout.Width(60))) {
                        CreateWorkspaceTab(label);
                    }
                    if (GUILayout.Button("Remove", GUILayout.Width(70))) {
                        RemoveLabel(label);
                    }
                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void CreateWorkspaceTab(string tabName) {
            var workspaceTab = WorkspaceTab.Element("Assets", tabName);
            TabListController.AddWorkspaceTab(workspaceTab);
            TabListController.SelectTab(workspaceTab);
            
            Debug.Log($"Workspace tab '{tabName}' added successfully.");
        }

        private void RemoveLabel(string labelName) {
            if (!EditorUtility.DisplayDialog(
                "Remove Label",
                $"Are you sure you want to remove the label '{labelName}' from all assets?",
                "Remove",
                "Cancel")) {
                return;
            }

            var allAssetPaths = AssetDatabase.GetAllAssetPaths();
            var removedCount = 0;

            foreach (var path in allAssetPaths) {
                if (!path.StartsWith("Assets/")) continue;

                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null) continue;

                var labels = AssetDatabase.GetLabels(asset).ToList();
                if (labels.Contains(labelName)) {
                    labels.Remove(labelName);
                    AssetDatabase.SetLabels(asset, labels.ToArray());
                    removedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            RefreshLabels();

            Debug.Log($"Label '{labelName}' removed from {removedCount} asset(s).");
            EditorUtility.DisplayDialog("Complete", $"Label '{labelName}' has been removed from {removedCount} asset(s).", "OK");
        }
    }
}