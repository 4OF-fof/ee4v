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
        private void BuildWindow()
        {
            InvalidateVariantSaveStatus();
            ApplyEditorMode();
            _parts.FlushPendingPartVisibility();
            DisposeEditors();
            ClearAppearanceCaches();
            var root = this;
            root.Clear();
            AssetManagerWindowSession.PrepareWorkflowRoot(root);
            root.AddToClassList("ee4v-modification-workflow");

            if (_avatarContext.Root == null ||
                string.IsNullOrEmpty(GetWorkingAssetPath()))
            {
                DisposePreview();
                ReleaseWorkingScene();
                _workingAsset = null;
                root.Add(_creatingDerivedAsset
                    ? BuildDerivedAssetCreation()
                    : BuildDerivedAssetSelection());
                return;
            }

            root.Add(BuildWorkspaceHeader());
            var preview = BuildPreviewPane();
            _editorLayout = new WorkflowEditorLayout(
                _mode == ModificationEditorMode.All ? BuildCategoryRail() : null,
                preview,
                _mode != ModificationEditorMode.Composition);
            root.Add(_editorLayout);
            if (_mode == ModificationEditorMode.Composition)
            {
                SetPreviewTitle("workflow.preview.appearanceTitle");
                return;
            }
            _customizerHost = _editorLayout.AppearanceHost;
            _appearanceHeader = _editorLayout.AppearanceHeader;
            _avatarContext.ControlsHost = _editorLayout.Controls;
            _faceExpressionHost = _editorLayout.FaceExpressionHost;
            _executionHost = _editorLayout.ExecutionHost;
            ShowCategory(_currentCategory, false);
        }

        private VisualElement BuildWorkspaceHeader()
        {
            _avatarContext.PrefabSiblingIndices = Array.Empty<int>();
            var header = new SelectionTabBar();
            var cards = header.Items;
            var combined = CreateHeaderPrefabCard(
                _avatarContext.Root.name,
                SelectionTabVariant.Primary);
            combined.tooltip = _avatarContext.Root.name;
            combined.RegisterCallback<ClickEvent>(
                _ => SelectPrefabCard(null, string.Empty));
            combined.SetSelected(!_avatarContext.SelectedPrefabSiblingIndex.HasValue);
            var changeLabel = I18N.Get("workflow.asset.change");
            var change = new UiButton(
                changeLabel,
                ClearDerivedAsset,
                changeLabel,
                variant: UiButtonVariant.Ghost);
            change.AddToClassList(
                "ee4v-modification-workflow__header-change");
            change.RegisterCallback<ClickEvent>(
                evt => evt.StopPropagation());
            var changeSeparator = new VisualElement();
            changeSeparator.AddToClassList(
                "ee4v-modification-workflow__header-change-separator");
            combined.Add(changeSeparator);
            combined.Add(change);
            header.SetLeadingTab(combined);

            var strip = header.Tabs;
            VisualElement selectedCard = null;
            var basePrefab = PrefabUtility.GetCorrespondingObjectFromSource(
                _avatarContext.Root) as GameObject;
            var baseCard = CreateHeaderPrefabCard(
                basePrefab != null ? basePrefab.name : _avatarContext.Root.name,
                SelectionTabVariant.Default);
            baseCard.tooltip = I18N.Get("workflow.assets.base");
            if (HasMeshInPrefabScope(-1))
            {
                var baseVisibility = CreatePrefabVisibilityButton(
                    !_avatarContext.BasePrefabHidden,
                    I18N.Get(_avatarContext.BasePrefabHidden
                        ? "workflow.assets.clickToShow"
                        : "workflow.assets.clickToHide"),
                    () => TogglePrefabPreviewVisibility(-1),
                    "ee4v-ui-selection-tab-visibility");
                baseVisibility.userData = -1;
                baseVisibility.AddToClassList(
                    "ee4v-modification-workflow__prefab-preview-visibility");
                baseVisibility.RegisterCallback<ClickEvent>(
                    evt => evt.StopPropagation());
                baseCard.Add(baseVisibility);
            }
            baseCard.SetSelected(_avatarContext.SelectedPrefabSiblingIndex == -1);
            baseCard.RegisterCallback<ClickEvent>(
                _ => SelectPrefabCard(-1, _avatarContext.Root.name));
            strip.Add(baseCard);
            if (_avatarContext.SelectedPrefabSiblingIndex == -1)
            {
                selectedCard = baseCard;
            }

            if (IsEditableWorkflowPrefab())
            {
                try
                {
                    var children = ReadAssetChildren();
                    if (!_prefabPreviewVisibilityInitialized)
                    {
                        _avatarContext.HiddenPrefabSiblingIndices.Clear();
                        foreach (var child in children)
                        {
                            if (!child.IsVisible)
                            {
                                _avatarContext.HiddenPrefabSiblingIndices.Add(
                                    child.SiblingIndex);
                            }
                        }
                        _prefabPreviewVisibilityInitialized = true;
                    }
                    _avatarContext.PrefabSiblingIndices = children
                        .Select(child => child.SiblingIndex).ToArray();
                    foreach (var child in children)
                    {
                        var card = BuildAssetChildCard(child);
                        strip.Add(card);
                        if (_avatarContext.SelectedPrefabSiblingIndex ==
                            child.SiblingIndex)
                        {
                            selectedCard = card;
                        }
                    }
                }
                catch (Exception exception)
                {
                    _avatarContext.PrefabSiblingIndices = Array.Empty<int>();
                    Debug.LogException(exception);
                    header.Add(UiTextFactory.CreateHelpBox(
                        I18N.Get("workflow.assets.readFailed"),
                        HelpBoxMessageType.Error));
                }
                var sources = GetVariantSources();
                UiButton add = null;
                add = new UiButton(
                    "+",
                    () => OpenAssetPicker(add, sources),
                    I18N.Get("workflow.assets.add"),
                    variant: UiButtonVariant.Ghost);
                add.SetLabelFontSize(20);
                add.SetLabelTextAlign(TextAnchor.MiddleCenter);
                add.AddToClassList(
                    "ee4v-modification-workflow__header-add");
                add.SetEnabled(sources.Count > 0);
                strip.Add(add);
                if (sources.Count == 0)
                {
                    add.tooltip = I18N.Get(
                        "workflow.assets.noSourceDescription");
                }
            }
            if (selectedCard != null)
            {
                var cardToReveal = selectedCard;
                strip.schedule.Execute(() => strip.ScrollTo(cardToReveal));
            }
            var discard = new UiButton(
                I18N.Get("variant.discardChanges"),
                DiscardVariantChanges,
                variant: UiButtonVariant.Ghost);
            discard.AddToClassList(
                "ee4v-modification-workflow__variant-discard");
            discard.SetEnabled(false);
            cards.Add(discard);
            UiButton save = null;
            save = AssetManagerControls.CreateButton(I18N.Get("variant.save"),
                () => AssetVariantSaveOverlay.Show(this, SaveVariantRevision));
            save.AddToClassList("ee4v-modification-workflow__variant-save");
            save.SetEnabled(false);
            save.schedule.Execute(() =>
            {
                RefreshVariantSaveButton(save);
                RefreshVariantDiscardButton(discard);
            }).Every(750);
            cards.Add(save);

            if (!string.IsNullOrEmpty(_avatarContext.AssetFeedback))
            {
                header.Add(UiTextFactory.CreateHelpBox(
                    _avatarContext.AssetFeedback,
                    HelpBoxMessageType.Error));
            }
            return header;
        }

        private async void SaveVariantRevision(string message)
        {
            if (_disposed || _savingVariant || _avatarContext.Root == null) { return; }
            _parts.EndBodyScaleDrag();
            _parts.SaveBodyScalePrefab();
            if (_parts.BodyScaleDirty) { return; }
            if (!_parts.FlushPendingPartVisibility()) { return; }
            if (!CommitWorkingScene()) { return; }
            _savingVariant = true;
            var assetPath = GetWorkingAssetPath();
            BuildWindow();
            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                var variants = AssetManagerWindowSession.TryGetVariantManager(_manager);
                if (!variants.HasChanges(assetPath)) { return; }
                await variants.Save(new AssetVariantSaveRequest
                {
                    RootAssetPath = assetPath,
                    Memo = message
                });
                if (!_disposed)
                {
                    _avatarContext.AssetFeedback = string.Empty;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!_disposed) { _avatarContext.AssetFeedback = exception.Message; }
            }
            finally
            {
                _savingVariant = false;
                if (!_disposed) { BuildWindow(); }
            }
        }

        private async void DiscardVariantChanges()
        {
            if (_disposed || _savingVariant || _avatarContext.Root == null ||
                string.IsNullOrEmpty(_workingAsset?.VariantId))
            {
                return;
            }

            var assetPath = GetWorkingAssetPath();
            var variantId = _workingAsset.VariantId;
            var category = _currentCategory;
            var shapePartsSection = _parts.Section;
            var rebuild = false;
            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                var variants = AssetManagerWindowSession.TryGetVariantManager(
                    _manager);
                var revisionId = variants?.GetCurrentRevisionId(variantId);
                if (string.IsNullOrEmpty(revisionId) ||
                    !_parts.HasPendingPartVisibility &&
                    !_workingSceneDirty &&
                    !_parts.BodyScaleDirty &&
                    !_parts.HasPendingBodySizeChange &&
                    !variants.HasChangesFromCurrentRevision(assetPath))
                {
                    return;
                }
                if (!EditorUtility.DisplayDialog(
                    I18N.Get("variant.discardChanges"),
                    I18N.Get("variant.discardConfirm"),
                    I18N.Get("variant.discardChanges"),
                    I18N.Get("action.cancel")))
                {
                    return;
                }

                _savingVariant = true;
                rebuild = true;
                _parts.ClearPendingPartVisibility();
                _parts.CancelBodySizeChange();
                _parts.BodyScaleDirty = false;
                _parts.ClearPendingBodySizeChange();
                BuildWindow();

                var scene = _avatarContext.Root.scene;
                ReleaseWorkingScene(true);
                if (scene.IsValid() && scene.isLoaded &&
                    !EditorSceneManager.CloseScene(scene, true))
                {
                    throw new InvalidOperationException(
                        "The Variant working Scene could not be closed.");
                }

                await variants.Restore(variantId, revisionId);
                if (_disposed)
                {
                    return;
                }

                var restored = DerivedAssetCreator.FindAll()
                    .FirstOrDefault(candidate =>
                        candidate.VariantId == variantId);
                if (restored?.Prefab == null)
                {
                    throw new InvalidOperationException(
                        "The restored Variant Prefab could not be loaded.");
                }
                SelectDerivedAsset(restored);
                _currentCategory = category;
                _parts.Section = shapePartsSection;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!_disposed)
                {
                    rebuild = true;
                    _avatarContext.AssetFeedback = exception.Message;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        assetPath);
                    if (prefab != null && _workingAsset != null)
                    {
                        _avatarContext.PrefabAsset = prefab;
                        _avatarContext.Root = AcquireWorkingScene(prefab);
                        _workingAsset.Prefab = prefab;
                    }
                }
            }
            finally
            {
                _savingVariant = false;
                if (!_disposed && rebuild)
                {
                    InvalidateVariantSaveStatus();
                    BuildWindow();
                }
            }
        }

        private void RefreshVariantSaveButton(UiButton save)
        {
            if (_disposed || _savingVariant || _avatarContext.Root == null ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SetVariantSaveButtonEnabled(save, false);
                return;
            }
            var pending = PrefabEditingChanges.HasContentOverrides(_avatarContext.Root) ||
                _parts.HasPendingPartVisibility || _parts.HasPendingBodySizeChange;
            // Always compare Project contents with the Git revision. A standalone
            // Prefab save does not create a revision or clear this difference.
            if (!_variantSaveStatusDirty || EditorApplication.timeSinceStartup >= _variantSaveStatusDueAt)
            {
                try
                {
                    _manager = _manager ?? AssetManagerWindowSession.GetManager();
                    var variants = AssetManagerWindowSession.TryGetVariantManager(_manager);
                    if (!ReferenceEquals(_variantStatusManager, variants))
                    {
                        ReleaseVariantStatusManager();
                        _variantStatusManager = variants;
                        if (_variantStatusManager != null)
                        {
                            _variantStatusManager.Changed += InvalidateVariantSaveStatus;
                            _manager.Changed += OnVariantStatusAssetManagerChanged;
                        }
                    }
                    var status = variants?.GetChangeStatus(
                        GetWorkingAssetPath());
                    _variantHasChanges = status?.HasChanges ?? false;
                    _variantHasDiscardableChanges =
                        status?.HasChangesFromCurrentRevision ?? false;
                    _variantSaveStatusError = null;
                }
                catch (Exception exception)
                {
                    _variantHasChanges = false;
                    _variantHasDiscardableChanges = false;
                    _variantSaveStatusError = exception.Message;
                }
                _variantSaveStatusDirty = false;
            }
            var hasChanges = pending || _variantHasChanges;
            save.tooltip = _variantSaveStatusError ??
                (hasChanges ? string.Empty : I18N.Get("variant.noChanges"));
            SetVariantSaveButtonEnabled(save, hasChanges && _variantSaveStatusError == null);
        }

        private void RefreshVariantDiscardButton(UiButton discard)
        {
            if (_disposed || _savingVariant || _avatarContext.Root == null ||
                string.IsNullOrEmpty(_workingAsset?.VariantId) ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                _variantSaveStatusError != null)
            {
                discard.SetEnabled(false);
                return;
            }

            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                var variants = AssetManagerWindowSession.TryGetVariantManager(
                    _manager);
                var revisionId = variants?.GetCurrentRevisionId(
                    _workingAsset.VariantId);
                var pending = PrefabEditingChanges.HasContentOverrides(_avatarContext.Root) ||
                    _parts.HasPendingPartVisibility || _parts.HasPendingBodySizeChange;
                discard.SetEnabled(!string.IsNullOrEmpty(revisionId) &&
                                   (pending || _variantHasDiscardableChanges));
                discard.tooltip = string.IsNullOrEmpty(revisionId)
                    ? I18N.Get("variant.noSavedRevision")
                    : pending || _variantHasDiscardableChanges
                        ? string.Empty
                        : I18N.Get("variant.noChanges");
            }
            catch (Exception exception)
            {
                discard.SetEnabled(false);
                discard.tooltip = exception.Message;
            }
        }

        private static void SetVariantSaveButtonEnabled(UiButton save, bool enabled)
        {
            save.SetPrimaryActionEnabled(enabled);
        }

        private void InvalidateVariantSaveStatus()
        {
            _variantSaveStatusDirty = true;
            _variantSaveStatusDueAt = EditorApplication.timeSinceStartup +
                VariantSaveStatusDelaySeconds;
        }

        private void OnVariantStatusAssetManagerChanged(AssetManagerChange change)
        {
            InvalidateVariantSaveStatus();
        }

        private void ReleaseVariantStatusManager()
        {
            if (_variantStatusManager == null)
            {
                return;
            }
            _variantStatusManager.Changed -= InvalidateVariantSaveStatus;
            _manager.Changed -= OnVariantStatusAssetManagerChanged;
            _variantStatusManager = null;
        }

        private static SelectionTab CreateHeaderPrefabCard(
            string name,
            SelectionTabVariant variant)
        {
            return new SelectionTab(name, variant);
        }

        private VisualElement BuildCategoryRail()
        {
            _categoryRail = new WorkflowCategoryRail(category => ShowCategory(category));
            _categoryRail.SetPrefabScope(_avatarContext.SelectedPrefabSiblingIndex.HasValue);
            _categoryRail.SetSelected(_currentCategory);
            return _categoryRail;
        }

        private VisualElement BuildPreviewPane()
        {
            _previewPane = new PreviewPane(string.Empty);
            _previewPane.AddToClassList("ee4v-modification-workflow__preview-pane");
            _previewPane.Content.AddToClassList("ee4v-modification-workflow__preview-viewport");
            _avatarContext.Preview = new PrefabScenePreview();
            if (_mode != ModificationEditorMode.Composition)
            {
                _avatarContext.Preview.PreviewObjectClicked += OnPreviewObjectClicked;
                _avatarContext.Preview.PreviewSelectionCleared +=
                    OnPreviewSelectionCleared;
            }
            _avatarContext.Preview.SetFlexibleLayout(true);
            _avatarContext.Preview.SetFullBodyFraming(false);
            _avatarContext.Preview.AddToClassList(
                "ee4v-modification-workflow__preview");
            _previewScopeSiblingIndex = _currentCategory == WorkflowCategory.Overview
                ? null : _avatarContext.SelectedPrefabSiblingIndex;
            _avatarContext.Preview.SetScope(
                _previewScopeSiblingIndex, _avatarContext.PrefabSiblingIndices);
            _avatarContext.Preview.SetHiddenPrefabs(
                _avatarContext.BasePrefabHidden,
                _avatarContext.HiddenPrefabSiblingIndices);
            _avatarContext.Preview.SetHiddenParts(_avatarContext.HiddenPreviewParts);
            _avatarContext.Preview.SetPrefab(_avatarContext.Root);
            SyncPreviewSelection();
            _previewPane.Content.Add(_avatarContext.Preview);
            return _previewPane;
        }

        private void ShowCategory(
            WorkflowCategory category,
            bool clearFeedback = true)
        {
            if (category != WorkflowCategory.ShapeParts ||
                _parts.Section != ShapePartsSection.Parts)
            {
                if (!_parts.FlushPendingPartVisibility())
                {
                    BuildWindow();
                    return;
                }
            }
            if (_avatarContext.SelectedPrefabSiblingIndex.HasValue &&
                category != WorkflowCategory.Overview &&
                category != WorkflowCategory.Execution &&
                category != WorkflowCategory.ShapeParts &&
                category != WorkflowCategory.Material)
            {
                category = WorkflowCategory.ShapeParts;
            }
            if (_currentCategory == WorkflowCategory.ShapeParts &&
                _parts.Section == ShapePartsSection.Shape &&
                category != WorkflowCategory.ShapeParts)
            {
                _parts.EndBodyScaleDrag();
                _parts.SaveBodyScalePrefab();
                if (_parts.BodyScaleDirty) { return; }
            }
            if (_customizerHost == null ||
                _faceExpressionHost == null)
            {
                _currentCategory = category;
                return;
            }
            var categoryChanged = _currentCategory != category;
            if (categoryChanged && clearFeedback)
            {
                _avatarContext.Feedback = string.Empty;
            }
            _currentCategory = category;
            if (_appearanceDataDirty)
            {
                if (!_parts.FlushPendingPartVisibility())
                {
                    BuildWindow();
                    return;
                }
                ClearAppearanceCaches();
            }
            _avatarContext.Preview?.SetHiddenMaterials(
                category == WorkflowCategory.Material
                    ? _materials.HiddenMaterials
                    : null);
            _categoryRail?.SetSelected(category);

            var faceExpression =
                category == WorkflowCategory.ExpressionAnimation;
            var execution = category == WorkflowCategory.Execution;
            _editorLayout.ShowCategory(category);
            if (execution)
            {
                _faceExpressionEditor?.StopPlayback();
                if (_executionView == null)
                {
                    _executionView = new AvatarExecutionView(_avatarContext.Root, RequestRepaint);
                    _executionHost.Add(_executionView);
                }
                return;
            }
            if (faceExpression)
            {
                if (_faceExpressionEditor == null)
                {
                    var editor = new FaceExpressionEmbeddedView(
                        _faceExpressionHost, RequestRepaint);
                    try
                    {
                        editor.Initialize(_avatarContext.PrefabAsset);
                        _faceExpressionEditor = editor;
                    }
                    catch
                    {
                        editor.Dispose();
                        throw;
                    }
                }
                return;
            }

            _faceExpressionEditor?.StopPlayback();
            var previewScope = category == WorkflowCategory.Overview
                ? null : _avatarContext.SelectedPrefabSiblingIndex;
            if (_previewScopeSiblingIndex != previewScope)
            {
                _previewScopeSiblingIndex = previewScope;
                _avatarContext.Preview?.SetScope(previewScope, _avatarContext.PrefabSiblingIndices);
            }
            _overviewContent?.RemoveFromHierarchy();
            _overviewContent = null;
            if (category == WorkflowCategory.Overview)
            {
                _appearanceHeader.Clear();
                _appearanceHeader.style.display = DisplayStyle.None;
                foreach (var cachedControls in _appearanceControlsCache.Values)
                {
                    cachedControls.Content.AddToClassList(
                        "ee4v-modification-workflow__hidden");
                }
                _avatarContext.Preview?.FocusBodyPart(null);
                _avatarContext.Preview?.SetListSelection(null, null);
                _overviewContent = BuildOverviewControls();
                _avatarContext.ControlsHost.Add(_overviewContent);
                if (categoryChanged)
                {
                    _avatarContext.ControlsHost.scrollOffset = Vector2.zero;
                }
                SetPreviewTitle("workflow.preview.appearanceTitle");
                return;
            }
            if (_avatarContext.SelectedBodyPart.HasValue &&
                !HasFocusBone(_avatarContext.SelectedBodyPart.Value))
            {
                _avatarContext.SelectedBodyPart = null;
                _avatarContext.Preview?.FocusBodyPart(null);
            }
            _appearanceHeader.Clear();
            _appearanceHeader.style.display = DisplayStyle.Flex;
            if (category == WorkflowCategory.ShapeParts)
            {
                _appearanceHeader.Add(BuildBodyPartSelector());
                _appearanceHeader.Add(_parts.BuildShapePartsTabs());
            }
            else
            {
                var selector = BuildBodyPartSelector();
                selector.AddToClassList(
                    "ee4v-ui-body-part-selector--standalone");
                _appearanceHeader.Add(selector);
            }
            var appearancePanel = category == WorkflowCategory.Material
                ? AppearancePanel.Material
                : _parts.Section == ShapePartsSection.Shape
                    ? AppearancePanel.Shape
                    : AppearancePanel.Parts;
            if (!_appearanceControlsCache.TryGetValue(appearancePanel,
                    out var cached) ||
                cached.BodyPart != _avatarContext.SelectedBodyPart ||
                appearancePanel == AppearancePanel.Material &&
                    cached.Material != _avatarContext.SelectedMaterial ||
                cached.Feedback != _avatarContext.Feedback ||
                cached.FeedbackType != _avatarContext.FeedbackType)
            {
                InvalidateAppearanceControls(appearancePanel);
                if (appearancePanel == AppearancePanel.Material)
                {
                    _materials.DisposeMaterialEditor();
                }
                else if (appearancePanel == AppearancePanel.Parts)
                {
                    _parts.ClearRows();
                }
                var content = BuildAppearanceControls();
                cached = new CachedAppearanceControls
                {
                    Content = content,
                    BodyPart = _avatarContext.SelectedBodyPart,
                    Material = _avatarContext.SelectedMaterial,
                    Feedback = _avatarContext.Feedback,
                    FeedbackType = _avatarContext.FeedbackType
                };
                _appearanceControlsCache[appearancePanel] = cached;
                _avatarContext.ControlsHost.Add(cached.Content);
            }
            foreach (var pair in _appearanceControlsCache)
            {
                pair.Value.Content.EnableInClassList(
                    "ee4v-modification-workflow__hidden",
                    pair.Key != appearancePanel);
            }
            if (categoryChanged)
            {
                _avatarContext.ControlsHost.scrollOffset = Vector2.zero;
            }
            SetPreviewTitle(
                category == WorkflowCategory.Material
                    ? "workflow.preview.appearanceTitle"
                    : "workflow.preview.sizeTitle");
            SyncPreviewSelection();
        }

        private VisualElement BuildOverviewControls()
        {
            return new AvatarInfoView(_avatarContext.Root.name,
                AvatarInfoAnalysis.FindAttachmentWarnings(_avatarContext.Root),
                AvatarPlayModePerformanceCache.Get(_avatarContext.Root),
                _overviewMobile, AvatarInfoAnalysis.HasAaoComponents(_avatarContext.Root),
                mobile =>
                {
                    _overviewMobile = mobile;
                    ShowCategory(WorkflowCategory.Overview, false);
                },
                prefab =>
                {
                    if (prefab == null) { return; }
                    Selection.activeGameObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                    EditorUtility.OpenPropertyEditor(prefab);
                });
        }
    }
}
