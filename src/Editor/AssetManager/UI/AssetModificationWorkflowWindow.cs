using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetModificationWorkflowWindow : EditorWindow
    {
        [SerializeField] private string _derivedAssetGuid;
        [SerializeField] private WorkflowCategory _editingCategory;
        private AssetModificationWorkflowView _view;

        [MenuItem("ee4v/ee4v", false, 0)]
        private static void ShowWindow()
        {
            var window = GetWindow<AssetModificationWorkflowWindow>();
            window.ConfigureWindow();
            window.Show();
        }

        private void OnEnable()
        {
            I18N.Reloaded += ConfigureWindow;
            ConfigureWindow();
        }

        private void ConfigureWindow()
        {
            wantsMouseMove = true;
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get("workflow.windowTitle"));
            minSize = new Vector2(1180f, 720f);
        }

        private void CreateGUI()
        {
            _view?.Dispose();
            rootVisualElement.Clear();
            AssetManagerWindowSession.PrepareWorkflowRoot(rootVisualElement);
            _view = new AssetModificationWorkflowView(Repaint);
            _view.DerivedAssetChanged += RememberDerivedAsset;
            rootVisualElement.Add(_view);
            var asset = DerivedAssetCreator.FindAll().FirstOrDefault(candidate =>
                candidate.Prefab != null && AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(candidate.Prefab)) == _derivedAssetGuid);
            if (asset != null)
            {
                _view.SelectDerivedAsset(asset);
                _view.RestoreEditingCategory(_editingCategory);
            }
        }

        private void RememberDerivedAsset(DerivedAssetInfo asset)
        {
            _derivedAssetGuid = asset?.Prefab == null ? string.Empty :
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset.Prefab));
        }

        private void OnDisable()
        {
            _editingCategory = _view?.EditingCategory ?? _editingCategory;
            I18N.Reloaded -= ConfigureWindow;
            _view?.Dispose();
            _view = null;
        }
    }
}
