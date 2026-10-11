using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using static Ee4v.AvatarEditing.AvatarEditingUi;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Ee4v.AssetManager.Contracts;
using static Ee4v.AvatarParts.AvatarPartsEditor;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView
    {
        internal void ShowTemporaryAssetManager()
        {
            if (_disposed || IsBrowsingAssets || _editingWorkspace == null ||
                _avatarContext.Root == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            HideVariantChangesHover();
            var page = new VisualElement();
            page.AddToClassList("ee4v-modification-workflow__selection-page");
            page.AddToClassList("ee4v-modification-workflow__temporary-library");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-modification-workflow__library-toolbar");
            toolbar.Add(new UiButton(
                I18N.Get("workflow.selection.backToEditing"),
                () =>
                {
                    CloseTemporaryAssetManager();
                    RequestRepaint();
                },
                icon: FluentUiIcons.CreateState("arrow_left.png", UiSizeTokens.Size12),
                variant: UiButtonVariant.Ghost));
            page.Add(toolbar);
            try
            {
                _temporaryAssetManagerView = new AssetManagerWorkspaceView(
                    null,
                    createView: (mode, createDerivedAsset) => new AssetManagerView(
                        _manager = _manager ?? AssetManagerWindowSession.GetManager(),
                        _assetManagerViewState,
                        mode));
                page.Add(_temporaryAssetManagerView);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                page.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.selection.managerLoadFailed"),
                    HelpBoxMessageType.Error));
            }
            _temporaryAssetManagerPage = page;
            _editingWorkspace.AddToClassList("ee4v-modification-workflow__editing-workspace--hidden");
            Add(page);
            RequestRepaint();
        }

        private void CloseTemporaryAssetManager()
        {
            _temporaryAssetManagerView?.Dispose();
            _temporaryAssetManagerView = null;
            _temporaryAssetManagerPage?.RemoveFromHierarchy();
            _temporaryAssetManagerPage = null;
            _editingWorkspace?.RemoveFromClassList("ee4v-modification-workflow__editing-workspace--hidden");
        }

        private VisualElement BuildDerivedAssetSelection()
        {
            var page = new VisualElement();
            page.AddToClassList("ee4v-modification-workflow__selection-page");
            if (!string.IsNullOrEmpty(_avatarContext.AssetFeedback))
            {
                page.Add(UiTextFactory.CreateHelpBox(
                    _avatarContext.AssetFeedback, HelpBoxMessageType.Error));
            }
            try
            {
                _assetManagerView = new AssetManagerWorkspaceView(
                    SelectDerivedAsset,
                    StartDerivedAssetCreation,
                    (mode, createDerivedAsset) => new AssetManagerView(
                        _manager = _manager ?? AssetManagerWindowSession.GetManager(),
                        _assetManagerViewState,
                        mode,
                        createDerivedAsset: createDerivedAsset),
                    createOriginalAsset: StartOriginalAssetCreation);
                page.Add(_assetManagerView);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                page.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.selection.managerLoadFailed"),
                    HelpBoxMessageType.Error));
            }
            return page;
        }

        private VisualElement BuildDerivedAssetCreation()
        {
            var selectedSource = GetSelectedVariantSource();
            var sources = _creatingOriginalAsset ? GetVariantSources() : null;
            var prefabs = sources != null ? sources.SelectMany(source => source.Prefabs).Distinct().ToArray()
                : selectedSource?.Prefabs;
            var form = new VariantCreationView(
                selectedSource?.Item,
                prefabs,
                _derivedName,
                _derivedDescription,
                _creationPrefab,
                I18N.Get("workflow.selection.createAndStart"),
                CancelDerivedAssetCreation,
                _creatingOriginalAsset,
                sources == null ? null : (anchor, selected, select) =>
                    AssetPrefabPickerWindow.Show(anchor, sources, _manager, select, null, selectPrefab: true));
            _creationPrefab = form.Prefab.Value;
            form.Prefab.ValueChanged += prefab => _creationPrefab = prefab;
            form.Name.ValueChanged += value => _derivedName = value;
            form.Description.ValueChanged += value => _derivedDescription = value;
            form.SetFeedback(_avatarContext.Feedback);
            form.Create.clicked += CreateDerivedAsset;
            return form;
        }

        private void StartDerivedAssetCreation()
        {
            _creatingDerivedAsset = true;
            _avatarContext.Feedback = string.Empty;
            BuildWindow();
        }

        private void StartDerivedAssetCreation(string itemId)
        {
            _manager = _manager ?? AssetManagerWindowSession.GetManager();
            var item = _manager.GetItem(itemId);
            if (item == null || item.IsArchived)
            {
                return;
            }
            _creationItemId = item.Id;
            _creatingOriginalAsset = false;
            _creationPrefab = null;
            _derivedName = item.Name + " Variant";
            _derivedDescription = string.Empty;
            StartDerivedAssetCreation();
        }

        private void StartOriginalAssetCreation()
        {
            _creatingOriginalAsset = true;
            _creationItemId = null;
            _creationPrefab = null;
            _derivedName = string.Empty;
            _derivedDescription = string.Empty;
            StartDerivedAssetCreation();
        }

        private VisualElement BuildOriginalOverviewControls()
        {
            var controls = new VisualElement();
            var fittingAvatar = DerivedAssetCreator.GetFittingAvatar(_avatarContext.Root);
            var candidates = GetVariantSources().SelectMany(source => source.Prefabs)
                .Where(DerivedAssetCreator.IsAvatarPrefab).Distinct().ToArray();
            var currentPrefab = fittingAvatar != null
                ? PrefabUtility.GetCorrespondingObjectFromSource(fittingAvatar) as GameObject : null;
            if (currentPrefab != null && !candidates.Contains(currentPrefab))
            {
                candidates = candidates.Append(currentPrefab).ToArray();
            }
            var selector = new PrefabSelector(candidates, (anchor, selected, select) =>
                AssetPrefabPickerWindow.Show(anchor, GetVariantSources().Select(source => new VariantSourceOption
                {
                    Item = source.Item,
                    Prefabs = source.Prefabs.Where(DerivedAssetCreator.IsAvatarPrefab).ToArray()
                }).Where(source => source.Prefabs.Count > 0).ToArray(), _manager, select, ShowTemporaryAssetManager));
            selector.SetValueWithoutNotify(currentPrefab);
            selector.ValueChanged += prefab => ChangeFittingAvatar(prefab);
            controls.Add(new FormInput(I18N.Get("workflow.original.fittingAvatar"), selector));
            controls.Add(UiTextFactory.CreateHelpBox(
                I18N.Get("workflow.original.fittingDescription"), HelpBoxMessageType.Info));
            if (fittingAvatar != null)
            {
                var actions = new VisualElement();
                actions.Add(new UiButton(I18N.Get("workflow.original.removeFittingAvatar"),
                    () => ChangeFittingAvatar(null), variant: UiButtonVariant.Ghost));
                actions.Add(new UiButton(I18N.Get("workflow.original.showFittingScene"), () =>
                {
                    Selection.activeGameObject = fittingAvatar;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }, variant: UiButtonVariant.Ghost));
                controls.Add(actions);
                var preview = new PrefabScenePreview();
                preview.SetFullBodyFraming(true);
                preview.SetPrefab(fittingAvatar);
                controls.Add(preview);
            }
            return controls;
        }

        private void ChangeFittingAvatar(GameObject prefab)
        {
            try
            {
                _parts.EndBodyScaleDrag();
                _parts.SaveBodyScalePrefab();
                if (_parts.BodyScaleDirty || !_parts.FlushPendingPartVisibility()) { return; }
                DerivedAssetCreator.SetFittingAvatar(_avatarContext.Root, prefab);
                _workingSceneDirty = true;
                QueueWorkingSceneSave();
                InvalidateVariantSaveStatus();
                BuildWindow();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowAssetError("workflow.original.fittingFailed");
            }
        }

        private void CancelDerivedAssetCreation()
        {
            _creatingDerivedAsset = false;
            _creatingOriginalAsset = false;
            _avatarContext.Feedback = string.Empty;
            BuildWindow();
        }

        private IReadOnlyList<VariantSourceOption> GetVariantSources(Func<GameObject, bool> canSelect = null)
        {
            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                return AssetPrefabPickerWindow.FindSources(_manager, canSelect);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Array.Empty<VariantSourceOption>();
            }
        }

        private VariantSourceOption CreateVariantSource(AssetItem item)
        {
            return new VariantSourceOption
            {
                Item = item,
                Prefabs = DerivedAssetCreator.FindPrefabCandidates(_manager, item)
            };
        }

        private VariantSourceOption GetSelectedVariantSource()
        {
            try
            {
                if (string.IsNullOrEmpty(_creationItemId)) { return null; }
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                var item = _manager.GetItem(_creationItemId);
                if (item == null || item.IsArchived) { return null; }
                var source = CreateVariantSource(item);
                return source.Prefabs.Count == 0 ? null : source;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        private void CreateDerivedAsset()
        {
            if (!DerivedAssetCreator.IsValidName(_derivedName))
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetNameInvalid"),
                    HelpBoxMessageType.Error);
                return;
            }
            if (!_creatingOriginalAsset && (string.IsNullOrEmpty(_creationItemId) ||
                _creationPrefab == null))
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetPrefabInvalid"),
                    HelpBoxMessageType.Error);
                return;
            }
            if (_creatingOriginalAsset && _creationPrefab != null &&
                !GetVariantSources().SelectMany(source => source.Prefabs).Contains(_creationPrefab))
            {
                SetCreationFeedback(I18N.Get("notice.derivedAssetPrefabInvalid"), HelpBoxMessageType.Error);
                return;
            }
            if (AssetDatabase.IsValidFolder(
                    DerivedAssetCreator.GetVariantFolder(_derivedName)))
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetAlreadyExists"),
                    HelpBoxMessageType.Error);
                return;
            }

            AssetItem owner = null;
            var created = false;
            try
            {
                if (_creatingOriginalAsset)
                {
                    _manager = _manager ?? AssetManagerWindowSession.GetManager();
                    owner = _manager.CreateItem(new CreateAssetItemRequest
                    {
                        Name = _derivedName.Trim(), Description = _derivedDescription, IsOriginal = true
                    });
                }
                var result = DerivedAssetCreator.Create(
                    new DerivedAssetCreationRequest
                    {
                        ParentItemId = owner?.Id ?? _creationItemId,
                        Name = _derivedName,
                        Description = _derivedDescription,
                        Prefab = _creationPrefab,
                        CreateOriginal = _creatingOriginalAsset && _creationPrefab == null
                    });
                created = true;
                _creatingDerivedAsset = false;
                _creatingOriginalAsset = false;
                SelectDerivedAsset(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!created && owner != null)
                {
                    try
                    {
                        _manager.SetItemArchived(new[] { owner.Id }, true);
                        _manager.DeleteItem(new[] { owner.Id });
                    }
                    catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                }
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetCreateFailed"),
                    HelpBoxMessageType.Error);
            }
        }

        private void SetCreationFeedback(
            string message,
            HelpBoxMessageType type)
        {
            _avatarContext.Feedback = message ?? string.Empty;
            _avatarContext.FeedbackType = type;
            BuildWindow();
        }

        internal void SelectDerivedAsset(DerivedAssetInfo asset)
        {
            if (asset?.Prefab == null)
            {
                return;
            }
            _parts.EndBodyScaleDrag();
            _parts.SaveBodyScalePrefab();
            if (_parts.BodyScaleDirty) { return; }
            ReleaseWorkingScene();
            _parts.ResetEditingState();
            _materials.ResetEditingState();
            GameObject root;
            try
            {
                root = AcquireWorkingScene(asset.Prefab);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReleaseWorkingScene();
                _workingAsset = null;
                _avatarContext.AssetFeedback = exception.Message;
                DerivedAssetChanged?.Invoke(null);
                BuildWindow();
                return;
            }
            _workingAsset = asset;
            _avatarContext.PrefabAsset = asset.Prefab;
            _avatarContext.Root = root;
            _avatarContext.BasePrefabHidden = false;
            _avatarContext.HiddenPrefabSiblingIndices.Clear();
            _avatarContext.HiddenPreviewParts.Clear();
            _prefabPreviewVisibilityInitialized = false;
            _avatarContext.SelectedPrefabSiblingIndex = null;
            _avatarContext.SelectedPrefabName = string.Empty;
            _creatingDerivedAsset = false;
            _currentCategory = WorkflowCategory.Overview;
            ApplyEditorMode();
            _parts.Section = ShapePartsSection.Parts;
            _avatarContext.SelectedBodyPart = null;
            _avatarContext.SelectedPartKey = null;
            _avatarContext.SelectedMaterial = null;
            _avatarContext.Feedback = string.Empty;
            _avatarContext.AssetFeedback = string.Empty;
            DerivedAssetChanged?.Invoke(asset);
            BuildWindow();
        }

        private void ClearDerivedAsset()
        {
            _parts.EndBodyScaleDrag();
            _parts.SaveBodyScalePrefab();
            if (_parts.BodyScaleDirty) { return; }
            ReleaseWorkingScene();
            _parts.ResetEditingState();
            _materials.ResetEditingState();
            _workingAsset = null;
            _avatarContext.BasePrefabHidden = false;
            _avatarContext.HiddenPrefabSiblingIndices.Clear();
            _avatarContext.HiddenPreviewParts.Clear();
            _prefabPreviewVisibilityInitialized = false;
            _avatarContext.SelectedPrefabSiblingIndex = null;
            _avatarContext.SelectedPrefabName = string.Empty;
            _avatarContext.SelectedBodyPart = null;
            _avatarContext.SelectedPartKey = null;
            _avatarContext.SelectedMaterial = null;
            _avatarContext.Feedback = string.Empty;
            _avatarContext.AssetFeedback = string.Empty;
            DerivedAssetChanged?.Invoke(null);
            BuildWindow();
        }

    }
}
