using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager {
    public class TestAssetManagerWindow : EditorWindow {
        // rename inputs for folders
        private readonly Dictionary<string, string> _folderRenameInputs = new();

        // tag input per-asset
        private readonly Dictionary<string, string> _tagInputs = new();

        // rename inputs for library tags
        private readonly Dictionary<string, string> _tagRenameInputs = new();

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
                if (GUILayout.Button("Unload Library")) AssetLibrary.Instance.UnloadAssetLibrary();
                if (GUILayout.Button("Clear Library (delete on-disk)")) ClearLibraryOnDisk();
            }

            EditorGUILayout.Space();

            EditorGUILayout.Space();
            // Combine the library info and assets into a single scroll view
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope("box")) {
                EditorGUILayout.LabelField("Library Info", EditorStyles.boldLabel);
                DrawLibraryInfo();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope("box")) {
                EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
                DrawAssets();
            }

            EditorGUILayout.EndScrollView();
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
                AssetLibrary.Instance.UnloadAssetLibrary();
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
                    f.SetName(_newFolderName);
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("All Tags:", EditorStyles.boldLabel);
            var allTags = AssetLibrary.Instance.GetAllTags();
            if (allTags == null || allTags.Count == 0) EditorGUILayout.LabelField("(no tags)");
            else
                foreach (var tag in allTags)
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.LabelField(tag, GUILayout.Width(200));
                        if (!_tagRenameInputs.ContainsKey(tag)) _tagRenameInputs[tag] = "";
                        _tagRenameInputs[tag] = EditorGUILayout.TextField(_tagRenameInputs[tag]);
                        if (GUILayout.Button("Rename", GUILayout.Width(80))) {
                            var newTag = _tagRenameInputs[tag]?.Trim();
                            if (!string.IsNullOrEmpty(newTag) && newTag != tag) {
                                AssetLibrary.Instance.RenameTag(tag, newTag);
                                // save any assets that were affected
                                foreach (var asset in AssetLibrary.Instance.Assets)
                                    if (asset.Tags.Contains(newTag) || asset.Tags.Contains(tag))
                                        AssetLibrarySerializer.SaveAsset(asset);

                                AssetLibrarySerializer.SaveLibrary();
                            }
                        }
                    }
        }

        private void DrawFolderInfo(FolderInfo f, LibraryMetadata lib) {
            using (new EditorGUILayout.VerticalScope("box")) {
                EditorGUILayout.LabelField($"{f.Name} ({f.ID})");
                // Rename UI for folder
                var folderKey = f.ID.ToString();
                if (!_folderRenameInputs.ContainsKey(folderKey)) _folderRenameInputs[folderKey] = f.Name;
                using (new EditorGUILayout.HorizontalScope()) {
                    _folderRenameInputs[folderKey] = EditorGUILayout.TextField(_folderRenameInputs[folderKey]);
                    if (GUILayout.Button("Rename Folder", GUILayout.Width(120))) {
                        var newName = _folderRenameInputs[folderKey]?.Trim();
                        if (!string.IsNullOrEmpty(newName) && newName != f.Name) {
                            f.SetName(newName);
                            AssetLibrarySerializer.SaveLibrary();
                        }
                    }
                }

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
                        child.SetName(f.Name + "_child");
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

            // build folder options (flat list) from library
            var lib = AssetLibrary.Instance.Libraries;
            var folderNames = new List<string> { "(none)" };
            var folderIds = new List<Ulid?> { null };
            if (lib != null) {
                void WalkFolders(IEnumerable<FolderInfo> cols) {
                    foreach (var f in cols) {
                        folderNames.Add(f.Name);
                        folderIds.Add(f.ID);
                        if (f.Children.Count > 0) WalkFolders(f.Children);
                    }
                }

                WalkFolders(lib.FolderInfo);
            }

            foreach (var a in assets)
                using (new EditorGUILayout.VerticalScope("box")) {
                    EditorGUILayout.LabelField($"ID: {a.ID}");

                    // Editable name
                    var newName = EditorGUILayout.TextField("Name", a.Name);
                    if (newName != a.Name) AssetLibraryService.RenameAsset(a.ID, newName);

                    // Editable description
                    var newDesc = EditorGUILayout.TextField("Description", a.Description);
                    if (newDesc != a.Description) AssetLibraryService.SetDescription(a.ID, newDesc);

                    EditorGUILayout.LabelField($"Size: {a.Size}");
                    EditorGUILayout.LabelField($"Ext: {a.Ext}");

                    // Folder popup
                    if (folderNames.Count > 0) {
                        var currentFolderId = a.Folder == Ulid.Empty ? (Ulid?)null : a.Folder;
                        var selectedIndex = 0;
                        for (var i = 0; i < folderIds.Count; i++)
                            if (folderIds[i].HasValue && currentFolderId.HasValue &&
                                folderIds[i].Value == currentFolderId.Value) {
                                selectedIndex = i;
                                break;
                            }

                        var newIndex = EditorGUILayout.Popup("Folder", selectedIndex, folderNames.ToArray());
                        if (newIndex != selectedIndex) {
                            if (newIndex == 0) {
                                AssetLibraryService.SetFolder(a.ID, Ulid.Empty);
                            }
                            else {
                                var selectedFolder = folderIds[newIndex];
                                if (selectedFolder.HasValue) AssetLibraryService.SetFolder(a.ID, selectedFolder.Value);
                            }
                        }
                    }

                    // Tags: list + remove
                    EditorGUILayout.LabelField($"Tags: {string.Join(",", a.Tags)}");
                    using (new EditorGUILayout.HorizontalScope()) {
                        var key = a.ID.ToString();
                        if (!_tagInputs.ContainsKey(key)) _tagInputs[key] = "";
                        _tagInputs[key] = EditorGUILayout.TextField(_tagInputs[key]);
                        if (GUILayout.Button("Add Tag", GUILayout.Width(80))) {
                            var t = _tagInputs[key]?.Trim();
                            if (!string.IsNullOrEmpty(t)) {
                                _tagInputs[key] = "";
                                AssetLibraryService.AddTag(a.ID, t);
                            }
                        }
                    }

                    if (a.Tags != null && a.Tags.Count > 0)
                        using (new EditorGUILayout.HorizontalScope()) {
                            foreach (var tag in a.Tags.ToList())
                                if (GUILayout.Button($"Remove: {tag}", GUILayout.Width(120)))
                                    AssetLibraryService.RemoveTag(a.ID, tag);
                        }

                    EditorGUILayout.LabelField($"Deleted: {a.IsDeleted}");
                    EditorGUILayout.LabelField($"Modified: {a.ModificationTime}");

                    using (new EditorGUILayout.HorizontalScope()) {
                        if (GUILayout.Button("Set Thumbnail")) {
                            var thumbPath = EditorUtility.OpenFilePanel("Select thumbnail image", Application.dataPath,
                                "png,jpg,jpeg");
                            if (!string.IsNullOrEmpty(thumbPath)) AssetLibrarySerializer.SetThumbnail(a.ID, thumbPath);
                        }

                        if (GUILayout.Button("Remove Thumbnail")) AssetLibrarySerializer.RemoveThumbnail(a.ID);
                        if (GUILayout.Button("Load From Disk")) AssetLibrarySerializer.LoadAsset(a.ID);
                        if (GUILayout.Button("Remove Asset"))
                            if (EditorUtility.DisplayDialog("Confirm",
                                    $"Remove metadata for asset '{a.Name}' ({a.ID})? This will not delete files on disk.",
                                    "Remove", "Cancel")) {
                                var asset = AssetLibrary.Instance.GetAsset(a.ID);
                                asset.SetDeleted(true);
                                AssetLibrarySerializer.SaveAsset(asset);
                            }

                        if (GUILayout.Button("Delete Asset (on-disk)"))
                            if (EditorUtility.DisplayDialog("Confirm",
                                    $"Permanently delete asset '{a.Name}' ({a.ID}) and its on-disk files?", "Delete",
                                    "Cancel")) {
                                AssetLibrarySerializer.DeleteAsset(a.ID);
                                AssetLibrarySerializer.SaveLibrary();
                                AssetDatabase.Refresh();
                            }
                    }
                }
        }
    }
}