using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private static void AddCatalogStyleSheets(VisualElement root)
        {
            EnsureCatalogRegistrations();
            for (var i = 0; i < RegisteredStyleSheetPaths.Count; i++)
            {
                UiStyleUtility.AddPackageStyleSheet(root, RegisteredStyleSheetPaths[i]);
            }
        }
    }
}
