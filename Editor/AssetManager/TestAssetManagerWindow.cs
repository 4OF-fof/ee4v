using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace Assets._4OF.ee4v.AssetManager {
	public class TestAssetManagerWindow : EditorWindow {
		private Vector2 _scrollPos;
		private Vector2 _windowScrollPos;
		private Vector2 _logScrollPos;
		private string _filterName = "";
		private string _filterTag = "";
		private List<AssetMetadata> _assetList = new();
		private int _selectedIndex = -1;

		// Editing values
		private string _editName = "";
		private string _editDescription = "";
		private string _editFolder = "";
		private int _selectedFolderIndex = 0;
		private List<(Ulid id, string path)> _folderOptions = new();
		private string _newFolderName = "";
		private string _newFolderDescription = "";
		private int _newFolderParentIndex = 0;
		private int _renameFolderIndex = 0;
		private string _renameFolderName = "";
		private int _removeFolderIndex = 0;
		private string _tagInput = "";
        private string _newAssetFilePath = "";

		private readonly List<string> _logs = new();

		[MenuItem("Debug/Asset Manager Test")]
		public static void ShowWindow() {
			var window = GetWindow<TestAssetManagerWindow>("AssetManager Test");
			AssetLibraryService.LoadAssetLibrary();
			window.RefreshAssets();
			window.Show();
		}

		private void OnEnable() {
			RefreshAssets();
		}

		private void OnDisable() {
			_assetList.Clear();
			_selectedIndex = -1;
		}

		private void RefreshAssets() {
			_assetList = AssetLibrary.Instance.Assets.OrderBy(a => a.Name).ToList();
			if (_selectedIndex >= _assetList.Count) _selectedIndex = -1;
			Repaint();
			RefreshFolderOptions();
		}

		private void RefreshFolderOptions() {
			_folderOptions = new List<(Ulid, string)> { (Ulid.Empty, "Root") };
			var libraries = AssetLibrary.Instance.Libraries;
			if (libraries == null) return;
			foreach (var f in libraries.FolderInfo) AddFolderOptionsRecursive(f, "");
		}

		private void AddFolderOptionsRecursive(FolderInfo folder, string prefix) {
			var path = string.IsNullOrEmpty(prefix) ? folder.Name : $"{prefix}/{folder.Name}";
			_folderOptions.Add((folder.ID, path));
			foreach (var child in folder.Children) AddFolderOptionsRecursive(child, path);
		}

		private void OnGUI() {
			EditorGUILayout.Space();
			// Wrap the whole window in a scroll view so long forms can scroll
			_windowScrollPos = EditorGUILayout.BeginScrollView(_windowScrollPos);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Reload library")) {
				AssetLibraryService.LoadAssetLibrary();
				AddLog("Request: LoadAssetLibrary");
				RefreshAssets();
			}

			if (GUILayout.Button("Refresh library")) {
				AssetLibraryService.RefreshAssetLibrary();
				AddLog("Request: RefreshAssetLibrary");
				RefreshAssets();
			}

			if (GUILayout.Button("Refresh list")) {
				RefreshAssets();
				AddLog("UI: Asset list refreshed");
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			_filterName = EditorGUILayout.TextField("Filter name", _filterName);
			_filterTag = EditorGUILayout.TextField("Filter tag", _filterTag);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(220));
			var filtered = _assetList.Where(a => (string.IsNullOrEmpty(_filterName) || a.Name.IndexOf(_filterName, StringComparison.OrdinalIgnoreCase) >= 0)
												  && (string.IsNullOrEmpty(_filterTag) || a.Tags.Contains(_filterTag))).ToList();

			for (var i = 0; i < filtered.Count; i++) {
				var asset = filtered[i];
				EditorGUILayout.BeginHorizontal();
				GUI.enabled = _selectedIndex != _assetList.IndexOf(asset) ;
				if (GUILayout.Button(asset.Name, GUILayout.Width(240))) {
					_selectedIndex = _assetList.IndexOf(asset);
					LoadSelectedAssetIntoEdit();
				}
				GUI.enabled = true;
				EditorGUILayout.LabelField(asset.ID.ToString(), GUILayout.Width(260));
				EditorGUILayout.LabelField(asset.IsDeleted ? "Deleted" : "Active", GUILayout.Width(60));
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Selected Asset", EditorStyles.boldLabel);
			if (_selectedIndex < 0 || _selectedIndex >= _assetList.Count) {
				EditorGUILayout.LabelField("No asset selected");
			}
			else {
				var selected = _assetList[_selectedIndex];
				EditorGUILayout.LabelField("ID: ", selected.ID.ToString());
				EditorGUILayout.LabelField("Name: ", selected.Name);
				EditorGUILayout.LabelField("Name: ", selected.Description);
				EditorGUILayout.LabelField("Folder: ", selected.Folder.ToString());
				EditorGUILayout.BeginHorizontal();
				// Folder selection dropdown
				var folderNames = _folderOptions.Select(x => x.path).ToArray();
				if (folderNames.Length == 0) RefreshFolderOptions();
				// Ensure selected index is within bounds
				_selectedFolderIndex = Mathf.Clamp(_selectedFolderIndex, 0, Math.Max(0, _folderOptions.Count - 1));
				// Show the popup using the persisted _selectedFolderIndex so the selection stays while the user picks
				var newIndex = EditorGUILayout.Popup("Select folder", _selectedFolderIndex, folderNames);
				if (newIndex != _selectedFolderIndex) {
					_selectedFolderIndex = newIndex;
					_editFolder = _folderOptions[newIndex].id.ToString();
				}
				if (GUILayout.Button("Apply Folder")) {
					ApplyFolder(selected);
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.LabelField("Tags: ", string.Join(", ", selected.Tags));
				// list asset tags with remove buttons
				EditorGUILayout.BeginHorizontal();
				foreach (var t in selected.Tags) {
					if (GUILayout.Button($"- {t}", GUILayout.Width(120))) {
						AssetLibraryService.RemoveTag(selected.ID, t);
						AddLog($"Request: RemoveTag {selected.ID} -> {t}");
						RefreshAssets();
						break;
					}
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.LabelField("Deleted: ", selected.IsDeleted.ToString());
				EditorGUILayout.Space();

				EditorGUILayout.LabelField("Edit fields", EditorStyles.boldLabel);
				_editName = EditorGUILayout.TextField("Name", _editName);
				_editDescription = EditorGUILayout.TextField("Description", _editDescription);

				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("Apply Rename")) {
					ApplyRename(selected);
				}

				if (GUILayout.Button("Apply Description")) {
					ApplyDescription(selected);
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
				EditorGUILayout.BeginHorizontal();
				_tagInput = EditorGUILayout.TextField(_tagInput);
				if (GUILayout.Button("Add Tag", GUILayout.Width(100))) {
					ApplyAddTag(selected);
				}

				if (GUILayout.Button("Remove Tag", GUILayout.Width(100))) {
					ApplyRemoveTag(selected);
				}
				EditorGUILayout.EndHorizontal();

				// helper: existing tags list for quick add
				var allTags = AssetLibrary.Instance.GetAllTags();
				if (allTags.Count > 0) {
					EditorGUILayout.LabelField("All Tags", EditorStyles.miniLabel);
					EditorGUILayout.BeginHorizontal();
					foreach (var t in allTags) {
						if (GUILayout.Button(t, GUILayout.Width(100))) {
							AssetLibraryService.AddTag(selected.ID, t);
							AddLog($"Request: AddTag {selected.ID} -> {t}");
							RefreshAssets();
							break;
						}
					}
					EditorGUILayout.EndHorizontal();
				}

				EditorGUILayout.Space();
				EditorGUILayout.BeginHorizontal();
				if (!selected.IsDeleted) {
					EditorGUILayout.BeginHorizontal();
					if (GUILayout.Button("Mark Deleted")) {
						AssetLibraryService.RemoveAsset(selected.ID);
						AddLog($"Request: RemoveAsset {selected.ID}");
						RefreshAssets();
					}
					if (GUILayout.Button("Delete Physically")) {
						if (EditorUtility.DisplayDialog("Confirm Delete", $"Are you sure you want to permanently delete asset {selected.Name}?", "Delete", "Cancel")) {
							AssetLibraryService.DeleteAsset(selected.ID);
							AddLog($"Request: DeleteAsset {selected.ID}");
							RefreshAssets();
						}
					}
					EditorGUILayout.EndHorizontal();
				}
				else {
					if (GUILayout.Button("Restore Asset")) {
						AssetLibraryService.RestoreAsset(selected.ID);
						AddLog($"Request: RestoreAsset {selected.ID}");
						RefreshAssets();
					}
				}
				EditorGUILayout.EndHorizontal();

				// Folder operations (create/rename/remove) are not available via AssetLibraryService.
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Folder operations", EditorStyles.boldLabel);
					EditorGUILayout.BeginVertical(GUILayout.Width(600));

					// Create folder UI
					EditorGUILayout.LabelField("Create Folder", EditorStyles.boldLabel);
					_newFolderName = EditorGUILayout.TextField("Name", _newFolderName);
					_newFolderDescription = EditorGUILayout.TextField("Description", _newFolderDescription);
					if (_folderOptions.Count == 0) RefreshFolderOptions();
					var parentNames = _folderOptions.Select(x => x.path).ToArray();
					_newFolderParentIndex = Mathf.Clamp(_newFolderParentIndex, 0, Math.Max(0, _folderOptions.Count - 1));
					_newFolderParentIndex = EditorGUILayout.Popup("Parent folder", _newFolderParentIndex, parentNames);
					if (GUILayout.Button("Create Folder")) {
						var parentId = _folderOptions.ElementAtOrDefault(_newFolderParentIndex).id;
						AssetLibraryService.CreateFolder(parentId, _newFolderName, _newFolderDescription);
						AddLog($"Request: AddFolder '{_newFolderName}' parent {parentId}");
						RefreshAssets();
						_newFolderName = "";
						_newFolderDescription = "";
					}

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Rename Folder", EditorStyles.boldLabel);
					var renameNames = _folderOptions.Select(x => x.path).ToArray();
					_renameFolderIndex = Mathf.Clamp(_renameFolderIndex, 0, Math.Max(0, _folderOptions.Count - 1));
					_renameFolderIndex = EditorGUILayout.Popup("Folder", _renameFolderIndex, renameNames);
					_renameFolderName = EditorGUILayout.TextField("New name", _renameFolderName);
					if (GUILayout.Button("Rename Folder")) {
						var folderId = _folderOptions.ElementAtOrDefault(_renameFolderIndex).id;
						AssetLibraryService.RenameFolder(folderId, _renameFolderName);
						AddLog($"Request: RenameFolder {folderId} -> {_renameFolderName}");
						RefreshAssets();
						_renameFolderName = "";
					}

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Remove Folder", EditorStyles.boldLabel);
					var removeNames = _folderOptions.Select(x => x.path).ToArray();
					_removeFolderIndex = Mathf.Clamp(_removeFolderIndex, 0, Math.Max(0, _folderOptions.Count - 1));
					_removeFolderIndex = EditorGUILayout.Popup("Folder", _removeFolderIndex, removeNames);
					if (GUILayout.Button("Remove Folder")) {
						var folderId = _folderOptions.ElementAtOrDefault(_removeFolderIndex).id;
						// skip removing root
						if (folderId == Ulid.Empty) {
							AddLog($"Cannot remove root folder");
						}
						else {
							AssetLibraryService.DeleteFolder(folderId);
							AddLog($"Request: RemoveFolder {folderId}");
							RefreshAssets();
							_removeFolderIndex = 0;
						}
					}

					EditorGUILayout.EndVertical();
			}

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Asset operations", EditorStyles.boldLabel);
				EditorGUILayout.BeginHorizontal();
				_newAssetFilePath = EditorGUILayout.TextField("Asset file", _newAssetFilePath);
				if (GUILayout.Button("Pick", GUILayout.Width(60))) {
					var path = EditorUtility.OpenFilePanel("Select asset file", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "*");
					if (!string.IsNullOrEmpty(path)) _newAssetFilePath = path;
				}
				if (GUILayout.Button("Add Asset", GUILayout.Width(100))) {
					if (string.IsNullOrEmpty(_newAssetFilePath) || !System.IO.File.Exists(_newAssetFilePath)) {
						AddLog($"Invalid file path: {_newAssetFilePath}");
					}
					else {
						AssetLibraryService.CreateAsset(_newAssetFilePath);
						AddLog($"Request: CreateAsset {_newAssetFilePath}");
						RefreshAssets();
					}
				}
				EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Logs", EditorStyles.boldLabel);
			// Make logs scrollable so long histories don't overflow the window
			EditorGUILayout.BeginVertical(GUI.skin.box);
			_logScrollPos = EditorGUILayout.BeginScrollView(_logScrollPos, GUILayout.Height(160));
			var start = Math.Max(0, _logs.Count - 200);
			for (var i = start; i < _logs.Count; i++) {
				var log = _logs[i];
				EditorGUILayout.LabelField(log);
			}
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			if (GUILayout.Button("Clear logs")) _logs.Clear();
			EditorGUILayout.EndScrollView();
		}

		private void LoadSelectedAssetIntoEdit() {
			if (_selectedIndex < 0 || _selectedIndex >= _assetList.Count) {
				_editName = _editDescription = _editFolder = _tagInput = "";
				return;
			}

			var asset = _assetList[_selectedIndex];
			_editName = asset.Name;
			_editDescription = asset.Description;
			_editFolder = asset.Folder.ToString();
			// Initialize the popup selection index to match the asset's current folder
			var idx = _folderOptions.FindIndex(x => x.id == asset.Folder);
			_selectedFolderIndex = idx < 0 ? 0 : idx;
			_tagInput = "";
		}

		private void ApplyRename(AssetMetadata selected) {
			if (string.IsNullOrWhiteSpace(_editName)) {
				AddLog("Rename aborted: name empty");
				return;
			}
			AssetLibraryService.RenameAsset(selected.ID, _editName);
			AddLog($"Request: RenameAsset {selected.ID} -> {_editName}");
			RefreshAssets();
		}

		private void ApplyDescription(AssetMetadata selected) {
			AssetLibraryService.SetDescription(selected.ID, _editDescription);
			AddLog($"Request: SetDescription {selected.ID} -> {_editDescription}");
			RefreshAssets();
		}

		private void ApplyFolder(AssetMetadata selected) {
			if (!Ulid.TryParse(_editFolder, out var parsed)) {
				AddLog($"Failed to parse folder ULID: {_editFolder}");
				return;
			}
			AssetLibraryService.SetFolder(selected.ID, parsed);
			AddLog($"Request: SetFolder {selected.ID} -> {parsed}");
			RefreshAssets();
		}

		private void ApplyAddTag(AssetMetadata selected) {
			if (string.IsNullOrWhiteSpace(_tagInput)) return;
			AssetLibraryService.AddTag(selected.ID, _tagInput.Trim());
			AddLog($"Request: AddTag {selected.ID} -> {_tagInput.Trim()}");
			RefreshAssets();
		}

		private void ApplyRemoveTag(AssetMetadata selected) {
			if (string.IsNullOrWhiteSpace(_tagInput)) return;
			AssetLibraryService.RemoveTag(selected.ID, _tagInput.Trim());
			AddLog($"Request: RemoveTag {selected.ID} -> {_tagInput.Trim()}");
			RefreshAssets();
		}

		private void AddLog(string message) {
			_logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
			Repaint();
		}
	}
}

