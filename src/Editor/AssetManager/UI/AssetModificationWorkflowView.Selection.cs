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
                        createDerivedAsset: createDerivedAsset));
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

        private void ShowAssetManagerSelection()
        {
            _creatingDerivedAsset = false;
            BuildWindow();
        }

        private VisualElement BuildDerivedAssetCreation()
        {
            var page = new VisualElement();
            page.AddToClassList(
                "ee4v-modification-workflow__selection-page");
            var header = BuildSelectionHeader(
                "workflow.selection.createTitle",
                "workflow.selection.createDescription");
            header.Insert(0, AssetManagerControls.CreateIconButton(
                I18N.Get("workflow.selection.backToAssets"),
                "arrow_left.png",
                CancelDerivedAssetCreation,
                "ee4v-modification-workflow__selection-back"));
            page.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList(
                "ee4v-modification-workflow__creation-scroll");
            var selectedSource = GetSelectedVariantSource();
            if (selectedSource == null)
            {
                scroll.Add(CreateEmptyState(
                    I18N.Get("workflow.selection.assetNoSourceTitle"),
                    I18N.Get("workflow.selection.noSourceDescription")));
                scroll.Add(AssetManagerControls.CreateIconTextButton(
                    I18N.Get("workflow.selection.openManager"),
                    "library.png",
                    ShowAssetManagerSelection,
                    "ee4v-modification-workflow__selection-manager"));
                page.Add(scroll);
                return page;
            }

            if (_creationPrefab == null ||
                !selectedSource.Prefabs.Contains(_creationPrefab))
            {
                _creationPrefab = selectedSource.Prefabs[0];
            }

            var sourceField = UiTextFactory.Create(FormatVariantSource(selectedSource));
            var sourceInput = new FormInput(
                I18N.Get("workflow.selection.sourceItem"),
                sourceField);
            sourceInput.AddToClassList(
                "ee4v-modification-workflow__creation-source");
            scroll.Add(sourceInput);

            var layout = new VisualElement();
            layout.AddToClassList(
                "ee4v-modification-workflow__creation-layout");
            var previewColumn = new VisualElement();
            previewColumn.AddToClassList(
                "ee4v-modification-workflow__creation-preview-column");
            var preview = new PrefabScenePreview();
            preview.SetPrefab(_creationPrefab);
            previewColumn.Add(preview);
            var selector = new PrefabSelector(
                selectedSource.Prefabs);
            selector.SetValueWithoutNotify(_creationPrefab);
            selector.ValueChanged += prefab =>
            {
                _creationPrefab = prefab;
                preview.SetPrefab(prefab);
            };
            var prefabInput = new FormInput(
                I18N.Get("workflow.selection.sourcePrefab"),
                selector);
            previewColumn.Add(prefabInput);
            layout.Add(previewColumn);

            var fields = new VisualElement();
            fields.AddToClassList(
                "ee4v-modification-workflow__creation-fields");
            var name = AssetManagerControls.CreateTextField(
                I18N.Get("workflow.selection.name"));
            name.value = _derivedName;
            fields.Add(name);
            var description = AssetManagerControls.CreateTextField(
                I18N.Get("workflow.selection.assetDescription"));
            description.value = _derivedDescription;
            description.SetMultiline(true, 144f);
            fields.Add(description);
            AddFeedback(_avatarContext, fields);
            fields.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get("workflow.selection.createAndStart"),
                "add.png",
                () =>
                {
                    _derivedName = name.value;
                    _derivedDescription = description.value;
                    CreateDerivedAsset();
                },
                "ee4v-modification-workflow__primary-action",
                "ee4v-modification-workflow__creation-submit"));
            layout.Add(fields);
            scroll.Add(layout);
            page.Add(scroll);
            return page;
        }

        private VisualElement BuildSelectionHeader(
            string titleKey,
            string descriptionKey)
        {
            var header = new VisualElement();
            header.AddToClassList(
                "ee4v-modification-workflow__selection-header");
            var text = new VisualElement();
            text.AddToClassList(
                "ee4v-modification-workflow__selection-header-text");
            text.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__selection-title"));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__selection-description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            text.Add(description);
            header.Add(text);
            return header;
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
            _creationPrefab = null;
            _derivedName = item.Name + " Variant";
            _derivedDescription = string.Empty;
            StartDerivedAssetCreation();
        }

        private void CancelDerivedAssetCreation()
        {
            _creatingDerivedAsset = false;
            _avatarContext.Feedback = string.Empty;
            BuildWindow();
        }

        private IReadOnlyList<VariantSourceOption> GetVariantSources()
        {
            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                return _manager.SearchItems(new AssetItemQuery())
                    .Items
                    .Where(item => item != null && !item.IsArchived)
                    .Select(CreateVariantSource)
                    .Where(source => source.Prefabs.Count > 0)
                    .OrderBy(source => source.Item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
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
                Prefabs = DerivedAssetCreator.FindPrefabCandidates(
                    _manager.GetItemImportedAssetGuids(item.Id))
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
            if (string.IsNullOrEmpty(_creationItemId) ||
                _creationPrefab == null)
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetPrefabInvalid"),
                    HelpBoxMessageType.Error);
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

            try
            {
                var result = DerivedAssetCreator.Create(
                    new DerivedAssetCreationRequest
                    {
                        ParentItemId = _creationItemId,
                        Name = _derivedName,
                        Description = _derivedDescription,
                        Prefab = _creationPrefab
                    });
                _creatingDerivedAsset = false;
                SelectDerivedAsset(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
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

        private static string FormatVariantSource(
            VariantSourceOption source)
        {
            return source?.Item?.Name ?? string.Empty;
        }
    }
}
