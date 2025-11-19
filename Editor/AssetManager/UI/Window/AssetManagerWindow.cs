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

            _assetInfo = new AssetInfo {
                style = {
                    width = 260,
                    flexShrink = 0
                }
            };
            root.Add(_assetInfo);

            _assetController = new AssetViewController();
            _assetView.SetController(_assetController);

            _navigation.FilterChanged += predicate => { _assetController.SetFilter(predicate); };
            _navigation.FolderSelected += folderId => { _assetController.SelectFolder(folderId); };

            _assetController.AssetSelected += asset => { _assetInfo.SetAsset(asset); };
            _assetController.FoldersChanged += folders => { _navigation.SetFolders(folders); };

            _navigation.SelectAll();
        }

        [MenuItem("ee4v/Asset Manager")]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>("Asset Manager");
            AssetLibraryService.LoadAssetLibrary();
            window.Show();
        }
    }
}