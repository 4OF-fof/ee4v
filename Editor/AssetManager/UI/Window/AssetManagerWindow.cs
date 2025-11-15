using UnityEditor;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetManagerWindow: EditorWindow {
        [MenuItem("ee4v/Asset Manager")]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>("Asset Manager");
            window.Show();
        }
    }
}