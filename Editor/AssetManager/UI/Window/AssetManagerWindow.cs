using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.Core.UI;
using UnityEditor;
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
                    width = 280,
                    minWidth = 200,
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
            _assetController.Refresh();
            
            ShowAssetView();
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
            window.minSize = new UnityEngine.Vector2(800, 400);
            AssetManagerContainer.Repository.Load();
            window.Show();
        }
    }
}