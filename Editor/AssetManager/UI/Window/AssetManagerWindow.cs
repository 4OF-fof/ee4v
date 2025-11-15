using _4OF.ee4v.AssetManager.Service;
using UnityEditor;
using _4OF.ee4v.AssetManager.UI.Window._Component;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetManagerWindow: EditorWindow {
        private MainContent _mainContent;
        private MainContentController _mainContentController;
        [MenuItem("ee4v/Asset Manager")]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>("Asset Manager");
            AssetLibraryService.LoadAssetLibrary();
            window.Show();
        }

        private void CreateGUI() {
            rootVisualElement.Clear();
            var toolbar = new Toolbar();
            rootVisualElement.Add(toolbar);

            _mainContent = new MainContent();
            rootVisualElement.Add(_mainContent);

            _mainContentController = new MainContentController(_mainContent);
            toolbar.requestRefresh = _mainContentController.RefreshLibrary;
            toolbar.requestFilter = _mainContentController.SetTextFilter;

            _mainContentController.Refresh();
        }

        
    }
}