using System;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Simulation;
using Ee4v.FaceExpression;
using Ee4v.AvatarInfo;
using static Ee4v.AvatarParts.AvatarPartsEditor;
using AppearancePanel = Ee4v.AvatarEditing.AvatarEditorPanel;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView
    {
        private void BuildWindow()
        {
            var browsingAssets = IsBrowsingAssets;
            HideVariantChangesHover();
            InvalidateVariantSaveStatus();
            ApplyEditorMode();
            _parts.FlushPendingPartVisibility();
            DisposeEditors();
            ClearAppearanceCaches();
            var root = this;
            root.Clear();
            _editingWorkspace = null;
            AssetManagerWindowSession.PrepareWorkflowRoot(root);
            root.AddToClassList("ee4v-modification-workflow");

            if (_discardingVariant)
            {
                DisposePreview();
                var processing = new VisualElement();
                processing.AddToClassList("ee4v-modification-workflow__processing");
                processing.Add(new StatusOverlay(new StatusOverlayState(
                    true, I18N.Get("variant.discarding"))));
                root.Add(processing);
                RequestRepaint();
                return;
            }

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

            _editingWorkspace = new VisualElement();
            _editingWorkspace.AddToClassList("ee4v-modification-workflow__editing-workspace");
            root.Add(_editingWorkspace);
            _workspaceHeader = BuildWorkspaceHeader();
            _editingWorkspace.Add(_workspaceHeader);
            var preview = EditorApplication.isPlaying
                ? new VisualElement() : BuildPreviewPane();
            _editorLayout = new WorkflowEditorLayout(
                BuildCategoryRail(), preview);
            _editingWorkspace.Add(_editorLayout);
            _customizerHost = _editorLayout.AppearanceHost;
            _appearanceHeader = _editorLayout.AppearanceHeader;
            _bodyPartSelector = null;
            _appearanceTabs = null;
            _avatarContext.ControlsHost = _editorLayout.Controls;
            _faceExpressionHost = _editorLayout.FaceExpressionHost;
            _executionHost = _editorLayout.ExecutionHost;
            ShowCategory(_currentCategory, false);
            if (browsingAssets) { ShowTemporaryAssetManager(); }
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
            header.SetLeadingTab(combined);

            if (EditorApplication.isPlaying)
            {
                combined.SetSelected(true);
                return header;
            }

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
                var baseVisibility = CreateWorkflowPrefabVisibilityButton(
                    -1,
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
            if (_workingAsset?.IsOriginal != true || HasMeshInPrefabScope(-1)) { strip.Add(baseCard); }
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
                    header.Add(UiTextFactory.CreateMessagePanel(
                        I18N.Get("workflow.assets.readFailed"),
                        MessageSeverity.Error));
                }
                UiButton add = null;
                add = new UiButton(
                    "+",
                    () => OpenAssetPicker(add),
                    I18N.Get("workflow.assets.add"),
                    variant: UiButtonVariant.Ghost);
                add.SetLabelFontSize(20);
                add.SetLabelTextAlign(TextAnchor.MiddleCenter);
                add.AddToClassList(
                    "ee4v-modification-workflow__header-add");
                strip.Add(add);
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
            AttachVariantChangesHover(save, false);
            AttachVariantChangesHover(discard, true);
            save.schedule.Execute(() =>
            {
                RefreshVariantSaveButton(save);
                RefreshVariantDiscardButton(discard);
                if (_variantChangesHoverAnchor != null && !_variantChangesHoverAnchor.enabledInHierarchy)
                {
                    HideVariantChangesHover();
                }
            }).Every(750);
            cards.Add(save);

            if (!string.IsNullOrEmpty(_avatarContext.AssetFeedback))
            {
                header.Add(UiTextFactory.CreateMessagePanel(
                    _avatarContext.AssetFeedback,
                    MessageSeverity.Error));
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
            if (!_parts.PrepareBodyBlendShapeSyncForSave()) { return; }
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
            var appearancePanel = _appearancePanel;
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
                _discardingVariant = true;
                rebuild = true;
                _parts.ClearPendingPartVisibility();
                _parts.CancelBodySizeChange();
                _parts.BodyScaleDirty = false;
                _parts.ClearPendingBodySizeChange();
                BuildWindow();

                // Let the processing view paint before closing the working Scene.
                await Task.Yield();
                if (_disposed) { return; }

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
                _appearancePanel = appearancePanel;
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
                        try
                        {
                            var root = AcquireWorkingScene(prefab);
                            _avatarContext.PrefabAsset = prefab;
                            _avatarContext.Root = root;
                            _workingAsset.Prefab = prefab;
                        }
                        catch (Exception recoveryException)
                        {
                            Debug.LogException(recoveryException);
                            ReleaseWorkingScene();
                            _avatarContext.AssetFeedback = exception.Message +
                                Environment.NewLine + recoveryException.Message;
                        }
                    }
                }
            }
            finally
            {
                _discardingVariant = false;
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
            _variantHasPrefabOverrides = PrefabEditingChanges.HasContentOverrides(_avatarContext.Root);
            var pending = _variantHasPrefabOverrides ||
                _parts.HasPendingPartVisibility || _parts.HasPendingBodySizeChange;
            // Keep the Git comparison until a change notification invalidates it.
            // A standalone Prefab save still requires a new Variant revision.
            if (_variantSaveStatusDirty && !_variantSaveStatusRefreshing &&
                EditorApplication.timeSinceStartup >= _variantSaveStatusDueAt)
            {
                RefreshVariantSaveStatus();
            }
            var hasChanges = pending || _variantHasChanges;
            save.tooltip = _variantSaveStatusError ??
                (hasChanges ? string.Empty : I18N.Get("variant.noChanges"));
            SetVariantSaveButtonEnabled(save, hasChanges && _variantSaveStatusError == null);
        }

        private async void RefreshVariantSaveStatus()
        {
            var version = _variantSaveStatusVersion;
            var root = _avatarContext.Root;
            var path = GetWorkingAssetPath();
            _variantSaveStatusRefreshing = true;
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
                var status = variants == null ? null : await variants.GetChangeStatusAsync(path);
                if (!IsCurrentRequest()) { return; }
                _variantHasChanges = status?.HasChanges ?? false;
                _variantHasDiscardableChanges = status?.HasChangesFromCurrentRevision ?? false;
                _variantSaveStatusError = null;
                _variantSaveStatusDirty = false;
            }
            catch (Exception exception)
            {
                if (!IsCurrentRequest()) { return; }
                _variantHasChanges = false;
                _variantHasDiscardableChanges = false;
                _variantSaveStatusError = exception.Message;
                _variantSaveStatusDirty = false;
            }
            finally { _variantSaveStatusRefreshing = false; }

            bool IsCurrentRequest() => !_disposed && !_savingVariant &&
                version == _variantSaveStatusVersion && root == _avatarContext.Root &&
                path == GetWorkingAssetPath();
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
                var pending = _variantHasPrefabOverrides ||
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
            _variantChangesSnapshot = null;
            _variantSaveStatusVersion++;
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

        private bool IsExpressionAssignmentOpen => _currentCategory == WorkflowCategory.ExpressionAnimation &&
            _faceExpressionEditor?.IsAssignmentView == true;

        private VisualElement BuildCategoryRail()
        {
            _categoryRail = new WorkflowCategoryRail(category => ShowCategory(category), EditorApplication.isPlaying,
                DerivedAssetCreator.IsAvatarPrefab(_avatarContext.Root));
            _categoryRail.SetPrefabScope(_avatarContext.SelectedPrefabSiblingIndex.HasValue);
            _categoryRail.SetSelected(_currentCategory);
            if (!EditorApplication.isPlaying)
            {
                var findAsset = AssetPicker.CreateButton(
                    () => IsExpressionAssignmentOpen ? typeof(AnimationClip) : typeof(Material),
                    () => _currentCategory == WorkflowCategory.Appearance && _appearancePanel == AppearancePanel.Material
                        ? _avatarContext.SelectedMaterial : null,
                    () => _avatarContext.Root,
                    () => IsExpressionAssignmentOpen ? AssetPickerAnimationFilter.Facial : AssetPickerAnimationFilter.All);
                findAsset.AddToClassList("ee4v-modification-workflow__category-button");
                _categoryRail.Actions.Add(findAsset);
                var openManager = new UiButton(
                    I18N.Get("workflow.selection.openManager"),
                    ShowTemporaryAssetManager,
                    icon: FluentUiIcons.CreateState("library.png", UiSizeTokens.Size12),
                    variant: UiButtonVariant.Ghost);
                openManager.AddToClassList("ee4v-modification-workflow__category-button");
                _categoryRail.Actions.Add(openManager);
            }
            return _categoryRail;
        }

        private VisualElement BuildPreviewPane()
        {
            _previewPane = new PreviewPane(string.Empty);
            _previewPane.AddToClassList("ee4v-modification-workflow__preview-pane");
            _previewPane.Content.AddToClassList("ee4v-modification-workflow__preview-viewport");
            _avatarContext.Preview = new GameObjectPreview();
            _avatarContext.Preview.PreviewObjectClicked += OnPreviewObjectClicked;
            _avatarContext.Preview.PreviewObjectDoubleClicked += OnPreviewObjectDoubleClicked;
            _avatarContext.Preview.PreviewSelectionCleared +=
                OnPreviewSelectionCleared;
            _avatarContext.Preview.SetFlexibleLayout(true);
            _avatarContext.Preview.SetFullBodyFraming(_previewOnly);
            _avatarContext.Preview.AddToClassList(
                "ee4v-modification-workflow__preview");
            _previewScopeSiblingIndex = _avatarContext.SelectedPrefabSiblingIndex;
            _avatarContext.Preview.SetScope(
                _previewScopeSiblingIndex, _avatarContext.PrefabSiblingIndices);
            _avatarContext.Preview.SetHiddenPrefabs(
                _avatarContext.BasePrefabHidden,
                _avatarContext.HiddenPrefabSiblingIndices);
            _avatarContext.Preview.SetHiddenParts(_avatarContext.HiddenPreviewParts);
            _avatarContext.Preview.SetTarget(_avatarContext.Root);
            UpdateAppearancePreviewScope(_currentCategory);
            _avatarContext.Preview.FocusBodyPart(_appearancePanel == AppearancePanel.Position
                ? null : _avatarContext.SelectedBodyPart, true);
            SyncPreviewSelection();
            _previewPane.Content.Add(_avatarContext.Preview);
            return _previewPane;
        }

        private void ShowAppearancePanel(AppearancePanel panel)
        {
            if (panel == AppearancePanel.Position && !_parts.CanAdjustItemPosition)
                panel = AppearancePanel.Parts;
            var changed = _appearancePanel != panel;
            if (changed && !_avatarContext.Edits.FlushChanges()) { return; }
            if (changed) { ReleaseAppearanceDetails(); }
            _appearancePanel = panel;
            if (changed) _avatarContext.Preview?.FocusBodyPart(panel == AppearancePanel.Position
                ? null : _avatarContext.SelectedBodyPart, true);
            if (panel != AppearancePanel.Material)
            {
                _parts.Section = panel == AppearancePanel.Shape
                    ? ShapePartsSection.Shape : panel == AppearancePanel.Position
                        ? ShapePartsSection.Position : ShapePartsSection.Parts;
            }
            ShowCategory(WorkflowCategory.Appearance, false);
            if (changed && _avatarContext.ControlsHost != null)
            {
                _avatarContext.ControlsHost.scrollOffset = Vector2.zero;
            }
            RequestRepaint();
        }

        private void UpdateAppearancePreviewScope(WorkflowCategory category)
        {
            var fitting = category == WorkflowCategory.Appearance &&
                          _appearancePanel == AppearancePanel.Position && _parts.CanAdjustItemPosition;
            var scope = fitting ? null : _avatarContext.SelectedPrefabSiblingIndex;
            if (_previewScopeSiblingIndex != scope)
            {
                _previewScopeSiblingIndex = scope;
                _avatarContext.Preview?.SetScope(scope, _avatarContext.PrefabSiblingIndices);
            }
            _avatarContext.Preview?.SetHiddenPrefabs(_avatarContext.BasePrefabHidden,
                _avatarContext.HiddenPrefabSiblingIndices);
        }

        private void ReleaseAppearanceDetails()
        {
            if (_appearancePanel == AppearancePanel.Parts && _avatarContext.SelectedPartKey != null)
            {
                InvalidateAppearanceControls(AppearancePanel.Parts);
            }
            else if (_appearancePanel == AppearancePanel.Material && _avatarContext.SelectedMaterial != null)
            {
                InvalidateAppearanceControls(AppearancePanel.Material);
                _materials.DisposeMaterialEditor();
            }
        }

        private void ShowCategory(
            WorkflowCategory category,
            bool clearFeedback = true,
            bool preservePreviewCamera = false,
            bool confirmExpressionChanges = true)
        {
            if (_appearancePanel == AppearancePanel.Position && !_parts.CanAdjustItemPosition)
            {
                _appearancePanel = AppearancePanel.Parts;
                _parts.Section = ShapePartsSection.Parts;
                _avatarContext.Preview?.FocusBodyPart(_avatarContext.SelectedBodyPart, true);
            }
            if (EditorApplication.isPlaying && !category.IsPlayMode()) { category = WorkflowCategory.MenuAndGestures; }
            else if (!EditorApplication.isPlaying && category.IsPlayMode()) { category = _editingCategory; }
            if (!DerivedAssetCreator.IsAvatarPrefab(_avatarContext.Root))
            {
                if (category == WorkflowCategory.MenuAndGestures) { category = WorkflowCategory.Lighting; }
                else if (category == WorkflowCategory.ExpressionAnimation || category == WorkflowCategory.ExpressionMenu)
                    category = WorkflowCategory.Overview;
            }
            if (category != WorkflowCategory.Appearance ||
                _appearancePanel != AppearancePanel.Parts)
            {
                if (!_parts.FlushPendingPartVisibility())
                {
                    BuildWindow();
                    return;
                }
            }
            if (_avatarContext.SelectedPrefabSiblingIndex.HasValue &&
                !category.IsPlayMode() &&
                category != WorkflowCategory.Appearance)
            {
                category = WorkflowCategory.Appearance;
            }
            if (confirmExpressionChanges && _currentCategory == WorkflowCategory.ExpressionAnimation &&
                category != _currentCategory && _faceExpressionEditor != null &&
                !_faceExpressionEditor.TryConfirmNavigation(this,
                    () => ShowCategory(category, clearFeedback, preservePreviewCamera, false)))
            {
                _categoryRail?.SetSelected(_currentCategory);
                return;
            }
            if (_currentCategory == WorkflowCategory.Appearance &&
                _appearancePanel == AppearancePanel.Shape &&
                category != WorkflowCategory.Appearance)
            {
                _parts.EndBodyScaleDrag();
                _parts.SaveBodyScalePrefab();
                if (_parts.BodyScaleDirty) { return; }
            }
            UpdateAppearancePreviewScope(category);
            if (_customizerHost == null ||
                _faceExpressionHost == null)
            {
                _currentCategory = category;
                return;
            }
            var categoryChanged = _currentCategory != category;
            if (categoryChanged && _currentCategory == WorkflowCategory.Appearance)
            {
                ReleaseAppearanceDetails();
                foreach (var cachedControls in _appearanceControlsCache.Values)
                {
                    cachedControls.Content.RemoveFromHierarchy();
                }
            }
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
                category == WorkflowCategory.Appearance && _appearancePanel == AppearancePanel.Material
                    ? _materials.PreviewHiddenMaterials
                    : null);
            _categoryRail?.SetSelected(category);

            var faceExpression =
                category == WorkflowCategory.ExpressionAnimation;
            var execution = category.IsPlayMode();
            _editorLayout.ShowCategory(category);
            if (execution)
            {
                _faceExpressionEditor?.StopPlayback();
                if (_executionCategory != category)
                {
                    _executionView?.Dispose();
                    _inspectionView?.Dispose();
                    _executionView = null;
                    _inspectionView = null;
                    _executionHost.Clear();
                    _executionCategory = category;
                    if (category == WorkflowCategory.MenuAndGestures)
                    {
                        _executionView = new AvatarExecutionView(_avatarContext.Root, RequestRepaint);
                        _executionHost.Add(_executionView);
                    }
                    else
                    {
                        _inspectionView = new AvatarLightingView(_avatarContext.Root, RequestRepaint);
                        _executionHost.Add(_inspectionView);
                    }
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
            _overviewContent?.RemoveFromHierarchy();
            _overviewContent = null;
            if (category == WorkflowCategory.Overview || category == WorkflowCategory.ExpressionMenu)
            {
                _appearanceHeader.style.display = DisplayStyle.None;
                foreach (var cachedControls in _appearanceControlsCache.Values)
                {
                    cachedControls.Content.RemoveFromHierarchy();
                }
                _avatarContext.Preview?.FocusBodyPart(null, preservePreviewCamera);
                _avatarContext.Preview?.SetListSelection(null, null);
                _overviewContent = category == WorkflowCategory.ExpressionMenu
                    ? new Ee4v.ExpressionMenu.ExpressionMenuView(_avatarContext)
                    : BuildOverviewControls();
                if (category == WorkflowCategory.ExpressionMenu)
                    _editorLayout.ExpressionMenuHost.Add(_overviewContent);
                else _avatarContext.ControlsHost.Add(_overviewContent);
                if (categoryChanged)
                {
                    _avatarContext.ControlsHost.scrollOffset = Vector2.zero;
                }
                SetPreviewTitle("workflow.preview.appearanceTitle");
                return;
            }
            if (_bodyPartSelector == null)
            {
                _bodyPartSelector = BuildBodyPartSelector();
                _appearanceHeader.Add(_bodyPartSelector);
            }
            _bodyPartSelector.Refresh(_avatarContext.SelectedBodyPart, part => !part.HasValue || HasAvailableBodyPart(part.Value));
            if (_appearanceTabs == null)
            {
                _appearanceTabs = _parts.BuildShapePartsTabs(ShowAppearancePanel, _appearancePanel);
                _appearanceHeader.Add(_appearanceTabs);
            }
            _parts.RefreshShapePartsTabs(_appearanceTabs, _appearancePanel);
            var appearancePanel = _appearancePanel;
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
            }
            foreach (var pair in _appearanceControlsCache)
            {
                if (pair.Key != appearancePanel)
                {
                    pair.Value.Content.RemoveFromHierarchy();
                }
            }
            if (cached.Content.parent != _avatarContext.ControlsHost.contentContainer)
            {
                _avatarContext.ControlsHost.Add(cached.Content);
            }
            _bodyPartSelector.style.display = appearancePanel == AppearancePanel.Position
                ? DisplayStyle.None : DisplayStyle.Flex;
            _appearanceHeader.style.display = DisplayStyle.Flex;
            if (categoryChanged)
            {
                _avatarContext.ControlsHost.scrollOffset = Vector2.zero;
            }
            SetPreviewTitle(
                appearancePanel == AppearancePanel.Material
                    ? "workflow.preview.appearanceTitle"
                    : "workflow.preview.sizeTitle");
            SyncPreviewSelection();
        }

        private VisualElement BuildOverviewControls()
        {
            if (!DerivedAssetCreator.IsAvatarPrefab(_avatarContext.Root))
            {
                return BuildOriginalOverviewControls();
            }
            var editing = AvatarInfoEditing.CreateOptions(_avatarContext);
            if (!_previewOnly) editing.RenameAsync = RenameWorkingAvatar;
            return new AvatarInfoView(_avatarContext.Root.name,
                AvatarInfoAnalysis.GetBlueprintId(_avatarContext.Root),
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
                    SelectPrefabCard(prefab.transform.GetSiblingIndex(), prefab.name);
                    _parts.Section = ShapePartsSection.Position;
                    ShowAppearancePanel(AppearancePanel.Position);
                }, editing,
                AvatarInfoSdk.Provider?.ReadParameterMemory(_avatarContext.Root),
                AvatarBuildSizeCache.Get(_avatarContext.Root, _overviewMobile),
                () =>
                {
                    if (_avatarContext.Edits.FlushChanges())
                        AvatarPlayModePerformanceCache.Bake(_avatarContext.Root);
                }, AvatarPlayModePerformanceCache.CanBake(_avatarContext.Root));
        }
    }
}
