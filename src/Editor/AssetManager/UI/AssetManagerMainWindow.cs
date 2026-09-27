using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerMainWindow : AssetManagerPaneWindow
    {
        protected override string WindowTitle =>
            I18N.Get("window.mainTitle");
        protected override Vector2 MinimumSize =>
            new Vector2(640f, 420f);
        protected override AssetManagerViewMode ViewMode =>
            AssetManagerViewMode.Main;

        [MenuItem("ee4v/Window/Asset Manager/Panes/Main", false, 121)]
        internal static void ShowWindow()
        {
            var window = GetWindow<AssetManagerMainWindow>();
            window.ConfigureWindow();
            window.Show();
        }
    }
}
