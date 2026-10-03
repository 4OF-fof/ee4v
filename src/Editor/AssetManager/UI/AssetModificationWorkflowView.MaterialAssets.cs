using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using static Ee4v.AvatarEditing.AvatarEditingUi;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Ee4v.AssetProtection;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Simulation;
using Ee4v.Core.Settings;
using Ee4v.FaceExpression;
using Ee4v.AvatarParts;
using Ee4v.AvatarMaterials;
using Ee4v.AvatarInfo;
using static Ee4v.AvatarParts.AvatarPartsEditor;
using AppearancePanel = Ee4v.AvatarEditing.AvatarEditorPanel;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView
    {
        private VisualElement BuildAppearanceControls()
        {
            return _currentCategory == WorkflowCategory.Material
                ? _materials.BuildControls() : _parts.BuildControls();
        }

        private void AddFeedback(VisualElement panel)
        {
            if (string.IsNullOrWhiteSpace(_avatarContext.Feedback))
            {
                return;
            }
            panel.Add(UiTextFactory.CreateHelpBox(
                _avatarContext.Feedback,
                _avatarContext.FeedbackType,
                "ee4v-modification-workflow__feedback"));
        }

        private VisualElement CreateEmptyState(
            string titleKey,
            string descriptionKey)
        {
            var empty = new VisualElement();
            empty.AddToClassList("ee4v-ui-empty-state");
            empty.AddToClassList(
                "ee4v-modification-workflow__empty-state");
            empty.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle,
                "ee4v-ui-empty-state__title"));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText,
                "ee4v-ui-empty-state__description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            empty.Add(description);
            return empty;
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
                var prefabPath = GetWorkingAssetPath();
                var variantFolder = System.IO.Path
                    .GetDirectoryName(prefabPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(variantFolder))
                {
                    throw new InvalidOperationException(
                        "The derived asset folder could not be found.");
                }
                var assetsFolder = variantFolder + "/Assets";
                if (!AssetDatabase.IsValidFolder(assetsFolder))
                {
                    AssetDatabase.CreateFolder(variantFolder, "Assets");
                }
                var materialsFolder = assetsFolder + "/Materials";
                if (!AssetDatabase.IsValidFolder(materialsFolder))
                {
                    AssetDatabase.CreateFolder(assetsFolder, "Materials");
                }
                var safeName = new string(sourceMaterial.name
                    .Select(character =>
                        char.IsLetterOrDigit(character) ||
                        character == ' ' ||
                        character == '_' ||
                        character == '-'
                            ? character
                            : '_')
                    .Take(80)
                    .ToArray());
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    safeName = "Material";
                }
                materialPath = AssetDatabase.GenerateUniqueAssetPath(
                    materialsFolder + "/" + safeName + ".mat");
                variant = new Material(sourceMaterial)
                {
                    name = sourceMaterial.name,
                    parent = sourceMaterial,
                    hideFlags = HideFlags.None
                };
                AssetDatabase.CreateAsset(variant, materialPath);
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
                _avatarContext.FeedbackType = HelpBoxMessageType.Error;
                ShowCategory(WorkflowCategory.Material, false);
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
            InvalidateVariantSaveStatus();
            ClearAppearanceCaches();
            if (_currentCategory != WorkflowCategory.ShapeParts ||
                _parts.Section != ShapePartsSection.Shape)
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
