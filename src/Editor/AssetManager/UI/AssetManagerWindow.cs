using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerWindow : EditorWindow
    {
        private AssetManagerView _view;

        [MenuItem("ee4v/Asset Manager")]
        private static void ShowWindow()
        {
            var window = GetWindow<AssetManagerWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("window.title"));
            window.minSize = new Vector2(
                UiSizeTokens.WindowMinWidth,
                UiSizeTokens.WindowMinHeight);
            window.Show();
        }

        private void CreateGUI()
        {
            _view?.Dispose();
            rootVisualElement.Clear();
            AssetManagerWindowSession.PrepareRoot(rootVisualElement);
            _view = AssetManagerWindowSession.CreateView(
                AssetManagerViewMode.Combined);
            rootVisualElement.Add(_view);
        }

        private void OnEnable()
        {
            AssetManagerWindowSession.ManagerInvalidated -= CreateGUI;
            AssetManagerWindowSession.ManagerInvalidated += CreateGUI;
            I18N.Reloaded -= OnLocalizationReloaded;
            I18N.Reloaded += OnLocalizationReloaded;
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("window.title"));
            minSize = new Vector2(
                UiSizeTokens.WindowMinWidth,
                UiSizeTokens.WindowMinHeight);
        }

        private void OnDisable()
        {
            AssetManagerWindowSession.ManagerInvalidated -= CreateGUI;
            I18N.Reloaded -= OnLocalizationReloaded;
            _view?.Dispose();
            _view = null;
        }

        private void OnLocalizationReloaded()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("window.title"));
            CreateGUI();
        }

    }
}
