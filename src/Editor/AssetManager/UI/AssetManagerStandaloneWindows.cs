using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerSeparatedWindows
    {
        [MenuItem("ee4v/Asset Manager (Separated)")]
        private static void ShowAll()
        {
            AssetManagerNavigationWindow.ShowWindow();
            AssetManagerMainWindow.ShowWindow();
            AssetManagerInformationWindow.ShowWindow();
        }
    }

    internal abstract class AssetManagerPaneWindow : EditorWindow
    {
        private AssetManagerView _view;

        protected abstract string WindowTitle { get; }
        protected abstract Vector2 MinimumSize { get; }
        protected abstract AssetManagerViewMode ViewMode { get; }

        protected void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(WindowTitle);
            minSize = MinimumSize;
        }

        protected virtual void OnEnable()
        {
            AssetManagerWindowSession.ManagerInvalidated -= CreateGUI;
            AssetManagerWindowSession.ManagerInvalidated += CreateGUI;
            I18N.Reloaded -= OnLocalizationReloaded;
            I18N.Reloaded += OnLocalizationReloaded;
            ConfigureWindow();
        }

        protected virtual void CreateGUI()
        {
            _view?.Dispose();
            rootVisualElement.Clear();
            AssetManagerWindowSession.PrepareRoot(rootVisualElement);
            _view = AssetManagerWindowSession.CreateView(ViewMode);
            rootVisualElement.Add(_view);
        }

        protected virtual void OnDisable()
        {
            AssetManagerWindowSession.ManagerInvalidated -= CreateGUI;
            I18N.Reloaded -= OnLocalizationReloaded;
            _view?.Dispose();
            _view = null;
        }

        private void OnLocalizationReloaded()
        {
            ConfigureWindow();
            CreateGUI();
        }
    }

    internal sealed class AssetManagerNavigationWindow :
        AssetManagerPaneWindow
    {
        protected override string WindowTitle =>
            I18N.Get("window.navigationTitle");
        protected override Vector2 MinimumSize =>
            new Vector2(240f, 420f);
        protected override AssetManagerViewMode ViewMode =>
            AssetManagerViewMode.Navigation;

        [MenuItem("ee4v/Window/Asset Manager Navigation")]
        internal static void ShowWindow()
        {
            var window = GetWindow<AssetManagerNavigationWindow>();
            window.ConfigureWindow();
            window.Show();
        }
    }

    internal sealed class AssetManagerMainWindow : AssetManagerPaneWindow
    {
        protected override string WindowTitle =>
            I18N.Get("window.mainTitle");
        protected override Vector2 MinimumSize =>
            new Vector2(640f, 420f);
        protected override AssetManagerViewMode ViewMode =>
            AssetManagerViewMode.Main;

        [MenuItem("ee4v/Window/Asset Manager Main")]
        internal static void ShowWindow()
        {
            var window = GetWindow<AssetManagerMainWindow>();
            window.ConfigureWindow();
            window.Show();
        }
    }

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
