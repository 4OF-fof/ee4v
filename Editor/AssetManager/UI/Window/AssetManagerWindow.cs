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
        private TagListView _tagListView;

        // 依存するサービスとリポジトリ
        private AssetService _assetService;
        private FolderService _folderService;
        private IAssetRepository _repository;

        private void OnEnable() {
            // コンテナからシングルトンインスタンスを取得
            _repository = AssetManagerContainer.Repository;
            _assetService = AssetManagerContainer.AssetService;
            _folderService = AssetManagerContainer.FolderService;
        }

        private void CreateGUI() {
            // 初期化がまだならロード（コンパイル直後など）
            if (_repository == null) OnEnable();

            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            _navigation = new Navigation();
            root.Add(_navigation);

            _assetView = new AssetView();
            root.Add(_assetView);

            _tagListView = new TagListView();
            root.Add(_tagListView);

            _assetInfo = new AssetInfo();
            root.Add(_assetInfo);

            // Controllerに依存性を注入して初期化
            _assetController = new AssetViewController(_repository);
            
            // 各ViewにControllerやServiceをセット
            _assetView.SetController(_assetController);
            _navigation.Initialize(_repository); // フォルダ一覧取得用
            _tagListView.Initialize(_repository); // タグ一覧取得用

            // イベント購読
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

            // 初期表示
            _navigation.SelectAll();
            _assetController.Refresh();
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
            // データのロードをトリガー
            AssetManagerContainer.Repository.Load();
            window.Show();
        }
    }
}