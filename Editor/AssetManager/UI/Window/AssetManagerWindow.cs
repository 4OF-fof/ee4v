using System;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using UnityEditor;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetManagerWindow : EditorWindow {
        private AssetViewController _assetController;
        private AssetInfo _assetInfo;
        private AssetView _assetView;
        private Navigation _navigation;
        private Action _onAssetLibraryLoadedHandler;
        private TagListView _tagListView;

        private void OnDisable() {
            if (_onAssetLibraryLoadedHandler != null)
                AssetLibraryService.AssetLibraryLoaded -= _onAssetLibraryLoadedHandler;
        }

        private void CreateGUI() {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            _navigation = new Navigation {
                style = {
                    width = 220,
                    flexShrink = 0
                }
            };
            root.Add(_navigation);

            _assetView = new AssetView {
                style = {
                    flexGrow = 1
                }
            };
            root.Add(_assetView);

            _tagListView = new TagListView {
                style = {
                    flexGrow = 1,
                    display = DisplayStyle.None
                }
            };
            root.Add(_tagListView);

            _assetInfo = new AssetInfo {
                style = {
                    width = 260,
                    flexShrink = 0
                }
            };
            root.Add(_assetInfo);

            _assetController = new AssetViewController();
            _assetView.SetController(_assetController);

            _navigation.FilterChanged += predicate =>
            {
                _assetController.SetFilter(predicate);
                ShowAssetView();
            };
            _navigation.FolderSelected += folderId =>
            {
                _assetController.SelectFolder(folderId);
                ShowAssetView();
            };
            _navigation.BoothItemClicked += () =>
            {
                _assetController.ShowBoothItemFolders();
                ShowAssetView();
            };
            _navigation.TagListClicked += () =>
            {
                _tagListView.Refresh();
                ShowTagListView();
            };

            _tagListView.OnTagSelected += tag =>
            {
                _assetController.SetFilter(a => !a.IsDeleted && a.Tags.Contains(tag));
                ShowAssetView();
            };

            _assetController.AssetSelected += asset => { _assetInfo.SetAsset(asset); };
            _assetController.FoldersChanged += folders => { _navigation.SetFolders(folders); };
            _assetController.BoothItemFoldersChanged += folders => { _assetView.ShowBoothItemFolders(folders); };

            _navigation.SelectAll();

            _onAssetLibraryLoadedHandler = () => _assetController.Refresh();
            AssetLibraryService.AssetLibraryLoaded += _onAssetLibraryLoadedHandler;
            if (AssetLibrary.Instance?.Libraries != null) _assetController.Refresh();
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
            AssetLibraryService.LoadAssetLibrary();
            window.Show();
        }
    }
}