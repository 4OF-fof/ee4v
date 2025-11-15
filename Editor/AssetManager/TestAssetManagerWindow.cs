using System;
using System.IO;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.Data;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager {
    public class TestAssetManagerWindow : EditorWindow {
        // Inputs
        private string _assetIdInput = "";
        private string _newAssetName = "New Asset";
        private string _newFolderName = "New Folder";
        private Vector2 _scroll;

        private void OnGUI() {
            EditorGUILayout.LabelField("Asset Manager - Data Operation Tests", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Initialize (create dirs & metadata)")) AssetLibrarySerializer.Initialize();
                if (GUILayout.Button("Load AssetLibrary")) AssetLibraryService.LoadAssetLibrary();
                if (GUILayout.Button("Load Library")) AssetLibrarySerializer.LoadLibrary();
                if (GUILayout.Button("Load All Assets")) AssetLibrarySerializer.LoadAllAssets();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Add Asset (pick file)")) AddAssetFromFile();
                if (GUILayout.Button("Unload Library")) AssetLibrary.Instance.UnloadLibrary();
                if (GUILayout.Button("Clear Library (delete on-disk)")) ClearLibraryOnDisk();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope("box")) {
                EditorGUILayout.LabelField("Library Info", EditorStyles.boldLabel);
                DrawLibraryInfo();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope("box")) {
                EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
                DrawAssets();
                EditorGUILayout.EndScrollView();
            }
        }

        [MenuItem("Debug/Asset Manager Test")]
        public static void OpenWindow() {
            var w = GetWindow<TestAssetManagerWindow>("AssetManager Test");
            w.minSize = new Vector2(600, 300);
        }

        private void AddAssetFromFile() {
            var path = EditorUtility.OpenFilePanel("Select asset file", Application.dataPath, "*");
            if (string.IsNullOrEmpty(path)) return;
            try {
                AssetLibrarySerializer.AddAsset(path);
                AssetLibrarySerializer.SaveLibrary();
                AssetDatabase.Refresh();
            }
            catch (Exception e) {
                Debug.LogError(e);
            }
        }

        private void ClearLibraryOnDisk() {
            if (!EditorUtility.DisplayDialog("Confirm",
                    "Delete AssetManager data folder on disk? This will remove all saved metadata and asset copies.",
                    "Delete", "Cancel")) return;
            var root = Path.Combine(EditorPrefsManager.ContentFolderPath, "AssetManager");
            try {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                AssetLibrary.Instance.UnloadLibrary();
                AssetDatabase.Refresh();
            }
            catch (Exception e) {
                Debug.LogError(e);
            }
        }

        private void DrawLibraryInfo() {
            var lib = AssetLibrary.Instance.Libraries;
            if (lib == null) {
                EditorGUILayout.HelpBox("Library is null. Call Initialize or Load Library.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Library Version:", lib.LibraryVersion);
            EditorGUILayout.LabelField("Modification Time:", lib.ModificationTime.ToString());

            using (new EditorGUILayout.HorizontalScope()) {
                _newFolderName = EditorGUILayout.TextField("New Folder Name", _newFolderName);
                if (GUILayout.Button("Add Folder", GUILayout.Width(120))) {
                    var f = new FolderInfo();
                    f.UpdateName(_newFolderName);
                    lib.AddFolder(f);
                    AssetLibrarySerializer.SaveLibrary();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Folders:");
            var folders = lib.FolderInfo;
            if (folders.Count == 0) EditorGUILayout.LabelField("(no folders)");
            else
                foreach (var f in folders)
                    DrawFolderInfo(f, lib);
        }

        private void DrawFolderInfo(FolderInfo f, LibraryMetadata lib) {
            using (new EditorGUILayout.VerticalScope("box")) {
                EditorGUILayout.LabelField($"{f.Name} ({f.ID})");
                EditorGUILayout.LabelField("Description:", f.Description);
                EditorGUILayout.LabelField("Modified:", f.ModificationTime.ToString());
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Remove Folder")) {
                        lib.RemoveFolder(f.ID);
                        AssetLibrarySerializer.SaveLibrary();
                        return;
                    }

                    if (GUILayout.Button("Add Child Folder")) {
                        var child = new FolderInfo();
                        child.UpdateName(f.Name + "_child");
                        f.AddChild(child);
                        AssetLibrarySerializer.SaveLibrary();
                    }
                }

                if (f.Children.Count > 0) {
                    EditorGUILayout.LabelField("Children:");
                    foreach (var c in f.Children) DrawFolderInfo(c, lib);
                }
            }
        }

        private void DrawAssets() {
            var assets = AssetLibrary.Instance.Assets;
            if (assets == null || assets.Count == 0) {
                EditorGUILayout.LabelField("(no assets)");
                return;
            }

            foreach (var a in assets)
                using (new EditorGUILayout.VerticalScope("box")) {
                    EditorGUILayout.LabelField($"Name: {a.Name}");
                    EditorGUILayout.LabelField($"ID: {a.ID}");
                    EditorGUILayout.LabelField($"Desc: {a.Description}");
                    EditorGUILayout.LabelField($"Size: {a.Size}");
                    EditorGUILayout.LabelField($"Ext: {a.Ext}");
                    EditorGUILayout.LabelField($"Folder: {(a.Folder.HasValue ? a.Folder.Value.ToString() : "(none)")}");
                    EditorGUILayout.LabelField($"Tags: {string.Join(",", a.Tags)}");
                    EditorGUILayout.LabelField($"Deleted: {a.IsDeleted}");
                    EditorGUILayout.LabelField($"Modified: {a.ModificationTime}");

                    using (new EditorGUILayout.HorizontalScope()) {
                        if (GUILayout.Button("Load From Disk")) AssetLibrarySerializer.LoadAsset(a.ID);
                        if (GUILayout.Button("Remove Asset"))
                            if (EditorUtility.DisplayDialog("Confirm",
                                    $"Remove metadata for asset '{a.Name}' ({a.ID})? This will not delete files on disk.",
                                    "Remove", "Cancel")) {
                                AssetLibrary.Instance.RemoveAsset(a.ID);
                                AssetLibrarySerializer.SaveLibrary();
                            }

                        if (GUILayout.Button("Delete Asset (on-disk)"))
                            if (EditorUtility.DisplayDialog("Confirm",
                                    $"Permanently delete asset '{a.Name}' ({a.ID}) and its on-disk files?", "Delete",
                                    "Cancel")) {
                                AssetLibrarySerializer.RemoveAsset(a.ID);
                                AssetLibrarySerializer.SaveLibrary();
                                AssetDatabase.Refresh();
                            }
                    }
                }
        }
    }
}