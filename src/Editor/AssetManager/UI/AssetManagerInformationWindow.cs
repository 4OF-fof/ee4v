using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerInformationWindow :
        AssetManagerPaneWindow
    {
        protected override string WindowTitle =>
            I18N.Get("window.informationTitle");
        protected override Vector2 MinimumSize =>
            new Vector2(300f, 420f);
        protected override AssetManagerViewMode ViewMode =>
            AssetManagerViewMode.Information;

        [MenuItem("ee4v/Window/Asset Manager Information")]
        internal static void ShowWindow()
        {
            var window = GetWindow<AssetManagerInformationWindow>();
            window.ConfigureWindow();
            window.Show();
        }
    }
}
