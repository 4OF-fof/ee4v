using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerNavigationWindow :
        AssetManagerPaneWindow
    {
        protected override string WindowTitle =>
            I18N.Get("window.navigationTitle");
        protected override Vector2 MinimumSize =>
            new Vector2(240f, 420f);
        protected override AssetManagerViewMode ViewMode =>
            AssetManagerViewMode.Navigation;

        [MenuItem("ee4v/Window/Asset Manager/Navigation")]
        internal static void ShowWindow()
        {
            var window = GetWindow<AssetManagerNavigationWindow>();
            window.ConfigureWindow();
            window.Show();
        }
    }
}
