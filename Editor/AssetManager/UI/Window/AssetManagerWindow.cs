using _4OF.ee4v.AssetManager.Service;
using UnityEditor;
using UnityEngine.UIElements;
using _4OF.ee4v.AssetManager.UI.Window._Component;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetManagerWindow : EditorWindow {
        private Navigation _navigation;
        private AssetView _assetView;
        private AssetInfo _assetInfo;
        private AssetViewController _assetController;

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
            _navigation.ModeChanged += mode => { _assetController.SetMode(mode); };
            _assetController.AssetSelected += asset => { _assetInfo.SetAsset(asset); };

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