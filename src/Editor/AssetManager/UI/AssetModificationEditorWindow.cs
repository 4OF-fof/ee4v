using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal abstract class AssetModificationEditorWindow : EditorWindow
    {
        [SerializeField] private string _derivedAssetGuid;
        private AssetModificationWorkflowView _view;

        protected abstract ModificationEditorMode EditorMode { get; }
        protected abstract string TitleKey { get; }

        protected void OnEnable()
        {
            I18N.Reloaded += ConfigureWindow;
            ConfigureWindow();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get(TitleKey));
            minSize = new Vector2(960f, 640f);
        }

        protected void CreateGUI()
        {
            _view?.Dispose();
            rootVisualElement.Clear();
            AssetManagerWindowSession.PrepareWorkflowRoot(rootVisualElement);
            _view = new AssetModificationWorkflowView(
                EditorMode, Repaint, AssetManagerLibraryWindow.ShowWindow);
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

        protected void OnDisable()
        {
            I18N.Reloaded -= ConfigureWindow;
            _view?.Dispose();
            _view = null;
        }
    }
}
