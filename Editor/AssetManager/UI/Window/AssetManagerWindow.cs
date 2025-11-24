using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetManagerWindow : EditorWindow {
        private AssetViewController _assetController;
        private AssetInfo _assetInfo;
        private AssetService _assetService;

        private AssetView _assetView;

        // When a folder is previewed in the grid (single-selection preview), AssetController.SelectedFolderId
        // may be empty. Keep the previewed folder ID so tag operations can apply to previewed folders.
        private Ulid _currentPreviewFolderId = Ulid.Empty;
        private FolderService _folderService;
        private bool _isInitialized;
        private Navigation _navigation;
        private IAssetRepository _repository;
        private AssetMetadata _selectedAsset;
        private TagListView _tagListView;
        private TextureService _textureService;
        private ToastManager _toastManager;

        private void OnEnable() {
            _repository = AssetManagerContainer.Repository;
            _assetService = AssetManagerContainer.AssetService;
            _folderService = AssetManagerContainer.FolderService;
            _textureService = AssetManagerContainer.TextureService;
        }

        private void OnDisable() {
            if (_navigation != null) {
                _navigation.NavigationChanged -= OnNavigationChanged;
                _navigation.FolderSelected -= OnFolderSelected;
                _navigation.TagListClicked -= OnTagListClicked;
                _navigation.OnFolderRenamed -= OnFolderRenamed;
                _navigation.OnFolderDeleted -= OnFolderDeleted;
                _navigation.OnFolderMoved -= OnFolderMoved;
                _navigation.OnFolderCreated -= OnFolderCreated;
                _navigation.OnAssetsDroppedToFolder -= OnAssetsDroppedToFolder;
                _navigation.OnFolderReordered -= OnFolderReordered;
                _navigation.OnAssetCreated -= OnAssetCreated;
            }

            if (_tagListView != null) _tagListView.OnTagSelected -= OnTagSelected;

            if (_assetController != null) {
                _assetController.AssetSelected -= OnAssetSelected;
                _assetController.FolderPreviewSelected -= OnFolderPreviewSelected;
                _assetController.FoldersChanged -= OnFoldersChanged;
                _assetController.BoothItemFoldersChanged -= OnBoothItemFoldersChanged;
                _assetController.ModeChanged -= OnModeChanged;
                _assetController.OnHistoryChanged -= OnControllerHistoryChanged;
            }

            if (_assetView != null) _assetView.OnSelectionChange -= OnSelectionChanged;

            if (_assetInfo != null) {
                _assetInfo.OnNameChanged -= OnAssetNameChanged;
                _assetInfo.OnDescriptionChanged -= OnAssetDescriptionChanged;
                _assetInfo.OnTagAdded -= OnAssetTagAdded;
                _assetInfo.OnTagRemoved -= OnAssetTagRemoved;
                _assetInfo.OnTagClicked -= OnTagSelected;
                _assetInfo.OnFolderClicked -= OnFolderSelected;
            }

            _textureService?.ClearCache();
            _toastManager?.ClearAll();
        }

        private void CreateGUI() {
            if (_repository == null) OnEnable();

            _isInitialized = false;

            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;

            _navigation = new Navigation {
                style = {
                    width = 200,
                    minWidth = 150,
                    flexShrink = 0,
                    borderRightWidth = 1,
                    borderRightColor = ColorPreset.WindowBorder
                }
            };
            root.Add(_navigation);

            _assetView = new AssetView();
            root.Add(_assetView);

            _tagListView = new TagListView();
            root.Add(_tagListView);

            _assetInfo = new AssetInfo {
                style = {
                    width = 300,
                    minWidth = 250,
                    flexShrink = 0,
                    borderLeftWidth = 1,
                    borderLeftColor = ColorPreset.WindowBorder
                }
            };
            root.Add(_assetInfo);

            _assetController = new AssetViewController(_repository);

            _assetView.SetController(_assetController);
            _assetView.Initialize(_textureService, _repository);
            _tagListView.Initialize(_repository);
            _tagListView.SetController(_assetController);
            _tagListView.SetShowDialogCallback(ShowDialog);
            _assetInfo.Initialize(_repository, _textureService, _folderService);

            _navigation.NavigationChanged += OnNavigationChanged;
            _navigation.FolderSelected += OnFolderSelected;
            _navigation.TagListClicked += OnTagListClicked;
            _navigation.OnFolderRenamed += OnFolderRenamed;
            _navigation.OnFolderDeleted += OnFolderDeleted;
            _navigation.OnFolderMoved += OnFolderMoved;
            _navigation.OnFolderCreated += OnFolderCreated;
            _navigation.OnAssetsDroppedToFolder += OnAssetsDroppedToFolder;
            _navigation.OnFolderReordered += OnFolderReordered;
            _navigation.OnAssetCreated += OnAssetCreated;
            _navigation.SetShowDialogCallback(ShowDialog);
            _navigation.SetRepository(_repository);

            _tagListView.OnTagSelected += OnTagSelected;
            _tagListView.OnTagRenamed += OnTagRenamed;
            _tagListView.OnTagDeleted += OnTagDeleted;

            _assetController.AssetSelected += OnAssetSelected;
            _assetController.FolderPreviewSelected += OnFolderPreviewSelected;
            _assetController.FoldersChanged += OnFoldersChanged;
            _assetController.BoothItemFoldersChanged += OnBoothItemFoldersChanged;
            _assetController.ModeChanged += OnModeChanged;
            _assetController.OnHistoryChanged += OnControllerHistoryChanged;
            _assetView.OnSelectionChange += OnSelectionChanged;

            _assetInfo.OnNameChanged += OnAssetNameChanged;
            _assetInfo.OnDescriptionChanged += OnAssetDescriptionChanged;
            _assetInfo.OnTagAdded += OnAssetTagAdded;
            _assetInfo.OnTagRemoved += OnAssetTagRemoved;
            _assetInfo.OnTagClicked += OnTagSelected;
            _assetInfo.OnFolderClicked += OnFolderSelected;

            _toastManager = new ToastManager(root);

            _navigation.SelectAll();
            _assetController.Refresh();

            ShowAssetView();

            _isInitialized = true;
        }

        private void OnSelectionChanged(List<object> selectedItems) {
            _assetInfo.UpdateSelection(selectedItems);

            switch (selectedItems) {
                case { Count: 1 } when selectedItems[0] is AssetMetadata asset:
                    _selectedAsset = asset;
                    _currentPreviewFolderId = Ulid.Empty;
                    break;
                case { Count: 1 } when selectedItems[0] is BaseFolder folder:
                    _selectedAsset = null;
                    _currentPreviewFolderId = folder.ID;
                    break;
                default:
                    _selectedAsset = null;
                    _currentPreviewFolderId = Ulid.Empty;
                    break;
            }
        }

        private void OnNavigationChanged(NavigationMode mode, string contextName, Func<AssetMetadata, bool> filter) {
            _assetController.SetMode(mode, contextName, filter, _isInitialized);

            _assetView?.ClearSelection();
            _selectedAsset = null;
            _currentPreviewFolderId = Ulid.Empty;
            _assetInfo?.UpdateSelection(new List<object>());
        }

        private void OnFolderSelected(Ulid folderId) {
            _assetController.SetFolder(folderId, _isInitialized);

            if (folderId != Ulid.Empty && (_selectedAsset == null || _selectedAsset.Folder == folderId)) return;
            _assetView?.ClearSelection();
            _selectedAsset = null;
            _currentPreviewFolderId = Ulid.Empty;
            _assetInfo?.UpdateSelection(new List<object>());
        }

        private void OnTagListClicked() {
            _tagListView.Refresh();
            _assetController.SetMode(NavigationMode.TagList, "Tag List", _ => false);
        }

        private void OnTagSelected(string tag) {
            _assetController.SetMode(
                NavigationMode.Tag,
                $"Tag: {tag}",
                a => !a.IsDeleted && a.Tags.Contains(tag)
            );
        }

        private void OnModeChanged(NavigationMode mode) {
            if (mode == NavigationMode.TagList)
                ShowTagListView();
            else
                ShowAssetView();
        }

        private void OnControllerHistoryChanged() {
            _navigation?.SelectState(_assetController.CurrentMode, _assetController.SelectedFolderId);

            _assetView?.ClearSelection();
            _selectedAsset = null;
            _assetInfo?.UpdateSelection(new List<object>());
        }

        private void OnAssetSelected(AssetMetadata asset) {
            _currentPreviewFolderId = Ulid.Empty;
        }

        private void OnFolderPreviewSelected(BaseFolder folder) {
            _currentPreviewFolderId = folder?.ID ?? Ulid.Empty;
        }

        private void OnFoldersChanged(List<BaseFolder> folders) {
            _navigation.SetFolders(folders);
        }

        private void OnBoothItemFoldersChanged(List<BoothItemFolder> folders) {
            _assetView.ShowBoothItemFolders(folders);
        }

        private void OnAssetNameChanged(string newName) {
            if (_selectedAsset == null) return;
            var oldName = _selectedAsset.Name;
            var success = _assetService.SetAssetName(_selectedAsset.ID, newName);
            RefreshUI();
            if (!success)
                ShowToast($"アセット '{oldName}' のリネームに失敗しました: 名前が無効です", 10, ToastType.Error);
        }

        private void OnAssetDescriptionChanged(string newDesc) {
            if (_selectedAsset == null) return;
            _assetService.SetDescription(_selectedAsset.ID, newDesc);
            RefreshUI(false);
        }

        private void OnAssetTagAdded(string newTag) {
            if (string.IsNullOrWhiteSpace(newTag)) {
                Vector2 screenPosition;
                try {
                    screenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                }
                catch {
                    screenPosition = new Vector2(position.x + position.width / 2, position.y + position.height / 2);
                }

                TagSelectorWindow.Show(screenPosition, _repository, OnAssetTagAdded);
                return;
            }

            if (_selectedAsset != null) {
                _assetService.AddTag(_selectedAsset.ID, newTag);
            }
            else {
                var targetFolderId = Ulid.Empty;
                if (_assetController != null && _assetController.SelectedFolderId != Ulid.Empty)
                    targetFolderId = _assetController.SelectedFolderId;
                else if (_currentPreviewFolderId != Ulid.Empty)
                    targetFolderId = _currentPreviewFolderId;

                if (targetFolderId != Ulid.Empty) {
                    _folderService.AddTag(targetFolderId, newTag);
                }
                else {
                    ShowToast("タグの追加対象が見つかりませんでした", 3, ToastType.Error);
                    return;
                }
            }

            _tagListView.Refresh();
            RefreshUI(false);
        }

        private void OnAssetTagRemoved(string tagToRemove) {
            if (_selectedAsset != null) {
                _assetService.RemoveTag(_selectedAsset.ID, tagToRemove);
            }
            else {
                var targetFolderId = Ulid.Empty;
                if (_assetController != null && _assetController.SelectedFolderId != Ulid.Empty)
                    targetFolderId = _assetController.SelectedFolderId;
                else if (_currentPreviewFolderId != Ulid.Empty)
                    targetFolderId = _currentPreviewFolderId;

                if (targetFolderId != Ulid.Empty) {
                    _folderService.RemoveTag(targetFolderId, tagToRemove);
                }
                else {
                    ShowToast("タグの削除対象が見つかりませんでした", 3, ToastType.Error);
                    return;
                }
            }

            _tagListView.Refresh();
            RefreshUI(false);
        }

        private void OnTagRenamed(string oldTag, string newTag) {
            _assetService.RenameTag(oldTag, newTag);
            _tagListView.Refresh();
            RefreshUI(false);
            ShowToast($"タグ '{oldTag}' を '{newTag}' にリネームしました", 3, ToastType.Success);
        }

        private void OnTagDeleted(string tag) {
            _assetService.DeleteTag(tag);
            _tagListView.Refresh();
            RefreshUI(false);
            ShowToast($"タグ '{tag}' を削除しました", 3, ToastType.Success);
        }

        private void OnFolderRenamed(Ulid folderId, string newName) {
            var libMetadata = _repository.GetLibraryMetadata();
            var oldFolder = libMetadata?.GetFolder(folderId);
            var oldName = oldFolder?.Name ?? "フォルダ";

            var success = _folderService.SetFolderName(folderId, newName);
            var folders = _folderService.GetRootFolders();
            _navigation.SetFolders(folders);
            RefreshUI(false);
            if (!success)
                ShowToast($"フォルダ '{oldName}' のリネームに失敗しました: 名前が無効です", 10, ToastType.Error);
        }

        private void OnFolderDeleted(Ulid folderId) {
            var libMetadata = _repository.GetLibraryMetadata();
            var folder = libMetadata?.GetFolder(folderId);
            var folderName = folder?.Name ?? "フォルダ";

            _folderService.DeleteFolder(folderId);
            var folders = _folderService.GetRootFolders();
            _navigation.SetFolders(folders);
            RefreshUI(false);
            ShowToast($"フォルダ '{folderName}' を削除しました", 3, ToastType.Success);
        }

        private void OnFolderCreated(string folderName) {
            var success = _folderService.CreateFolder(Ulid.Empty, folderName);
            var folders = _folderService.GetRootFolders();
            _navigation.SetFolders(folders);
            RefreshUI(false);
            if (!success)
                ShowToast($"フォルダ '{folderName}' の作成に失敗しました: 名前が無効です", 10, ToastType.Error);
        }

        private void OnAssetCreated(string assetName, string description, string fileOrUrl, List<string> tags,
            string shopDomain, string itemId) {
            if (string.IsNullOrWhiteSpace(assetName)) {
                ShowToast("アセット名を入力してください", 5, ToastType.Error);
                return;
            }

            try {
                AssetMetadata asset;

                if (!string.IsNullOrWhiteSpace(fileOrUrl)) {
                    if (File.Exists(fileOrUrl)) {
                        _repository.CreateAssetFromFile(fileOrUrl);
                        asset = _repository.GetAllAssets().OrderByDescending(a => a.ModificationTime).FirstOrDefault();
                        if (asset == null) {
                            ShowToast("ファイルからのアセット作成に失敗しました", 5, ToastType.Error);
                            return;
                        }
                    }
                    else {
                        asset = _repository.CreateEmptyAsset();
                    }
                }
                else {
                    asset = _repository.CreateEmptyAsset();
                }

                asset.SetName(assetName);
                if (!string.IsNullOrWhiteSpace(description))
                    asset.SetDescription(description);

                if (!string.IsNullOrWhiteSpace(shopDomain) || !string.IsNullOrWhiteSpace(itemId)) {
                    var boothData = new BoothMetadata();
                    if (!string.IsNullOrWhiteSpace(shopDomain))
                        boothData.SetShopDomain(shopDomain);
                    if (!string.IsNullOrWhiteSpace(itemId))
                        boothData.SetItemID(itemId);
                    asset.SetBoothData(boothData);
                }

                if (tags is { Count: > 0 })
                    foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
                        asset.AddTag(tag);

                _repository.SaveAsset(asset);
                _assetController.Refresh();
                ShowToast($"アセット '{assetName}' を作成しました", 3, ToastType.Success);
            }
            catch (Exception ex) {
                ShowToast($"アセットの作成に失敗しました: {ex.Message}", 5, ToastType.Error);
                Debug.LogError($"Failed to create asset: {ex}");
            }
        }

        private void OnFolderMoved(Ulid sourceFolderId, Ulid targetFolderId) {
            _folderService.MoveFolder(sourceFolderId, targetFolderId);
            var folders = _folderService.GetRootFolders();
            _navigation.SetFolders(folders);
            RefreshUI(false);
        }

        private void OnFolderReordered(Ulid parentFolderId, Ulid folderId, int newIndex) {
            if (parentFolderId == Ulid.Empty) {
                var libraries = _repository.GetLibraryMetadata();
                if (libraries != null) {
                    var full = libraries.FolderList;
                    var mappedIndex = MapVisibleRootIndexToFullIndex(full, newIndex);
                    _folderService.ReorderFolder(parentFolderId, folderId, mappedIndex);
                    var mappedRootFolders = _folderService.GetRootFolders();
                    _navigation.SetFolders(mappedRootFolders);
                    RefreshUI(false);
                    return;
                }
            }

            _folderService.ReorderFolder(parentFolderId, folderId, newIndex);
            var rootFolders = _folderService.GetRootFolders();
            _navigation.SetFolders(rootFolders);
            RefreshUI(false);
        }

        private static int MapVisibleRootIndexToFullIndex(IReadOnlyList<BaseFolder> fullList, int visibleIndex) {
            if (visibleIndex < 0) return 0;

            var visibleCount = 0;
            for (var i = 0; i < fullList.Count; i++) {
                if (fullList[i] is BoothItemFolder) continue;

                if (visibleCount == visibleIndex) return i;
                visibleCount++;
            }

            if (visibleCount == 0) return 0;

            for (var i = fullList.Count - 1; i >= 0; i--)
                if (fullList[i] is not BoothItemFolder)
                    return i + 1;

            return fullList.Count;
        }

        private void OnAssetsDroppedToFolder(List<Ulid> assetIds, Ulid targetFolderId) {
            var libMetadata = _repository.GetLibraryMetadata();
            var assetsFromBoothItemFolder = (from assetId in assetIds
                select _repository.GetAsset(assetId)
                into asset
                where asset != null
                where asset.Folder != Ulid.Empty && libMetadata != null
                let currentFolder = libMetadata.GetFolder(asset.Folder)
                where currentFolder is BoothItemFolder
                select asset).ToList();

            if (assetsFromBoothItemFolder.Count > 0) {
                ShowBoothItemFolderWarningDialog(assetIds, targetFolderId, assetsFromBoothItemFolder);
                return;
            }

            foreach (var assetId in assetIds) _assetService.SetFolder(assetId, targetFolderId);
            RefreshUI();
        }

        private void RefreshUI(bool fullRefresh = true) {
            if (fullRefresh) _assetController.Refresh();

            if (_selectedAsset == null) return;
            var freshAsset = _repository.GetAsset(_selectedAsset.ID);
            _selectedAsset = freshAsset;
            _assetInfo.UpdateSelection(new List<object> { freshAsset });
        }

        private void ShowAssetView() {
            _assetView.style.display = DisplayStyle.Flex;
            _tagListView.style.display = DisplayStyle.None;
        }

        private void ShowTagListView() {
            _assetView.style.display = DisplayStyle.None;
            _tagListView.style.display = DisplayStyle.Flex;
        }

        private void ShowBoothItemFolderWarningDialog(List<Ulid> assetIds, Ulid targetFolderId,
            List<AssetMetadata> assetsFromBoothItemFolder) {
            var content = new VisualElement();

            var titleLabel = new Label("Warning") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(titleLabel);

            var messageText = assetsFromBoothItemFolder.Count == 1
                ? $"The asset '{assetsFromBoothItemFolder[0].Name}' is currently in a Booth Item folder.\n\nMoving it to a regular folder may cause issues with Booth item management. Are you sure you want to continue?"
                : $"{assetsFromBoothItemFolder.Count} assets are currently in Booth Item folders.\n\nMoving them to a regular folder may cause issues with Booth item management. Are you sure you want to continue?";

            var message = new Label(messageText) {
                style = {
                    marginBottom = 15,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            content.Add(message);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };

            var cancelBtn = new Button {
                text = "Cancel",
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var continueBtn = new Button {
                text = "Continue",
                style = {
                    backgroundColor = new Color(0.8f, 0.4f, 0.4f)
                }
            };
            buttonRow.Add(continueBtn);

            content.Add(buttonRow);

            var dialogContainer = ShowDialog(content);

            cancelBtn.clicked += () => dialogContainer?.RemoveFromHierarchy();
            continueBtn.clicked += () =>
            {
                foreach (var assetId in assetIds) _assetService.SetFolder(assetId, targetFolderId);

                RefreshUI();
                dialogContainer?.RemoveFromHierarchy();
            };
        }

        private VisualElement ShowDialog(VisualElement dialogContent) {
            var container = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    backgroundColor = new StyleColor(new Color(0, 0, 0, 0.5f)),
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            var dialog = new VisualElement {
                style = {
                    backgroundColor = ColorPreset.DefaultBackground,
                    paddingLeft = 20, paddingRight = 20, paddingTop = 20, paddingBottom = 20,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    minWidth = 300,
                    maxWidth = 500
                }
            };

            dialog.Add(dialogContent);
            container.Add(dialog);
            rootVisualElement.Add(container);

            return container;
        }

        private void ShowToast(string message, float? duration = 3f, ToastType type = ToastType.Info) {
            _toastManager?.Show(message, duration, type);
        }

        public static void ShowToastMessage(string message, float? duration = 3f, ToastType type = ToastType.Info) {
            var window = GetWindow<AssetManagerWindow>();
            window?.ShowToast(message, duration, type);
        }

        [MenuItem("ee4v/Asset Manager")]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>("Asset Manager");
            window.minSize = new Vector2(800, 400);
            AssetManagerContainer.Repository.Load();
            window.Show();
        }
    }
}