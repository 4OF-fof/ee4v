using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerLibraryWindow : EditorWindow
    {
        private AssetManagerWorkspaceView _view;

        [MenuItem("ee4v/Window/Asset Manager/Asset Manager", false, 100)]
        internal static void ShowWindow()
        {
            GetWindow<AssetManagerLibraryWindow>().Show();
        }

        private void OnEnable()
        {
            I18N.Reloaded += Rebuild;
            ConfigureWindow();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
            minSize = new Vector2(1040f, 640f);
        }

        private void CreateGUI()
        {
            _view?.Dispose();
            rootVisualElement.Clear();
            AssetManagerWindowSession.PrepareWorkflowRoot(rootVisualElement);
            _view = new AssetManagerWorkspaceView(null);
            rootVisualElement.Add(_view);
        }

        private void Rebuild()
        {
            ConfigureWindow();
            CreateGUI();
        }

        private void OnDisable()
        {
            I18N.Reloaded -= Rebuild;
            _view?.Dispose();
            _view = null;
        }
    }
}
