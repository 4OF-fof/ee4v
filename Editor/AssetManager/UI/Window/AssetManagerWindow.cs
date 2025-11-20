using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.Core.UI;
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
        private Navigation _navigation;
        private IAssetRepository _repository;

        private AssetMetadata _selectedAsset;
        private TagListView _tagListView;

        private void OnEnable() {
            _repository = AssetManagerContainer.Repository;
            _assetService = AssetManagerContainer.AssetService;
            _folderService = AssetManagerContainer.FolderService;
        }

        private void CreateGUI() {
            if (_repository == null) OnEnable();

            var root = rootVisualElement;
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
            _navigation.Initialize(_repository);
            _tagListView.Initialize(_repository);

            _navigation.NavigationChanged += (rootName, filter, isBoothMode) =>
            {
                _assetController.SetRootContext(rootName, isBoothMode);
                _assetController.SetFilter(filter);
                ShowAssetView();
            };

            _navigation.FolderSelected += folderId =>
            {
                _assetController.SelectFolder(folderId);
                ShowAssetView();
            };

            _navigation.TagListClicked += () =>
            {
                _tagListView.Refresh();
                ShowTagListView();
            };

            _tagListView.OnTagSelected += tag =>
            {
                _assetController.SetRootContext($"Tag: {tag}");
                _assetController.SetFilter(a => !a.IsDeleted && a.Tags.Contains(tag));
                ShowAssetView();
            };

            _assetController.AssetSelected += asset =>
            {
                _selectedAsset = asset;
                _assetInfo.SetAsset(asset);
            };
            _assetController.FolderPreviewSelected += folder => { _assetInfo.SetFolder(folder); };
            _assetController.FoldersChanged += folders => { _navigation.SetFolders(folders); };
            _assetController.BoothItemFoldersChanged += folders => { _assetView.ShowBoothItemFolders(folders); };

            _assetInfo.OnNameChanged += newName =>
            {
                if (_selectedAsset == null) return;
                _assetService.SetAssetName(_selectedAsset.ID, newName);
                RefreshUI();
            };
            _assetInfo.OnDescriptionChanged += newDesc =>
            {
                if (_selectedAsset == null) return;
                _assetService.SetDescription(_selectedAsset.ID, newDesc);
                RefreshUI(false);
            };
            _assetInfo.OnTagAdded += newTag =>
            {
                if (_selectedAsset == null) return;
                _assetService.AddTag(_selectedAsset.ID, newTag);
                RefreshUI(false);
            };
            _assetInfo.OnTagRemoved += tagToRemove =>
            {
                if (_selectedAsset == null) return;
                _assetService.RemoveTag(_selectedAsset.ID, tagToRemove);
                RefreshUI(false);
            };

            _navigation.SelectAll();
            _assetController.Refresh();

            ShowAssetView();
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

        [MenuItem("ee4v/Asset Manager")]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>("Asset Manager");
            window.minSize = new Vector2(800, 400);
            AssetManagerContainer.Repository.Load();
            window.Show();
        }
    }
}