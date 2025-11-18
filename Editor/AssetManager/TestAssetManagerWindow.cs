using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager {
    public class TestAssetManagerWindow : EditorWindow {
        private readonly List<string> _logs = new();
        private string _boothUrlValue = string.Empty;

        private string _createAssetFilePath = string.Empty;
        private string _descriptionValue = string.Empty;
        private string _folderMoveParentValue = "";
        private string _folderRenameValue = "";
        private string _folderValue = string.Empty;
        private Vector2 _leftScroll;
        private Vector2 _logScroll;
        private string _newFolderDescription = "";
        private string _newFolderName = "";
        private string _renameValue = string.Empty;
        private Vector2 _rightScroll;
        private Ulid _selectedAssetId = Ulid.Empty;

        private Ulid _selectedFolder = Ulid.Empty;
        private string _tagValue = string.Empty;
        private string _thumbnailFilePath = string.Empty;

        private void OnEnable() {
            Application.logMessageReceived += OnLogMessage;
            if (AssetLibrary.Instance.Libraries == null) _logs.Add("Library is not loaded.");

            RefreshSelectedFields();
        }

        private void OnDisable() {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnGUI() {
            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Service Logs", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(160));
            foreach (var l in _logs) EditorGUILayout.LabelField(l);
            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Debug/Test Window")]
        public static void ShowWindow() {
            var window = GetWindow<TestAssetManagerWindow>("Asset Manager Test");
            window.Show();
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type) {
            // Keep logs small
            _logs.Add($"[{DateTime.Now:HH:mm:ss}] {type}: {condition}");
            if (_logs.Count > 200) _logs.RemoveAt(0);
            Repaint();
        }

        private void DrawLeftPanel() {
            EditorGUILayout.BeginVertical(GUILayout.Width(380));
            EditorGUILayout.LabelField("Library Controls", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Initialize Library")) {
                AssetLibrarySerializer.Initialize();
                _logs.Add("Initialize called.");
            }

            if (GUILayout.Button("Load Library")) {
                AssetLibrarySerializer.LoadLibrary();
                _logs.Add("LoadLibrary called.");
            }

            if (GUILayout.Button("Load Cache")) {
                var ok = AssetLibrarySerializer.LoadCache();
                _logs.Add($"LoadCache called: {ok}");
            }

            if (GUILayout.Button("Save Library")) {
                AssetLibrarySerializer.SaveLibrary();
                _logs.Add("SaveLibrary called.");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load AssetLibrary")) {
                AssetLibraryService.LoadAssetLibrary();
                _logs.Add("AssetLibraryService.LoadAssetLibrary() invoked.");
            }

            if (GUILayout.Button("Verify Cache")) {
                _ = AssetLibrarySerializer.LoadAndVerifyAsync();
                _logs.Add("LoadAndVerifyAsync invoked.");
            }

            if (GUILayout.Button("Refresh AssetLibrary")) {
                AssetLibraryService.RefreshAssetLibrary();
                _logs.Add("RefreshAssetLibrary invoked.");
            }

            if (GUILayout.Button("Load All Assets")) {
                AssetLibrarySerializer.LoadAllAssets();
                _logs.Add("LoadAllAssets invoked.");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Library Info", EditorStyles.boldLabel);
            var lib = AssetLibrary.Instance;
            var loaded = lib.Libraries != null;
            EditorGUILayout.LabelField($"Loaded: {loaded}");
            EditorGUILayout.LabelField($"Assets: {lib.Assets.Count}");
            EditorGUILayout.LabelField($"Tags: {lib.GetAllTags().Count}", GUILayout.Width(180));
            // Show both counts: total folders from metadata vs folders that are in use (folder index)
            var usedFolders = lib.GetAllFolders().Count;
            var metadataFolderCount = lib.Libraries == null ? 0 : CountMetadataFolders(lib.Libraries);
            EditorGUILayout.LabelField($"Folders (metadata): {metadataFolderCount}", GUILayout.Width(220));
            EditorGUILayout.LabelField($"Folders (in use): {usedFolders - 1}"); // -1 to exclude root folder

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Create Asset", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("File:", GUILayout.Width(40));
            _createAssetFilePath = EditorGUILayout.TextField(_createAssetFilePath);
            if (GUILayout.Button("...", GUILayout.Width(30))) {
                var selected = EditorUtility.OpenFilePanel("Select Asset File", "", "*");
                if (!string.IsNullOrEmpty(selected)) _createAssetFilePath = selected;
            }

            if (GUILayout.Button("Create", GUILayout.Width(80)))
                try {
                    if (string.IsNullOrEmpty(_createAssetFilePath)) {
                        _logs.Add("Invalid path for Create Asset.");
                    }
                    else {
                        AssetLibraryService.CreateAsset(_createAssetFilePath);
                        _logs.Add("CreateAsset invoked.");
                    }
                }
                catch (Exception e) {
                    _logs.Add($"CreateAsset failed: {e}");
                }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Thumbnail", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _thumbnailFilePath = EditorGUILayout.TextField(_thumbnailFilePath);
            if (GUILayout.Button("...", GUILayout.Width(30))) {
                var selected = EditorUtility.OpenFilePanel("Select Image File", "", "png");
                if (!string.IsNullOrEmpty(selected)) _thumbnailFilePath = selected;
            }

            if (GUILayout.Button("Set Thumbnail", GUILayout.Width(120))) {
                if (_selectedAssetId == Ulid.Empty) {
                    _logs.Add("Select an asset first.");
                }
                else if (string.IsNullOrEmpty(_thumbnailFilePath)) {
                    _logs.Add("Select an image file first.");
                }
                else {
                    AssetLibrarySerializer.SetThumbnail(_selectedAssetId, _thumbnailFilePath);
                    _logs.Add("SetThumbnail invoked.");
                }
            }

            if (GUILayout.Button("Remove Thumbnail", GUILayout.Width(120))) {
                if (_selectedAssetId == Ulid.Empty) {
                    _logs.Add("Select an asset first.");
                }
                else {
                    AssetLibrarySerializer.RemoveThumbnail(_selectedAssetId);
                    _logs.Add("RemoveThumbnail invoked.");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Folders", EditorStyles.boldLabel);
            _selectedFolder = Ulid.TryParse(_folderValue, out var tmpFolder) ? tmpFolder : Ulid.Empty;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Folder Id:", GUILayout.Width(80));
            _folderValue = EditorGUILayout.TextField(_folderValue);
            if (GUILayout.Button("Select", GUILayout.Width(60))) _folderValue = _selectedFolder.ToString();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name:", GUILayout.Width(50));
            _newFolderName = EditorGUILayout.TextField(_newFolderName);
            if (GUILayout.Button("Create Folder", GUILayout.Width(120))) {
                var parentId = _selectedFolder;
                try {
                    AssetLibraryService.CreateFolder(parentId, _newFolderName, _newFolderDescription);
                    _logs.Add("CreateFolder invoked.");
                }
                catch (Exception e) {
                    _logs.Add($"CreateFolder error: {e}");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Desc:", GUILayout.Width(50));
            _newFolderDescription = EditorGUILayout.TextField(_newFolderDescription);

            // list existing folders
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Folders listing", EditorStyles.boldLabel);
            var libs = AssetLibrary.Instance.Libraries;
            if (libs == null)
                EditorGUILayout.LabelField("Library metadata is not loaded.");
            else
                foreach (var rootf in libs.FolderList)
                    DrawFolderInfo(rootf, 0);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rename to:", GUILayout.Width(80));
            _folderRenameValue = EditorGUILayout.TextField(_folderRenameValue);
            if (GUILayout.Button("Rename Folder", GUILayout.Width(120))) {
                if (_selectedFolder != Ulid.Empty) {
                    AssetLibraryService.RenameFolder(_selectedFolder, _folderRenameValue);
                    _logs.Add($"RenameFolder invoked: {_selectedFolder} -> {_folderRenameValue}");
                }
                else {
                    _logs.Add("No selected folder to rename.");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Move parent id:", GUILayout.Width(80));
            _folderMoveParentValue = EditorGUILayout.TextField(_folderMoveParentValue);
            if (GUILayout.Button("Move Folder", GUILayout.Width(120))) {
                if (_selectedFolder == Ulid.Empty) {
                    _logs.Add("No selected folder to move.");
                }
                else if (!Ulid.TryParse(_folderMoveParentValue, out var parentId)) {
                    _logs.Add("Invalid parent id.");
                }
                else {
                    AssetLibraryService.MoveFolder(_selectedFolder, parentId);
                    _logs.Add($"MoveFolder invoked: {_selectedFolder} -> {parentId}");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Desc:", GUILayout.Width(80));
            var descTmp = EditorGUILayout.TextField(_newFolderDescription);
            if (GUILayout.Button("Set Desc", GUILayout.Width(80))) {
                if (_selectedFolder == Ulid.Empty) {
                    _logs.Add("No selected folder for description change.");
                }
                else {
                    AssetLibraryService.SetFolderDescription(_selectedFolder, descTmp);
                    _logs.Add($"SetFolderDescription invoked: {_selectedFolder}");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Update Folder", GUILayout.Width(120))) {
                if (_selectedFolder == Ulid.Empty) {
                    _logs.Add("No selected folder to update.");
                }
                else {
                    var existing = AssetLibrary.Instance.Libraries?.GetFolder(_selectedFolder);
                    if (existing == null) {
                        _logs.Add("Selected folder not found.");
                    }
                    else {
                        if (existing is Folder existingFolder) {
                            var updated = new Folder(existingFolder);
                            if (!string.IsNullOrWhiteSpace(_folderRenameValue)) updated.SetName(_folderRenameValue);
                            updated.SetDescription(descTmp);
                            AssetLibraryService.UpdateFolder(updated);
                            _logs.Add($"UpdateFolder invoked: {_selectedFolder}");
                        }
                        else {
                            _logs.Add("Selected folder is not a Folder and cannot be updated via UpdateFolder.");
                        }
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawRightPanel() {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            var currentAssets = AssetLibrary.Instance.Assets.ToList();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Index", GUILayout.Width(40));
            EditorGUILayout.LabelField("ID", GUILayout.Width(200));
            EditorGUILayout.LabelField("Name", GUILayout.Width(200));
            EditorGUILayout.LabelField("Tags", GUILayout.Width(200));
            EditorGUILayout.LabelField("Folder", GUILayout.Width(120));
            EditorGUILayout.LabelField("Deleted", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            var i = 0;
            foreach (var a in currentAssets) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(40));
                if (GUILayout.Button(a.ID.ToString(), GUILayout.Width(200))) {
                    _selectedAssetId = a.ID;
                    RefreshSelectedFields();
                    _logs.Add($"Selected asset {a.ID}");
                }

                EditorGUILayout.LabelField(a.Name, GUILayout.Width(200));
                EditorGUILayout.LabelField(string.Join(",", a.Tags ?? new List<string>()), GUILayout.Width(200));
                EditorGUILayout.LabelField(a.Folder.ToString(), GUILayout.Width(120));
                EditorGUILayout.LabelField(a.IsDeleted.ToString(), GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
                i++;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            DrawSelectedAssetControls();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedAssetControls() {
            EditorGUILayout.LabelField("Selected Asset", EditorStyles.boldLabel);
            if (_selectedAssetId == Ulid.Empty) {
                EditorGUILayout.LabelField("No asset selected.");
                return;
            }

            var asset = AssetLibrary.Instance.GetAsset(_selectedAssetId);
            if (asset == null) {
                EditorGUILayout.LabelField("Asset not found.");
                return;
            }

            EditorGUILayout.LabelField($"ID: {asset.ID}");
            EditorGUILayout.LabelField($"Name: {asset.Name}");
            EditorGUILayout.LabelField($"Ext: {asset.Ext}");
            EditorGUILayout.LabelField($"Size: {asset.Size}");
            EditorGUILayout.LabelField($"Deleted: {asset.IsDeleted}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Booth Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ShopURL: {asset.BoothData?.ShopURL ?? "-"}");
            EditorGUILayout.LabelField($"ItemURL: {asset.BoothData?.ItemURL ?? "-"}");
            EditorGUILayout.LabelField($"DownloadURL: {asset.BoothData?.DownloadURL ?? "-"}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rename / Update", EditorStyles.boldLabel);
            _renameValue = EditorGUILayout.TextField(_renameValue);
            if (GUILayout.Button("Rename"))
                try {
                    AssetLibraryService.RenameAsset(_selectedAssetId, _renameValue);
                    _logs.Add($"Rename invoked to: {_renameValue}");
                }
                catch (Exception e) {
                    _logs.Add($"Rename failed: {e}");
                }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            _descriptionValue = EditorGUILayout.TextField(_descriptionValue);
            if (GUILayout.Button("Set Description"))
                try {
                    AssetLibraryService.SetDescription(_selectedAssetId, _descriptionValue);
                    _logs.Add("SetDescription invoked.");
                }
                catch (Exception e) {
                    _logs.Add($"SetDescription failed: {e}");
                }

            if (GUILayout.Button("Clear Booth Data", GUILayout.Width(140)))
                try {
                    var current = new AssetMetadata(asset);
                    current.BoothData = new BoothMetadata();
                    AssetLibraryService.UpdateAsset(current);
                    _logs.Add("Clear Booth data invoked.");
                }
                catch (Exception e) {
                    _logs.Add($"Clear Booth data failed: {e}");
                }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Booth url:", GUILayout.Width(80));
            _boothUrlValue = EditorGUILayout.TextField(_boothUrlValue);
            if (GUILayout.Button("Item")) {
                AssetLibraryService.SetBoothItemId(_selectedAssetId, _boothUrlValue);
                _logs.Add("SetBoothItemId invoked.");
            }

            if (GUILayout.Button("Shop")) {
                AssetLibraryService.SetBoothShopDomain(_selectedAssetId, _boothUrlValue);
                _logs.Add("SetBoothShopName invoked.");
            }

            if (GUILayout.Button("Download")) {
                AssetLibraryService.SetBoothDownloadId(_selectedAssetId, _boothUrlValue);
                _logs.Add("SetBoothDownloadId invoked.");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Folder ID:", GUILayout.Width(80));
            _folderValue = EditorGUILayout.TextField(_folderValue);
            if (GUILayout.Button("Set Folder", GUILayout.Width(120))) {
                if (Ulid.TryParse(_folderValue, out var uuid)) {
                    AssetLibraryService.SetFolder(_selectedAssetId, uuid);
                    _logs.Add("SetFolder invoked.");
                }
                else {
                    _logs.Add("Invalid folder id.");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tag:", GUILayout.Width(40));
            _tagValue = EditorGUILayout.TextField(_tagValue);
            if (GUILayout.Button("Add", GUILayout.Width(60))) {
                AssetLibraryService.AddTag(_selectedAssetId, _tagValue);
                _logs.Add($"AddTag: {_tagValue}");
            }

            if (GUILayout.Button("Remove", GUILayout.Width(60))) {
                AssetLibraryService.RemoveTag(_selectedAssetId, _tagValue);
                _logs.Add($"RemoveTag: {_tagValue}");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (!asset.IsDeleted) {
                if (GUILayout.Button("Soft Delete (Trash)", GUILayout.Width(160))) {
                    AssetLibraryService.RemoveAsset(_selectedAssetId);
                    _logs.Add("Soft Delete (RemoveAsset) invoked.");
                }

                if (GUILayout.Button("Permanent Delete", GUILayout.Width(140))) {
                    var ok = EditorUtility.DisplayDialog("Confirm Permanent Delete",
                        "This action will permanently delete the asset and its files from disk. Continue?",
                        "Delete", "Cancel");
                    if (ok) {
                        AssetLibraryService.DeleteAsset(_selectedAssetId);
                        _logs.Add("Permanent Delete (DeleteAsset) invoked.");
                        _selectedAssetId = Ulid.Empty;
                    }
                }
            }
            else {
                if (GUILayout.Button("Restore", GUILayout.Width(120))) {
                    AssetLibraryService.RestoreAsset(_selectedAssetId);
                    _logs.Add("RestoreAsset invoked.");
                }
            }

            if (GUILayout.Button("Update Metadata", GUILayout.Width(120)))
                try {
                    var current = new AssetMetadata(asset);
                    current.SetDescription(_descriptionValue);
                    current.SetName(_renameValue);
                    AssetLibraryService.UpdateAsset(current);
                    _logs.Add("UpdateAsset invoked.");
                }
                catch (Exception e) {
                    _logs.Add($"UpdateAsset failed: {e}");
                }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFolderInfo(BaseFolder folder, int indent) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 12);
            EditorGUILayout.LabelField(folder.Name, GUILayout.Width(220));
            if (GUILayout.Button("Select", GUILayout.Width(60))) {
                _folderValue = folder.ID.ToString();
                _selectedFolder = folder.ID;
            }

            if (GUILayout.Button("Rename", GUILayout.Width(60))) {
                _folderRenameValue = folder.Name;
                _selectedFolder = folder.ID;
            }

            if (GUILayout.Button("Delete", GUILayout.Width(60))) {
                AssetLibraryService.DeleteFolder(folder.ID);
                _logs.Add($"DeleteFolder invoked: {folder.ID}");
            }

            EditorGUILayout.EndHorizontal();

            if (folder is Folder f && f.Children != null)
                foreach (var child in f.Children)
                    DrawFolderInfo(child, indent + 1);
        }

        private void RefreshSelectedFields() {
            if (_selectedAssetId != Ulid.Empty) {
                var a = AssetLibrary.Instance.GetAsset(_selectedAssetId);
                if (a != null) {
                    _renameValue = a.Name;
                    _descriptionValue = a.Description;
                    // prefill booth url input with any existing booth URL (prefers Item then Shop then Download)
                    if (a.BoothData != null)
                        _boothUrlValue = !string.IsNullOrEmpty(a.BoothData.ItemURL) ? a.BoothData.ItemURL :
                            !string.IsNullOrEmpty(a.BoothData.ShopURL) ? a.BoothData.ShopURL :
                            !string.IsNullOrEmpty(a.BoothData.DownloadURL) ? a.BoothData.DownloadURL : string.Empty;
                }
            }
        }

        private int CountMetadataFolders(LibraryMetadata libraries) {
            if (libraries == null) return 0;
            var count = 0;
            foreach (var f in libraries.FolderList) count += 1 + (f is Folder folder ? CountFolderChildren(folder) : 0);
            return count;
        }

        private int CountFolderChildren(Folder folder) {
            if (folder == null || folder.Children == null) return 0;
            var c = 0;
            foreach (var child in folder.Children) {
                if (child is Folder childFolder) c += 1 + CountFolderChildren(childFolder);
            }
            return c;
        }
    }
}