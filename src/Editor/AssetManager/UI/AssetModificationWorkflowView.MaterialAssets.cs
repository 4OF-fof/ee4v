using System;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Ee4v.AssetProtection;
using AppearancePanel = Ee4v.AvatarEditing.AvatarEditorPanel;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView
    {
        private VisualElement BuildAppearanceControls()
        {
            return _appearancePanel == AppearancePanel.Material
                ? _materials.BuildControls() : _parts.BuildControls();
        }

        private void CreateEditableMaterialVariant(Material sourceMaterial)
        {
            if (!IsEditableWorkflowPrefab() || sourceMaterial == null)
            {
                return;
            }
            Material variant = null;
            string materialPath = null;
            var assigned = false;
            try
            {
                variant = DerivedAssetCreator.CreateMaterialVariantAsset(
                    GetWorkingAssetPath(), sourceMaterial, out materialPath);
                AssetDatabase.SaveAssets();
                _materials.ReplaceMaterialAssignments(sourceMaterial, variant);
                assigned = true;
                _materials.RefreshAfterMaterialReplacement(sourceMaterial, variant);
            }
            catch (Exception exception)
            {
                if (!assigned && variant != null)
                {
                    if (AssetDatabase.Contains(variant))
                    {
                        AssetDatabase.DeleteAsset(materialPath);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(variant);
                    }
                }
                Debug.LogException(exception);
                _avatarContext.Feedback = I18N.Get(
                    "workflow.appearance.materialVariantFailed");
                _avatarContext.FeedbackType = MessageSeverity.Error;
                ShowAppearancePanel(AppearancePanel.Material);
            }
        }

        private bool IsEditableWorkflowMaterial(Material material)
        {
            if (material == null || _avatarContext.Root == null)
            {
                return false;
            }
            var path = AssetDatabase.GetAssetPath(material);
            var prefabPath = GetWorkingAssetPath();
            var variantFolder = System.IO.Path
                .GetDirectoryName(prefabPath)?.Replace('\\', '/');
            return !string.IsNullOrEmpty(path) &&
                   !string.IsNullOrEmpty(variantFolder) &&
                   path.StartsWith(variantFolder + "/",
                       StringComparison.OrdinalIgnoreCase) &&
                   (material.hideFlags & HideFlags.NotEditable) == 0 &&
                   !AssetProtectionModule.IsProtected(path) &&
                   AssetDatabase.IsOpenForEdit(material, StatusQueryOptions.UseCachedIfPossible) &&
                   !_materials.IsMaterialSharedOutsideSelectedPrefab(material);
        }

        private void RefreshAfterUndoRedo()
        {
            if (_disposed || _avatarContext.Root == null) { return; }
            InvalidateVariantSaveStatus();
            ClearAppearanceCaches();
            if (IsEditableWorkflowPrefab() &&
                !ReadAssetChildren().Select(child => child.SiblingIndex)
                    .SequenceEqual(_avatarContext.PrefabSiblingIndices))
            {
                _workingSceneDirty = true;
                _avatarContext.SelectedPrefabSiblingIndex = null;
                _avatarContext.SelectedPrefabName = string.Empty;
                _avatarContext.HiddenPrefabSiblingIndices.Clear();
                _prefabPreviewVisibilityInitialized = false;
                _parts.InvalidateShapeTargets();
                BuildWindow();
                return;
            }
            if (_currentCategory != WorkflowCategory.Appearance ||
                _appearancePanel != AppearancePanel.Shape)
            {
                _materials.RefreshMaterialPreview();
                ShowCategory(_currentCategory, false);
                return;
            }

            if (IsEditableWorkflowPrefab()) { _workingSceneDirty = true; }

            _avatarContext.Preview?.ReloadPrefab();
            if (_avatarContext.ControlsHost == null)
            {
                return;
            }
            ShowCategory(_currentCategory, false);
        }

    }
}
