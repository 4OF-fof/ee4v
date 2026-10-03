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
        private void SyncPreviewSelection()
        {
            if (_avatarContext.Preview == null)
            {
                return;
            }
            if (_mode == ModificationEditorMode.Composition)
            {
                _avatarContext.Preview.SetListSelection(null, null);
            }
            else if (_currentCategory == WorkflowCategory.Material)
            {
                _avatarContext.Preview.SetListSelection(null, _avatarContext.SelectedMaterial);
            }
            else if (_currentCategory == WorkflowCategory.ShapeParts &&
                     _parts.Section == ShapePartsSection.Parts)
            {
                _avatarContext.Preview.SetListSelection(_avatarContext.SelectedPartKey, null);
            }
            else
            {
                _avatarContext.Preview.SetListSelection(null, null);
            }
        }

        private VisualElement BuildAssetChildCard(AssetChildEntry child)
        {
            var card = CreateHeaderPrefabCard(
                child.Name,
                SelectionTabVariant.Default);
            card.AddToClassList("ee4v-modification-workflow__part-tab");
            card.userData = child.SiblingIndex;
            card.EnableInClassList(
                "ee4v-ui-selection-tab--inactive",
                !child.IsActiveSelf);
            card.Q<UiTextElement>(className:
                "ee4v-ui-selection-tab-name")
                ?.SetColor(child.IsActiveSelf
                    ? UiColorTokens.TextPrimary
                    : UiColorTokens.TextDisabled);
            card.SetSelected(_avatarContext.SelectedPrefabSiblingIndex == child.SiblingIndex);
            card.RegisterCallback<ClickEvent>(
                _ => SelectPrefabCard(child.SiblingIndex, child.Name));
            if (HasMeshInPrefabScope(child.SiblingIndex))
            {
                var visibility = CreatePrefabVisibilityButton(
                    !_avatarContext.HiddenPrefabSiblingIndices.Contains(child.SiblingIndex),
                    I18N.Get(_avatarContext.HiddenPrefabSiblingIndices.Contains(child.SiblingIndex)
                        ? "workflow.assets.clickToShow"
                        : "workflow.assets.clickToHide"),
                    () => TogglePrefabPreviewVisibility(child.SiblingIndex),
                    "ee4v-ui-selection-tab-visibility");
                visibility.userData = child.SiblingIndex;
                visibility.AddToClassList(
                    "ee4v-modification-workflow__prefab-preview-visibility");
                visibility.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                card.Add(visibility);
            }
            card.RegisterCallback<ContextClickEvent>(evt =>
            {
                var menu = new GenericMenu();
                _parts.AddPrefabActiveSelfMenuItem(menu, child.SiblingIndex);
                if (child.IsAdded)
                {
                    menu.AddSeparator(string.Empty);
                    menu.AddItem(
                        UiTextFactory.CreateGuiContent(
                            I18N.Get("workflow.assets.remove")),
                        false,
                        () => RemoveAssetChild(child));
                }
                menu.ShowAsContext();
                evt.PreventDefault();
                evt.StopPropagation();
            });
            return card;
        }

        private void TogglePrefabPreviewVisibility(int siblingIndex)
        {
            if (siblingIndex == -1)
            {
                _avatarContext.BasePrefabHidden = !_avatarContext.BasePrefabHidden;
            }
            else if (_avatarContext.HiddenPrefabSiblingIndices.Contains(siblingIndex))
            {
                _avatarContext.HiddenPrefabSiblingIndices.Remove(siblingIndex);
            }
            else
            {
                _avatarContext.HiddenPrefabSiblingIndices.Add(siblingIndex);
            }
            RefreshPrefabPreviewVisibilityControls(siblingIndex);
            _avatarContext.Preview?.SetHiddenPrefabs(
                _avatarContext.BasePrefabHidden,
                _avatarContext.HiddenPrefabSiblingIndices);
        }

        private void RefreshPrefabPreviewVisibilityControls(
            int siblingIndex)
        {
            var hidden = siblingIndex == -1
                ? _avatarContext.BasePrefabHidden
                : _avatarContext.HiddenPrefabSiblingIndices.Contains(siblingIndex);
            var tooltip = I18N.Get(hidden
                ? "workflow.assets.clickToShow"
                : "workflow.assets.clickToHide");
            foreach (var button in this.Query<UiButton>(className:
                         "ee4v-modification-workflow__prefab-preview-visibility")
                     .ToList())
            {
                if (!(button.userData is int index) ||
                    index != siblingIndex)
                {
                    continue;
                }
                button.tooltip = tooltip;
                button.SetIcon(FluentUiIcons.CreateState(
                    hidden ? "eye_off.png" : "eye.png",
                    UiSizeTokens.Size18,
                    tooltip));
            }
        }

        private void RefreshPrefabHeaderActiveSelf(
            int siblingIndex,
            bool activeSelf)
        {
            foreach (var card in this.Query<VisualElement>(className:
                         "ee4v-modification-workflow__part-tab")
                     .ToList())
            {
                if (card.userData is int index && index == siblingIndex)
                {
                    card.EnableInClassList(
                        "ee4v-ui-selection-tab--inactive",
                        !activeSelf);
                    card.Q<UiTextElement>(className:
                        "ee4v-ui-selection-tab-name")
                        ?.SetColor(activeSelf
                            ? UiColorTokens.TextPrimary
                            : UiColorTokens.TextDisabled);
                }
            }
        }

        private void RefreshPrefabGroupVisibility(
            int siblingIndex,
            bool activeSelf,
            bool visible)
        {
            foreach (var header in this.Query<VisualElement>(className:
                         "ee4v-modification-workflow__prefab-group-header")
                     .ToList())
            {
                if (!(header.userData is int index) || index != siblingIndex)
                {
                    continue;
                }
                header.EnableInClassList(
                    "ee4v-modification-workflow__prefab-group-header--inactive",
                    !activeSelf);
                header.Q<UiButton>(className:
                    "ee4v-modification-workflow__prefab-group-toggle")
                    ?.SetLabelColor(activeSelf
                        ? UiColorTokens.TextPrimary
                        : UiColorTokens.TextDisabled);
                header.Q<Toggle>(className:
                    "ee4v-modification-workflow__prefab-group-visibility")
                    ?.SetValueWithoutNotify(visible);
            }
        }

        private void SelectPrefabCard(int? siblingIndex, string name)
        {
            if (_avatarContext.SelectedPrefabSiblingIndex == siblingIndex)
            {
                return;
            }

            _parts.EndBodyScaleDrag();
            _parts.SaveBodyScalePrefab();
            if (_parts.BodyScaleDirty) { return; }
            _avatarContext.SelectedPrefabSiblingIndex = siblingIndex;
            _avatarContext.SelectedPrefabName = name ?? string.Empty;
            _avatarContext.SelectedBodyPart = null;
            _avatarContext.SelectedPartKey = null;
            _avatarContext.SelectedMaterial = null;
            if (siblingIndex.HasValue &&
                _currentCategory != WorkflowCategory.Overview &&
                _currentCategory != WorkflowCategory.Execution &&
                _currentCategory != WorkflowCategory.Material)
            {
                _currentCategory = WorkflowCategory.ShapeParts;
            }
            _avatarContext.AssetFeedback = string.Empty;
            BuildWindow();
        }

        private IEnumerable<int> GetDisplayedPrefabScopes()
        {
            return _avatarContext.SelectedPrefabSiblingIndex.HasValue
                ? new[] { _avatarContext.SelectedPrefabSiblingIndex.Value }
                : new[] { -1 }.Concat(_avatarContext.PrefabSiblingIndices);
        }

        private VisualElement BuildCollapsiblePrefabGroup(
            int prefabSiblingIndex,
            VisualElement content,
            HashSet<int> expandedGroups,
            bool showEditorOnlyToggle)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__prefab-group");
            var header = new VisualElement();
            header.AddToClassList(
                "ee4v-modification-workflow__prefab-group-header");
            header.userData = prefabSiblingIndex;
            var inactive = !_avatarContext.Root.transform
                .GetChild(prefabSiblingIndex).gameObject.activeSelf;
            header.EnableInClassList(
                "ee4v-modification-workflow__prefab-group-header--inactive",
                inactive);
            content.AddToClassList(
                "ee4v-modification-workflow__prefab-group-content");
            UiButton toggle = null;
            void UpdateFoldout()
            {
                var expanded = expandedGroups.Contains(prefabSiblingIndex);
                toggle.SetIcon(AssetManagerControls.LoadFluentIconState(
                    expanded ? "chevron_down.png" : "chevron_right.png",
                    UiSizeTokens.Size12));
                content.EnableInClassList(
                    "ee4v-modification-workflow__hidden", !expanded);
                group.EnableInClassList(
                    "ee4v-modification-workflow__prefab-group--header-last",
                    !expanded || content.childCount == 0);
            }
            toggle = new UiButton(
                GetPrefabGroupName(prefabSiblingIndex),
                () =>
                {
                    if (!expandedGroups.Add(prefabSiblingIndex))
                    {
                        expandedGroups.Remove(prefabSiblingIndex);
                    }
                    UpdateFoldout();
                },
                variant: UiButtonVariant.Ghost);
            toggle.AddToClassList(
                "ee4v-modification-workflow__prefab-group-toggle");
            toggle.SetLabelColor(inactive
                ? UiColorTokens.TextDisabled
                : UiColorTokens.TextPrimary);
            var prefabIcon = new Icon(
                AssetManagerControls.LoadFluentIconState(
                    "cube.png", UiSizeTokens.Size16));
            prefabIcon.AddToClassList(
                "ee4v-modification-workflow__prefab-group-icon");
            toggle.Content.Insert(1, prefabIcon);
            header.Add(toggle);
            var actions = new VisualElement();
            actions.AddToClassList(
                "ee4v-modification-workflow__prefab-group-actions");
            if (HasMeshInPrefabScope(prefabSiblingIndex))
            {
                var previewHidden = _avatarContext.HiddenPrefabSiblingIndices.Contains(
                    prefabSiblingIndex);
                var previewVisibility = CreatePrefabVisibilityButton(
                    !previewHidden,
                    I18N.Get(previewHidden
                        ? "workflow.assets.clickToShow"
                        : "workflow.assets.clickToHide"),
                    () => TogglePrefabPreviewVisibility(prefabSiblingIndex),
                    "ee4v-modification-workflow__prefab-group-preview-visibility");
                previewVisibility.userData = prefabSiblingIndex;
                previewVisibility.AddToClassList(
                    "ee4v-modification-workflow__prefab-preview-visibility");
                actions.Add(previewVisibility);
            }
            if (showEditorOnlyToggle)
            {
                var visibility = UiTextFactory.CreateToggle();
                visibility.AddToClassList(
                    "ee4v-modification-workflow__prefab-group-visibility");
                visibility.SetValueWithoutNotify(
                    _parts.IsPrefabGroupCheckboxChecked(prefabSiblingIndex));
                visibility.SetEnabled(IsEditableWorkflowPrefab());
                visibility.RegisterValueChangedCallback(evt =>
                    _parts.ChangePrefabGroupVisibility(
                        prefabSiblingIndex, evt.newValue));
                actions.Add(visibility);
            }
            if (actions.childCount > 0)
            {
                header.Add(actions);
            }
            header.RegisterCallback<ContextClickEvent>(evt =>
                _parts.ShowPrefabContextMenu(evt, prefabSiblingIndex));
            group.Add(header);
            group.Add(content);
            UpdateFoldout();
            return group;
        }

        private string GetPrefabGroupName(int prefabSiblingIndex)
        {
            return prefabSiblingIndex >= 0 &&
                   prefabSiblingIndex < _avatarContext.Root.transform.childCount
                ? _avatarContext.Root.transform.GetChild(prefabSiblingIndex).name
                : _avatarContext.Root.name;
        }

        private bool HasMeshInPrefabScope(int prefabSiblingIndex)
        {
            if (_avatarContext.Root == null)
            {
                return false;
            }
            var root = _avatarContext.Root.transform;
            var meshPresence = new Dictionary<Transform, bool>();
            if (prefabSiblingIndex >= 0)
            {
                return prefabSiblingIndex < root.childCount &&
                       AvatarPartsEditor.HasMeshInSubtree(
                           root.GetChild(prefabSiblingIndex),
                           meshPresence);
            }

            if (AvatarPartsEditor.HasPartMesh(root))
            {
                return true;
            }
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject) &&
                    AvatarPartsEditor.HasMeshInSubtree(child, meshPresence))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnPreviewObjectClicked(string partKey, Material material)
        {
            if (_mode == ModificationEditorMode.Composition ||
                _currentCategory == WorkflowCategory.Overview || _avatarContext.ControlsHost == null) { return; }
            if (_currentCategory == WorkflowCategory.Material)
            {
                _materials.SelectPreviewMaterial(material, GetPreviewPrefabSiblingIndex(partKey));
            }
            else { _parts.SelectPreviewPart(partKey); }
        }

        private void OnPreviewSelectionCleared()
        {
            if (_mode == ModificationEditorMode.Composition || _avatarContext.ControlsHost == null) { return; }
            if (_currentCategory == WorkflowCategory.Material)
            {
                _avatarContext.SelectedMaterial = null;
                InvalidateAppearanceControls(AppearancePanel.Material);
                ShowCategory(WorkflowCategory.Material, false);
            }
            else { _parts.ClearPreviewSelection(); }
        }

        private int GetPreviewPrefabSiblingIndex(string partKey)
        {
            var separator = partKey.IndexOf('/');
            var first = separator < 0
                ? partKey
                : partKey.Substring(0, separator);
            return int.TryParse(first, out var index) &&
                   _avatarContext.PrefabSiblingIndices.Contains(index)
                ? index
                : -1;
        }

        private IReadOnlyList<AssetChildEntry> ReadAssetChildren()
        {
            var root = _avatarContext.Root;
            {
                var children = new List<AssetChildEntry>();
                for (var index = 0; index < root.transform.childCount; index++)
                {
                    var child = root.transform.GetChild(index).gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(child))
                    {
                        continue;
                    }
                    children.Add(new AssetChildEntry
                    {
                        SiblingIndex = index,
                        Name = child.name,
                        IsAdded = PrefabUtility.IsAddedGameObjectOverride(child),
                        IsActiveSelf = child.activeSelf,
                        IsVisible = child.activeSelf &&
                            !string.Equals(child.tag, "EditorOnly",
                                StringComparison.Ordinal)
                    });
                }
                return children.OrderBy(child => child.SiblingIndex)
                    .ToArray();
            }
        }

        private void OpenAssetPicker(
            VisualElement anchor,
            IReadOnlyList<VariantSourceOption> sources)
        {
            var prefabs = sources
                .SelectMany(source => source.Prefabs)
                .Where(prefab => prefab != null)
                .GroupBy(AssetDatabase.GetAssetPath,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(prefab => prefab.name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var target = _avatarContext.Root;
            PrefabSelector.ShowPicker(
                anchor,
                prefabs,
                null,
                prefab =>
                {
                    if (_avatarContext.Root == target)
                    {
                        AddPrefab(prefab);
                    }
                },
                showAsGrid: true);
        }

        private void AddPrefab(GameObject prefab)
        {
            if (!IsEditableWorkflowPrefab() || prefab == null)
            {
                ShowAssetError("workflow.assets.invalidPrefab");
                return;
            }
            var previousSelection = _avatarContext.SelectedPrefabSiblingIndex;
            var previousName = _avatarContext.SelectedPrefabName;
            var previousCategory = _currentCategory;
            var previousSection = _parts.Section;
            var previousBodyPart = _avatarContext.SelectedBodyPart;
            try
            {
                var prefabPath = AssetDatabase.GetAssetPath(prefab);
                var variantPath = GetWorkingAssetPath();
                if (string.IsNullOrEmpty(prefabPath) ||
                    !prefabPath.EndsWith(".prefab",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefabPath, variantPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    AssetDatabase.GetDependencies(prefabPath, true).Any(path =>
                        string.Equals(path, variantPath,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    ShowAssetError("workflow.assets.invalidPrefab");
                    return;
                }

                EditAssetChildren(root =>
                {
                    var added = PrefabUtility.InstantiatePrefab(
                        prefab,
                        root.scene) as GameObject;
                    if (added == null)
                    {
                        throw new InvalidOperationException(
                            "The child Prefab could not be instantiated.");
                    }
                    added.transform.SetParent(root.transform, false);
                    Undo.RegisterCreatedObjectUndo(added,
                        "Add Variant Prefab");
                    _avatarContext.SelectedPrefabSiblingIndex = null;
                    _avatarContext.SelectedPrefabName = string.Empty;
                    _avatarContext.SelectedBodyPart = null;
                    _currentCategory = _mode == ModificationEditorMode.All
                        ? WorkflowCategory.Overview : WorkflowCategory.ShapeParts;
                    _parts.Section = ShapePartsSection.Parts;
                    _avatarContext.HiddenPrefabSiblingIndices.Clear();
                    _prefabPreviewVisibilityInitialized = false;
                });
            }
            catch (Exception exception)
            {
                _avatarContext.SelectedPrefabSiblingIndex = previousSelection;
                _avatarContext.SelectedPrefabName = previousName;
                _currentCategory = previousCategory;
                _parts.Section = previousSection;
                _avatarContext.SelectedBodyPart = previousBodyPart;
                Debug.LogException(exception);
                ShowAssetError("workflow.assets.saveFailed");
            }
        }

        private void RemoveAssetChild(AssetChildEntry entry)
        {
            if (!EditorUtility.DisplayDialog(
                    I18N.Get("workflow.assets.removeConfirmTitle"),
                    string.Format(
                        I18N.Get("workflow.assets.removeConfirmMessage"),
                        entry.Name),
                    I18N.Get("workflow.assets.remove"),
                    I18N.Get("action.cancel")))
            {
                return;
            }
            var previousSelection = _avatarContext.SelectedPrefabSiblingIndex;
            var previousName = _avatarContext.SelectedPrefabName;
            var previousCategory = _currentCategory;
            try
            {
                EditAssetChildren(root =>
                {
                    if (entry.SiblingIndex >= root.transform.childCount)
                    {
                        throw new InvalidOperationException(
                            "The child Prefab is no longer present.");
                    }
                    var child = root.transform
                        .GetChild(entry.SiblingIndex).gameObject;
                    if (!string.Equals(child.name, entry.Name,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "The child Prefab changed before editing.");
                    }
                    if (!PrefabUtility.IsAddedGameObjectOverride(child))
                    {
                        throw new InvalidOperationException(
                            "Inherited Prefab children cannot be removed.");
                    }
                    Undo.DestroyObjectImmediate(child);
                    _avatarContext.SelectedPrefabSiblingIndex = null;
                    _avatarContext.SelectedPrefabName = string.Empty;
                    _currentCategory = previousCategory == WorkflowCategory.Overview ||
                        previousCategory == WorkflowCategory.Material
                            ? previousCategory : WorkflowCategory.ShapeParts;
                    _avatarContext.HiddenPrefabSiblingIndices.Clear();
                    _prefabPreviewVisibilityInitialized = false;
                });
            }
            catch (Exception exception)
            {
                _avatarContext.SelectedPrefabSiblingIndex = previousSelection;
                _avatarContext.SelectedPrefabName = previousName;
                _currentCategory = previousCategory;
                Debug.LogException(exception);
                ShowAssetError("workflow.assets.saveFailed");
            }
        }

        private void EditAssetChildren(
            Action<GameObject> edit)
        {
            _parts.EndBodyScaleDrag();
            _parts.SaveBodyScalePrefab();
            if (_parts.BodyScaleDirty)
            {
                throw new InvalidOperationException(
                    "The derived Prefab size could not be saved.");
            }
            if (!_parts.FlushPendingPartVisibility())
            {
                throw new InvalidOperationException(
                    "Pending part visibility could not be saved.");
            }
            if (!IsEditableWorkflowPrefab())
            {
                throw new InvalidOperationException(
                    "The selected Prefab is not a derived asset.");
            }
            edit(_avatarContext.Root);
            _workingSceneDirty = true;
            _avatarContext.Preview?.ReloadPrefabPreservingView(_avatarContext.Root);
            InvalidateVariantSaveStatus();
            _parts.InvalidateShapeTargets();
            _avatarContext.SelectedMaterial = null;
            _materials.HiddenMaterials.Clear();
            _avatarContext.HiddenPreviewParts.Clear();
            _parts.ClearExpandedGroups();
            _materials.ClearExpandedGroups();
            _avatarContext.Feedback = string.Empty;
            _avatarContext.AssetFeedback = string.Empty;
            BuildWindow();
        }

        private void ShowAssetError(string key)
        {
            _avatarContext.AssetFeedback = I18N.Get(key);
            BuildWindow();
        }
    }
}
