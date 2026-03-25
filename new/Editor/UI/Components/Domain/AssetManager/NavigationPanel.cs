using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class NavigationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--navigation";

        public NavigationPanel()
        {
            AssetManagerPanelFactory.Populate(
                this,
                RootClassName,
                AssetManagerPanelFactory.CreateCard(
                    "AssetManager",
                    "Navigation",
                    "カテゴリ、ソース、保存済みコレクションを切り替える領域です。",
                    "All Assets",
                    "Favorites",
                    "Booth Library",
                    "Packages"),
                AssetManagerPanelFactory.CreateCard(
                    "Collections",
                    "Saved Views",
                    "よく使う絞り込みや作業セットをここに配置します。",
                    "Recent Imports",
                    "Needs Review",
                    "Ready To Publish"));
        }
    }
}
