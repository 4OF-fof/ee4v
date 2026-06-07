using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private static void AddCatalogStyleSheets(VisualElement root)
        {
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/single-select-button-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/context-menu-window.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/DataView/searchable-tree-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/Testing/test-result-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/AssetManager/asset-manager-window-layout.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/AssetManager/panels.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/AssetManager/toolbar.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/item-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/DataView/item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/info-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/tab-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/alerts.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/status-badge.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/copyable-text-area.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/window-toast.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Catalog/catalog-window.uss");
        }
    }
}
