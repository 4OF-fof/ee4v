using System;
using System.Collections.Generic;
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
        private FolderService _folderService;
        private bool _isInitialized;
        private Navigation _navigation;
        private IAssetRepository _repository;
        private AssetMetadata _selectedAsset;
        private TagListView _tagListView;
        private TextureService _textureService;

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
            }

            if (_tagListView != null) _tagListView.OnTagSelected -= OnTagSelected;

            if (_assetController != null) {
                _assetController.AssetSelected -= OnAssetSelected;
                _assetController.FolderPreviewSelected -= OnFolderPreviewSelected;
                _assetController.FoldersChanged -= OnFoldersChanged;
                _assetController.BoothItemFoldersChanged -= OnBoothItemFoldersChanged;
                _assetController.ModeChanged -= OnModeChanged;
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
            _assetView.Initialize(_textureService);
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
            _navigation.SetShowDialogCallback(ShowDialog);

            _tagListView.OnTagSelected += OnTagSelected;
            _tagListView.OnTagRenamed += OnTagRenamed;
            _tagListView.OnTagDeleted += OnTagDeleted;

            _assetController.AssetSelected += OnAssetSelected;
            _assetController.FolderPreviewSelected += OnFolderPreviewSelected;
            _assetController.FoldersChanged += OnFoldersChanged;
            _assetController.BoothItemFoldersChanged += OnBoothItemFoldersChanged;
            _assetController.ModeChanged += OnModeChanged;

            _assetView.OnSelectionChange += OnSelectionChanged;

            _assetInfo.OnNameChanged += OnAssetNameChanged;
            _assetInfo.OnDescriptionChanged += OnAssetDescriptionChanged;
            _assetInfo.OnTagAdded += OnAssetTagAdded;
            _assetInfo.OnTagRemoved += OnAssetTagRemoved;
            _assetInfo.OnTagClicked += OnTagSelected;
            _assetInfo.OnFolderClicked += OnFolderSelected;

            _navigation.SelectAll();
            _assetController.Refresh();

            ShowAssetView();

            _isInitialized = true;
        }

        private void OnSelectionChanged(List<object> selectedItems) {
            _assetInfo.UpdateSelection(selectedItems);

            if (selectedItems is { Count: 1 } && selectedItems[0] is AssetMetadata asset)
                _selectedAsset = asset;
            else
                _selectedAsset = null;
        }

        private void OnNavigationChanged(NavigationMode mode, string contextName, Func<AssetMetadata, bool> filter) {
            _assetController.SetMode(mode, contextName, filter, _isInitialized);
        }

        private void OnFolderSelected(Ulid folderId) {
            _assetController.SetFolder(folderId, _isInitialized);
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

        private void OnAssetSelected(AssetMetadata asset) {
        }

        private void OnFolderPreviewSelected(BaseFolder folder) {
        }

        private void OnFoldersChanged(List<BaseFolder> folders) {
            _navigation.SetFolders(folders);
        }

        private void OnBoothItemFoldersChanged(List<BoothItemFolder> folders) {
            _assetView.ShowBoothItemFolders(folders);
        }

        private void OnAssetNameChanged(string newName) {
            if (_selectedAsset == null) return;
            _assetService.SetAssetName(_selectedAsset.ID, newName);
            RefreshUI();
        }

        private void OnAssetDescriptionChanged(string newDesc) {
            if (_selectedAsset == null) return;
            _assetService.SetDescription(_selectedAsset.ID, newDesc);
            RefreshUI(false);
        }

        private void OnAssetTagAdded(string newTag) {
            if (_selectedAsset == null) return;
            _assetService.AddTag(_selectedAsset.ID, newTag);
            _tagListView.Refresh();
            RefreshUI(false);
        }

        private void OnAssetTagRemoved(string tagToRemove) {
            if (_selectedAsset == null) return;
            _assetService.RemoveTag(_selectedAsset.ID, tagToRemove);
            _tagListView.Refresh();
            RefreshUI(false);
        }

        private void OnTagRenamed(string oldTag, string newTag) {
            _assetService.RenameTag(oldTag, newTag);
            _tagListView.Refresh();
            RefreshUI(false);
        }

        private void OnTagDeleted(string tag) {
            _assetService.DeleteTag(tag);
            _tagListView.Refresh();
            RefreshUI(false);
        }

        private void OnFolderRenamed(Ulid folderId, string newName) {
            _folderService.RenameFolder(folderId, newName);
            var folders = _folderService.GetFlatFolders();
            _navigation.SetFolders(folders);
            RefreshUI(false);
        }

        private void OnFolderDeleted(Ulid folderId) {
            _folderService.DeleteFolder(folderId);
            var folders = _folderService.GetFlatFolders();
            _navigation.SetFolders(folders);
            RefreshUI(false);
        }

        private void OnFolderCreated(string folderName) {
            _folderService.CreateFolder(Ulid.Empty, folderName);
            var folders = _folderService.GetFlatFolders();
            _navigation.SetFolders(folders);
            RefreshUI(false);
        }

        private void OnFolderMoved(Ulid sourceFolderId, Ulid targetFolderId) {
            _folderService.MoveFolder(sourceFolderId, targetFolderId);
            var folders = _folderService.GetFlatFolders();
            _navigation.SetFolders(folders);
            RefreshUI(false);
        }

        private void RefreshUI(bool fullRefresh = true) {
            if (fullRefresh) _assetController.Refresh();

            if (_selectedAsset == null) return;
            var freshAsset = _repository.GetAsset(_selectedAsset.ID);
            _selectedAsset = freshAsset;
            _assetInfo.SetAsset(freshAsset);
        }

        private void ShowAssetView() {
            _assetView.style.display = DisplayStyle.Flex;
            _tagListView.style.display = DisplayStyle.None;
        }

        private void ShowTagListView() {
            _assetView.style.display = DisplayStyle.None;
            _tagListView.style.display = DisplayStyle.Flex;
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
                    minWidth = 300
                }
            };

            dialog.Add(dialogContent);
            container.Add(dialog);
            rootVisualElement.Add(container);

            return container;
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