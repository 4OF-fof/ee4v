using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _4OF.ee4v.AssetManager.Data;

namespace _4OF.ee4v.AssetManager.Editor {
    public class TestAssetManagerWindow : EditorWindow {
        private Vector2 scrollPos;
        private readonly Dictionary<string, string> newTagInputs = new();
        private readonly Dictionary<string, string> editTagInputs = new();
        private string libraryVersionInput = "";

        [MenuItem("Debug/Test Window")]
        public static void ShowWindow() {
            var wnd = GetWindow<TestAssetManagerWindow>("AssetManager Test");
            wnd.minSize = new Vector2(400, 300);
        }

        private void OnEnable() {
            try {
                AssetLibrarySerializer.LoadLibrary();
                libraryVersionInput = AssetLibrary.Instance.Libraries?.LibraryVersion ?? "";
            } catch (Exception) {
                // Load が失敗してもウィンドウは表示させる
            }
        }

        private void OnGUI() {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Initialize", EditorStyles.toolbarButton)) {
                AssetLibrarySerializer.Initialize();
                AssetLibrarySerializer.LoadLibrary();
            }
            if (GUILayout.Button("Load", EditorStyles.toolbarButton)) {
                AssetLibrarySerializer.LoadLibrary();
            }
            if (GUILayout.Button("Save", EditorStyles.toolbarButton)) {
                AssetLibrarySerializer.SaveLibrary();
            }
            if (GUILayout.Button("Add Dummy Asset", EditorStyles.toolbarButton)) {
                AddDummyAsset();
            }
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) {
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            var lib = AssetLibrary.Instance.Libraries;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Library", EditorStyles.boldLabel);
            if (lib != null) {
                EditorGUILayout.LabelField("Version:", lib.LibraryVersion);
                EditorGUILayout.LabelField("Modified:", DateTimeOffset.FromUnixTimeMilliseconds(lib.ModificationTime).ToLocalTime().ToString());
                EditorGUILayout.LabelField("Folders:", lib.FolderInfo.Count.ToString());
            } else {
                EditorGUILayout.LabelField("Library metadata not loaded.");
            }
            EditorGUILayout.EndVertical();

            var assets = AssetLibrary.Instance.Assets;
            EditorGUILayout.LabelField($"Assets: {assets.Count}", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            for (int i = 0; i < assets.Count; i++) {
                var a = assets[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Name:", a.Name);
                EditorGUILayout.LabelField("ID:", a.ID.ToString());
                EditorGUILayout.LabelField("Ext:", a.Ext);
                EditorGUILayout.LabelField("Size:", a.Size.ToString());
                EditorGUILayout.LabelField("Modified:", DateTimeOffset.FromUnixTimeMilliseconds(a.ModificationTime).ToLocalTime().ToString());
                EditorGUILayout.Space();
                // Tags
                EditorGUILayout.LabelField("Tags:");
                EditorGUILayout.BeginHorizontal();
                var tags = a.Tags;
                for (int t = 0; t < tags.Count; t++) {
                    var tag = tags[t];
                    EditorGUILayout.BeginVertical(GUILayout.Width(160));
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(tag, GUILayout.Width(110));
                    if (GUILayout.Button("X", GUILayout.Width(20))) {
                        if (EditorUtility.DisplayDialog("Confirm", $"Remove tag '{tag}' from {a.Name}?", "Yes", "No")) {
                            a.RemoveTag(tag);
                            AssetLibrary.Instance.UpdateAsset(a);
                            AssetLibrarySerializer.SaveAsset(a);
                            Repaint();
                            break;
                        }
                    }
                    if (GUILayout.Button("Edit", GUILayout.Width(30))) {
                        var key = a.ID + ":" + tag;
                        editTagInputs[key] = tag;
                    }
                    EditorGUILayout.EndHorizontal();
                    // edit field if present
                    var editKey = a.ID + ":" + tag;
                    if (editTagInputs.TryGetValue(editKey, out var editVal)) {
                        EditorGUILayout.BeginHorizontal();
                        editVal = EditorGUILayout.TextField(editVal);
                        if (GUILayout.Button("Save", GUILayout.Width(50))) {
                            if (!string.IsNullOrEmpty(editVal) && editVal != tag) {
                                a.RemoveTag(tag);
                                a.AddTag(editVal);
                                AssetLibrary.Instance.UpdateAsset(a);
                                AssetLibrarySerializer.SaveAsset(a);
                            }
                            editTagInputs.Remove(editKey);
                            Repaint();
                        }
                        if (GUILayout.Button("Cancel", GUILayout.Width(60))) {
                            editTagInputs.Remove(editKey);
                        }
                        EditorGUILayout.EndHorizontal();
                        editTagInputs[editKey] = editVal;
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();

                // Add new tag
                var idKey = a.ID.ToString();
                if (!newTagInputs.ContainsKey(idKey)) newTagInputs[idKey] = "";
                EditorGUILayout.BeginHorizontal();
                newTagInputs[idKey] = EditorGUILayout.TextField(newTagInputs[idKey]);
                if (GUILayout.Button("Add Tag", GUILayout.Width(80))) {
                    var newTag = newTagInputs[idKey]?.Trim();
                    if (!string.IsNullOrEmpty(newTag)) {
                        a.AddTag(newTag);
                        AssetLibrary.Instance.UpdateAsset(a);
                        AssetLibrarySerializer.SaveAsset(a);
                        newTagInputs[idKey] = "";
                        Repaint();
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Delete", GUILayout.Width(80))) {
                    if (EditorUtility.DisplayDialog("Confirm", $"Remove asset {a.Name}?", "Yes", "No")) {
                        AssetLibrary.Instance.RemoveAsset(a.ID);
                        AssetLibrarySerializer.SaveLibrary();
                        Repaint();
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("このウィンドウはテスト用です。Unity エディタ上で操作してください。", MessageType.Info);
        }

        private void AddDummyAsset() {
            var path = EditorUtility.OpenFilePanel("Select Asset File", "", "");
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            try {
                AssetLibrarySerializer.AddAsset(path);
            } catch (Exception ex) {
                Debug.LogError($"Failed to add asset: {ex}");
                EditorUtility.DisplayDialog("Error", $"Failed to add asset:\n{ex.Message}", "OK");
            }
            Repaint();
        }
    }
}
