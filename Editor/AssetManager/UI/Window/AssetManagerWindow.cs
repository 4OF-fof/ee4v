using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.Data;
using UnityEditor;
using _4OF.ee4v.AssetManager.UI.Window._Component;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetManagerWindow: EditorWindow {
        private MainContent _mainContent;
        [MenuItem("ee4v/Asset Manager")]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>("Asset Manager");
            AssetLibraryService.LoadAssetLibrary();
            window.Show();
        }

        private void CreateGUI() {
            rootVisualElement.Clear();
            var toolbar = new Toolbar {
                requestRefresh = RefreshLibrary
            };
            rootVisualElement.Add(toolbar);

            _mainContent = new MainContent();
            rootVisualElement.Add(_mainContent);

            RefreshContents();
        }

        private void RefreshContents() {
            _mainContent.RefreshContents(AssetLibrary.Instance.Assets);
        }

        private void RefreshLibrary() {
            AssetLibraryService.RefreshAssetLibrary();
            RefreshContents();
        }
    }
}