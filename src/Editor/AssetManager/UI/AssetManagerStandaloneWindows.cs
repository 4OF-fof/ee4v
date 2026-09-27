using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerSeparatedWindows
    {
        [MenuItem("ee4v/Window/Asset Manager/Open All", false, 101)]
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
}
