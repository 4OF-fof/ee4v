using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetModificationWorkflowWindow : EditorWindow
    {
        [SerializeField] private string _derivedAssetGuid;
        private AssetModificationWorkflowView _view;

        [MenuItem("ee4v/ee4v", false, 0)]
        private static void ShowWindow()
        {
            var window = GetWindow<AssetModificationWorkflowWindow>();
            window.ConfigureWindow();
            window.Show();
        }

        internal static void ShowFor(DerivedAssetInfo asset)
        {
            if (asset?.Prefab == null)
            {
                return;
            }
            var window = GetWindow<AssetModificationWorkflowWindow>();
            if (window._view == null)
            {
                window.CreateGUI();
            }
            window._view.SelectDerivedAsset(asset);
            window.Show();
        }

        private void OnEnable()
        {
            I18N.Reloaded += ConfigureWindow;
            ConfigureWindow();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get("workflow.windowTitle"));
            minSize = new Vector2(1180f, 720f);
        }

        private void CreateGUI()
        {
            _view?.Dispose();
            rootVisualElement.Clear();
            AssetManagerWindowSession.PrepareWorkflowRoot(rootVisualElement);
            _view = new AssetModificationWorkflowView(
                ModificationEditorMode.All, Repaint, AssetManagerLibraryWindow.ShowWindow);
            _view.DerivedAssetChanged += RememberDerivedAsset;
            rootVisualElement.Add(_view);
            var asset = DerivedAssetCreator.FindAll().FirstOrDefault(candidate =>
                candidate.Prefab != null && AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(candidate.Prefab)) == _derivedAssetGuid);
            if (asset != null)
            {
                _view.SelectDerivedAsset(asset);
            }
        }

        private void RememberDerivedAsset(DerivedAssetInfo asset)
        {
            _derivedAssetGuid = asset?.Prefab == null ? string.Empty :
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset.Prefab));
        }

        private void OnDisable()
        {
            I18N.Reloaded -= ConfigureWindow;
            _view?.Dispose();
            _view = null;
        }
    }
}
