using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.WindowGroup
{
    internal sealed class WindowGroupSettingsWindow : EditorWindow
    {
        private const float MinimumWidth = 640f;
        private const float MinimumHeight = 400f;
        private WindowGroupSettingsView _view;

        [MenuItem("ee4v/Window/Window Groups")]
        private static void Open()
        {
            var window = GetWindow<WindowGroupSettingsWindow>();
            window.ConfigureWindow();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            ConfigureWindow();
        }

        private void OnDisable()
        {
            _view?.Dispose();
            _view = null;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/Feature/WindowGroup/UI/window-group-settings.uss");
            _view?.Dispose();
            _view = new WindowGroupSettingsView(
                WindowGroupBootstrap.Settings);
            root.Add(_view);
        }

        private void OnFocus()
        {
            _view?.Refresh();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("window.title"));
            minSize = new Vector2(MinimumWidth, MinimumHeight);
        }
    }
}
