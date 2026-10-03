using Ee4v.Core.EditorIntegration;
using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetProtection;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Simulation;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.FaceExpression;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView
    {
        private void SyncPreviewSelection()
        {
            if (_scenePreview == null)
            {
                return;
            }
            if (_mode == ModificationEditorMode.Composition)
            {
                _scenePreview.SetListSelection(null, null);
            }
            else if (_currentCategory == WorkflowCategory.Material)
            {
                _scenePreview.SetListSelection(null, _selectedMaterial);
            }
            else if (_currentCategory == WorkflowCategory.ShapeParts &&
                     _shapePartsSection == ShapePartsSection.Parts)
            {
                _scenePreview.SetListSelection(_selectedPartKey, null);
            }
            else
            {
                _scenePreview.SetListSelection(null, null);
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
            card.SetSelected(_selectedPrefabSiblingIndex == child.SiblingIndex);
            card.RegisterCallback<ClickEvent>(
                _ => SelectPrefabCard(child.SiblingIndex, child.Name));
            if (HasMeshInPrefabScope(child.SiblingIndex))
            {
                var visibility = CreatePrefabVisibilityButton(
                    !_hiddenPrefabSiblingIndices.Contains(child.SiblingIndex),
                    I18N.Get(_hiddenPrefabSiblingIndices.Contains(child.SiblingIndex)
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
                var entry = CreatePrefabRootEntry(child.SiblingIndex);
                var menu = new GenericMenu();
                AddActiveSelfMenuItem(menu, entry);
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
                _basePrefabHidden = !_basePrefabHidden;
            }
            else if (_hiddenPrefabSiblingIndices.Contains(siblingIndex))
            {
                _hiddenPrefabSiblingIndices.Remove(siblingIndex);
            }
            else
            {
                _hiddenPrefabSiblingIndices.Add(siblingIndex);
            }
            RefreshPrefabPreviewVisibilityControls(siblingIndex);
            _scenePreview?.SetHiddenPrefabs(
                _basePrefabHidden,
                _hiddenPrefabSiblingIndices);
        }

        private void RefreshPrefabPreviewVisibilityControls(
            int siblingIndex)
        {
            var hidden = siblingIndex == -1
                ? _basePrefabHidden
                : _hiddenPrefabSiblingIndices.Contains(siblingIndex);
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
            if (_selectedPrefabSiblingIndex == siblingIndex)
            {
                return;
            }

            EndBodyScaleDrag();
            SaveBodyScalePrefab();
            if (_bodyScaleDirty) { return; }
            _selectedPrefabSiblingIndex = siblingIndex;
            _selectedPrefabName = name ?? string.Empty;
            _selectedBodyPart = null;
            _selectedPartKey = null;
            _selectedMaterial = null;
            if (siblingIndex.HasValue &&
                _currentCategory != WorkflowCategory.Overview &&
                _currentCategory != WorkflowCategory.Execution &&
                _currentCategory != WorkflowCategory.Material)
            {
                _currentCategory = WorkflowCategory.ShapeParts;
            }
            _assetFeedback = string.Empty;
            BuildWindow();
        }

        private VisualElement BuildObjectControls()
        {
            _objectRows.Clear();
            var panel = new VisualElement();
            panel.AddToClassList(
                "ee4v-modification-workflow__objects-content");
            if (!IsEditableWorkflowPrefab())
            {
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.assets.protected"),
                    HelpBoxMessageType.Warning));
                return panel;
            }

            IReadOnlyList<PrefabObjectEntry> entries;
            try
            {
                entries = ReadPrefabObjects();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.assets.readFailed"),
                    HelpBoxMessageType.Error));
                return panel;
            }

            IReadOnlyList<PrefabObjectEntry> displayed = entries;
            if (_selectedBodyPart.HasValue)
            {
                displayed = FilterPrefabObjectsByBodyPart(
                    entries, _selectedBodyPart.Value);
            }
            if (displayed.Count == 0)
            {
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.objects.empty"),
                    HelpBoxMessageType.Info));
                return panel;
            }

            var list = new VisualElement();
            list.AddToClassList(
                "ee4v-modification-workflow__objects-list");
            panel.Add(list);
            foreach (var prefabSiblingIndex in GetDisplayedPrefabScopes())
            {
                var group = displayed.Where(entry =>
                    entry.PrefabSiblingIndex == prefabSiblingIndex).ToArray();
                if (group.Length == 0)
                {
                    continue;
                }
                if (!_selectedPrefabSiblingIndex.HasValue &&
                    prefabSiblingIndex >= 0)
                {
                    var content = new VisualElement();
                    AddObjectHierarchy(content, group);
                    list.Add(BuildCollapsiblePrefabGroup(
                        prefabSiblingIndex,
                        content,
                        _expandedPartPrefabGroups,
                        true));
                    continue;
                }
                AddObjectHierarchy(list, group);
            }
            MarkLastListItem(list);
            return panel;
        }

        private static IReadOnlyList<PrefabObjectEntry>
            FilterPrefabObjectsByBodyPart(
                IReadOnlyList<PrefabObjectEntry> entries,
                BodyPartCategory bodyPart)
        {
            var entriesByScope = new Dictionary<int,
                Dictionary<string, PrefabObjectEntry>>();
            foreach (var entry in entries)
            {
                if (!entriesByScope.TryGetValue(
                        entry.PrefabSiblingIndex, out var byPath))
                {
                    byPath = new Dictionary<string, PrefabObjectEntry>(
                        StringComparer.Ordinal);
                    entriesByScope.Add(entry.PrefabSiblingIndex, byPath);
                }
                byPath.Add(GetPrefabObjectPathKey(entry.SiblingPath), entry);
            }

            var included = new HashSet<PrefabObjectEntry>();
            foreach (var entry in entries)
            {
                if (!entry.Categories.Any(category =>
                        MatchesBodyPartGroup(bodyPart, category)))
                {
                    continue;
                }
                included.Add(entry);
                var byPath = entriesByScope[entry.PrefabSiblingIndex];
                for (var length = 0; length < entry.SiblingPath.Length;
                     length++)
                {
                    var ancestorPath = GetPrefabObjectPathKey(
                        entry.SiblingPath.Take(length));
                    if (byPath.TryGetValue(ancestorPath, out var ancestor))
                    {
                        included.Add(ancestor);
                    }
                }
            }
            return entries.Where(included.Contains).ToArray();
        }

        private static string GetPrefabObjectPathKey(
            IEnumerable<int> siblingPath)
        {
            return string.Join("/", siblingPath.Select(index =>
                index.ToString()).ToArray());
        }

        private void AddObjectHierarchy(
            VisualElement container,
            IReadOnlyList<PrefabObjectEntry> entries)
        {
            var roots = new List<PrefabObjectNode>();
            var ancestors = new Stack<PrefabObjectNode>();
            foreach (var entry in entries)
            {
                while (ancestors.Count > 0 &&
                       !IsAncestorPath(
                           ancestors.Peek().Entry.SiblingPath,
                           entry.SiblingPath))
                {
                    ancestors.Pop();
                }

                var node = new PrefabObjectNode { Entry = entry };
                if (ancestors.Count == 0)
                {
                    roots.Add(node);
                }
                else
                {
                    ancestors.Peek().Children.Add(node);
                }
                ancestors.Push(node);
            }

            foreach (var root in roots)
            {
                container.Add(BuildObjectNode(root));
            }
        }

        private static bool IsAncestorPath(
            IReadOnlyList<int> ancestor,
            IReadOnlyList<int> descendant)
        {
            if (ancestor.Count >= descendant.Count)
            {
                return false;
            }
            for (var index = 0; index < ancestor.Count; index++)
            {
                if (ancestor[index] != descendant[index])
                {
                    return false;
                }
            }
            return true;
        }

        private VisualElement BuildObjectNode(PrefabObjectNode node)
        {
            var row = BuildObjectRow(node.Entry);
            if (node.Children.Count == 0)
            {
                var placeholder = new VisualElement();
                placeholder.AddToClassList(
                    "ee4v-modification-workflow__object-disclosure-placeholder");
                row.Insert(0, placeholder);
                return row;
            }

            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__object-group");
            var content = new VisualElement();
            content.AddToClassList(
                "ee4v-modification-workflow__object-group-content");
            foreach (var child in node.Children)
            {
                content.Add(BuildObjectNode(child));
            }

            var key = PrefabScenePreview.GetPartKey(
                node.Entry.PrefabSiblingIndex,
                node.Entry.SiblingPath);
            UiButton toggle = null;
            void UpdateFoldout()
            {
                var expanded = _expandedObjectGroups.Contains(key);
                toggle.SetIcon(AssetManagerControls.LoadFluentIconState(
                    expanded ? "chevron_down.png" : "chevron_right.png",
                    UiSizeTokens.Size12));
                content.EnableInClassList(
                    "ee4v-modification-workflow__hidden", !expanded);
                group.EnableInClassList(
                    "ee4v-modification-workflow__object-group--header-last",
                    !expanded);
            }
            void ToggleFoldout()
            {
                if (!_expandedObjectGroups.Add(key))
                {
                    _expandedObjectGroups.Remove(key);
                }
                UpdateFoldout();
            }
            toggle = new UiButton(
                string.Empty,
                ToggleFoldout,
                variant: UiButtonVariant.Ghost);
            toggle.AddToClassList(
                "ee4v-modification-workflow__object-disclosure");
            row[0].RegisterCallback<ClickEvent>(_ => ToggleFoldout());
            row.Insert(0, toggle);
            group.Add(row);
            group.Add(content);
            UpdateFoldout();
            return group;
        }

        private static void MarkLastListItem(VisualElement list)
        {
            if (list.childCount == 0)
            {
                return;
            }

            var last = list[list.childCount - 1];
            var isPrefabGroup = last.ClassListContains(
                "ee4v-modification-workflow__prefab-group");
            var isObjectGroup = last.ClassListContains(
                "ee4v-modification-workflow__object-group");
            if (!isPrefabGroup && !isObjectGroup)
            {
                last.AddToClassList(
                    "ee4v-modification-workflow__list-last-row");
                return;
            }

            last.AddToClassList(
                isPrefabGroup
                    ? "ee4v-modification-workflow__prefab-group--last"
                    : "ee4v-modification-workflow__object-group--last");
            MarkLastListItem(last[last.childCount - 1]);
        }

        private IEnumerable<int> GetDisplayedPrefabScopes()
        {
            return _selectedPrefabSiblingIndex.HasValue
                ? new[] { _selectedPrefabSiblingIndex.Value }
                : new[] { -1 }.Concat(_prefabSiblingIndices);
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
            var inactive = !_workingObject.transform
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
                var previewHidden = _hiddenPrefabSiblingIndices.Contains(
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
                    IsPrefabGroupCheckboxChecked(prefabSiblingIndex));
                visibility.SetEnabled(IsEditableWorkflowPrefab());
                visibility.RegisterValueChangedCallback(evt =>
                    ChangePrefabGroupVisibility(
                        prefabSiblingIndex, evt.newValue));
                actions.Add(visibility);
            }
            if (actions.childCount > 0)
            {
                header.Add(actions);
            }
            header.RegisterCallback<ContextClickEvent>(evt =>
                ShowActiveSelfContextMenu(
                    evt, CreatePrefabRootEntry(prefabSiblingIndex)));
            group.Add(header);
            group.Add(content);
            UpdateFoldout();
            return group;
        }

        private string GetPrefabGroupName(int prefabSiblingIndex)
        {
            return prefabSiblingIndex >= 0 &&
                   prefabSiblingIndex < _workingObject.transform.childCount
                ? _workingObject.transform.GetChild(prefabSiblingIndex).name
                : _workingObject.name;
        }

        private PrefabObjectEntry CreatePrefabRootEntry(
            int prefabSiblingIndex)
        {
            var child = _workingObject.transform
                .GetChild(prefabSiblingIndex).gameObject;
            return new PrefabObjectEntry
            {
                PrefabSiblingIndex = prefabSiblingIndex,
                PrefabName = child.name,
                SiblingPath = Array.Empty<int>(),
                Name = child.name,
                IsActiveSelf = child.activeSelf
            };
        }

        private bool HasMeshInPrefabScope(int prefabSiblingIndex)
        {
            if (_workingObject == null)
            {
                return false;
            }
            var root = _workingObject.transform;
            var meshPresence = new Dictionary<Transform, bool>();
            if (prefabSiblingIndex >= 0)
            {
                return prefabSiblingIndex < root.childCount &&
                       HasMeshInSubtree(
                           root.GetChild(prefabSiblingIndex),
                           meshPresence);
            }

            if (HasPartMesh(root))
            {
                return true;
            }
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject) &&
                    HasMeshInSubtree(child, meshPresence))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsPrefabGroupCheckboxChecked(int prefabSiblingIndex)
        {
            if (_workingObject == null || prefabSiblingIndex < 0 ||
                prefabSiblingIndex >= _workingObject.transform.childCount)
            {
                return false;
            }
            var child = _workingObject.transform
                .GetChild(prefabSiblingIndex).gameObject;
            return !string.Equals(child.tag, "EditorOnly",
                StringComparison.Ordinal);
        }

        private void ChangePrefabGroupVisibility(
            int prefabSiblingIndex,
            bool visible)
        {
            var entry = _objectEntriesCache?.FirstOrDefault(candidate =>
                candidate.PrefabSiblingIndex == prefabSiblingIndex &&
                candidate.SiblingPath.Length == 0);
            if (entry == null)
            {
                ShowAssetError("workflow.assets.saveFailed");
                return;
            }
            if (entry.IsVisible == visible)
            {
                return;
            }
            ChangePrefabObject(entry, visible);
            if (entry.IsVisible != visible)
            {
                return;
            }
            if (entry.IsActiveSelf && entry.IsVisible)
            {
                _hiddenPrefabSiblingIndices.Remove(prefabSiblingIndex);
            }
            else
            {
                _hiddenPrefabSiblingIndices.Add(prefabSiblingIndex);
            }
            _scenePreview?.SetHiddenPrefabs(
                _basePrefabHidden, _hiddenPrefabSiblingIndices);
            RefreshPrefabPreviewVisibilityControls(prefabSiblingIndex);
        }

        private VisualElement BuildObjectRow(PrefabObjectEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-modification-workflow__object-row");
            var key = PrefabScenePreview.GetPartKey(
                entry.PrefabSiblingIndex, entry.SiblingPath);
            row.EnableInClassList(
                "ee4v-modification-workflow__object-row--selected",
                key == _selectedPartKey);
            var text = new VisualElement();
            text.AddToClassList(
                "ee4v-modification-workflow__object-text");
            text.RegisterCallback<ClickEvent>(_ => SelectPartRow(key));
            var name = UiTextFactory.Create(
                entry.Name,
                "ee4v-modification-workflow__object-name");
            name.tooltip = entry.Path;
            text.Add(name);
            var path = UiTextFactory.Create(
                entry.Path,
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__object-path");
            path.tooltip = entry.Path;
            text.Add(path);
            row.Add(text);
            var actions = new VisualElement();
            actions.AddToClassList(
                "ee4v-modification-workflow__object-actions");
            UiButton previewVisibility = null;
            if (entry.HasMeshInSubtree)
            {
                previewVisibility = CreatePrefabVisibilityButton(
                    true,
                    string.Empty,
                    () => TogglePartPreviewVisibility(entry),
                    "ee4v-modification-workflow__object-preview-visibility");
                actions.Add(previewVisibility);
            }
            var visibility = UiTextFactory.CreateToggle();
            visibility.AddToClassList(
                "ee4v-modification-workflow__object-visibility");
            visibility.RegisterValueChangedCallback(evt =>
                ChangePrefabObject(entry, evt.newValue));
            actions.Add(visibility);
            row.Add(actions);
            row.RegisterCallback<ContextClickEvent>(evt =>
                ShowActiveSelfContextMenu(evt, entry));
            var state = new PrefabObjectRowState
            {
                Row = row,
                Name = name,
                Path = path,
                Visibility = visibility,
                PreviewVisibility = previewVisibility
            };
            _objectRows[entry] = state;
            UpdateObjectRow(entry, state);
            return row;
        }

        private void OnPreviewObjectClicked(string partKey, Material material)
        {
            if (_mode == ModificationEditorMode.Composition ||
                _currentCategory == WorkflowCategory.Overview ||
                _controlsHost == null)
            {
                return;
            }

            var prefabSiblingIndex = GetPreviewPrefabSiblingIndex(partKey);
            if (_currentCategory == WorkflowCategory.Material)
            {
                if (material == null)
                {
                    return;
                }
                if (_selectedBodyPart.HasValue &&
                    !GetAvatarMaterials().Any(entry =>
                        entry.Material == material &&
                        entry.Usages.Any(usage =>
                            usage.PrefabSiblingIndex == prefabSiblingIndex &&
                            GetMaterialUsageCategories(usage, material)
                                .Any(MatchesSelectedBodyPart))))
                {
                    _selectedBodyPart = null;
                }
                _selectedMaterial = material;
                if (prefabSiblingIndex >= 0)
                {
                    _expandedMaterialPrefabGroups.Add(prefabSiblingIndex);
                }
                InvalidateAppearanceControls(AppearancePanel.Material);
                ShowCategory(WorkflowCategory.Material, false);
                ScrollToMaterial(material, prefabSiblingIndex);
                return;
            }

            var entries = ReadPrefabObjects();
            var entry = entries.FirstOrDefault(candidate =>
                PrefabScenePreview.GetPartKey(
                    candidate.PrefabSiblingIndex,
                    candidate.SiblingPath) == partKey);
            while (entry == null && !string.IsNullOrEmpty(partKey))
            {
                var separator = partKey.LastIndexOf('/');
                partKey = separator < 0
                    ? string.Empty
                    : partKey.Substring(0, separator);
                entry = entries.FirstOrDefault(candidate =>
                    PrefabScenePreview.GetPartKey(
                        candidate.PrefabSiblingIndex,
                        candidate.SiblingPath) == partKey);
            }
            if (entry == null)
            {
                return;
            }

            if (_shapePartsSection == ShapePartsSection.Shape)
            {
                EndBodyScaleDrag();
            }
            _shapePartsSection = ShapePartsSection.Parts;
            if (_selectedBodyPart.HasValue &&
                !FilterPrefabObjectsByBodyPart(
                    entries, _selectedBodyPart.Value).Contains(entry))
            {
                _selectedBodyPart = null;
            }
            var selectedKey = PrefabScenePreview.GetPartKey(
                entry.PrefabSiblingIndex, entry.SiblingPath);
            if (entry.PrefabSiblingIndex >= 0)
            {
                _expandedPartPrefabGroups.Add(entry.PrefabSiblingIndex);
            }
            var separatorIndex = selectedKey.IndexOf('/');
            while (separatorIndex >= 0)
            {
                _expandedObjectGroups.Add(
                    selectedKey.Substring(0, separatorIndex));
                separatorIndex = selectedKey.IndexOf('/', separatorIndex + 1);
            }
            _selectedPartKey = selectedKey;
            InvalidateAppearanceControls(AppearancePanel.Parts);
            ShowCategory(WorkflowCategory.ShapeParts, false);
            ScrollToSelectedPart();
        }

        private void OnPreviewSelectionCleared()
        {
            if (_mode == ModificationEditorMode.Composition ||
                _controlsHost == null)
            {
                return;
            }
            if (_currentCategory == WorkflowCategory.Material)
            {
                _selectedMaterial = null;
                InvalidateAppearanceControls(AppearancePanel.Material);
                ShowCategory(WorkflowCategory.Material, false);
                return;
            }

            _selectedPartKey = null;
            foreach (var row in _objectRows.Values)
            {
                row.Row.EnableInClassList(
                    "ee4v-modification-workflow__object-row--selected",
                    false);
            }
            SyncPreviewSelection();
        }

        private int GetPreviewPrefabSiblingIndex(string partKey)
        {
            var separator = partKey.IndexOf('/');
            var first = separator < 0
                ? partKey
                : partKey.Substring(0, separator);
            return int.TryParse(first, out var index) &&
                   _prefabSiblingIndices.Contains(index)
                ? index
                : -1;
        }

        private void SelectPartRow(string key)
        {
            _selectedPartKey = key;
            foreach (var pair in _objectRows)
            {
                pair.Value.Row.EnableInClassList(
                    "ee4v-modification-workflow__object-row--selected",
                    PrefabScenePreview.GetPartKey(
                        pair.Key.PrefabSiblingIndex,
                        pair.Key.SiblingPath) == key);
            }
            SyncPreviewSelection();
        }

        private void ScrollToSelectedPart()
        {
            var row = _objectRows.FirstOrDefault(pair =>
                PrefabScenePreview.GetPartKey(
                    pair.Key.PrefabSiblingIndex,
                    pair.Key.SiblingPath) == _selectedPartKey).Value?.Row;
            if (row != null)
            {
                _controlsHost.schedule.Execute(() =>
                {
                    if (row.panel != null)
                    {
                        _controlsHost.ScrollTo(row);
                    }
                });
            }
        }

        private void ScrollToMaterial(
            Material material,
            int prefabSiblingIndex)
        {
            var row = _controlsHost.Query<NavigationItem>(
                    className: "ee4v-modification-workflow__material-item")
                .ToList()
                .FirstOrDefault(item =>
                    item.userData is KeyValuePair<Material, int> choice &&
                    choice.Key == material &&
                    choice.Value == prefabSiblingIndex);
            if (row != null)
            {
                _controlsHost.schedule.Execute(() =>
                {
                    if (row.panel != null)
                    {
                        _controlsHost.ScrollTo(row);
                    }
                });
            }
        }

        private void ShowActiveSelfContextMenu(
            ContextClickEvent evt,
            PrefabObjectEntry entry)
        {
            var menu = new GenericMenu();
            AddActiveSelfMenuItem(menu, entry);
            menu.ShowAsContext();
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private void AddActiveSelfMenuItem(
            GenericMenu menu,
            PrefabObjectEntry entry)
        {
            menu.AddItem(
                UiTextFactory.CreateGuiContent(
                    I18N.Get(entry.IsActiveSelf
                        ? "workflow.objects.disable"
                        : "workflow.objects.enable")),
                false,
                () => TogglePrefabObjectActiveSelf(entry));
        }

        private void UpdateObjectRow(
            PrefabObjectEntry entry,
            PrefabObjectRowState state,
            bool updatePreviewIcon = true)
        {
            var parentHidden = entry.IsActiveSelf &&
                               !entry.ParentActiveInHierarchy;
            state.Row.EnableInClassList(
                "ee4v-modification-workflow__object-row--inactive",
                !entry.IsActiveSelf);
            state.Name.SetColor(entry.IsActiveSelf
                ? UiColorTokens.TextPrimary
                : UiColorTokens.TextDisabled);
            state.Path.SetColor(entry.IsActiveSelf
                ? UiColorTokens.TextMuted
                : UiColorTokens.TextDisabled);
            state.Row.tooltip = parentHidden
                ? I18N.Get("workflow.objects.parentHidden")
                : string.Empty;
            state.Visibility.SetValueWithoutNotify(entry.IsVisible);
            state.Visibility.tooltip = I18N.Get(entry.IsVisible
                ? "workflow.objects.turnOff"
                : "workflow.objects.turnOn");
            if (!updatePreviewIcon || state.PreviewVisibility == null)
            {
                return;
            }
            var previewVisible = !_hiddenPreviewParts.Contains(
                PrefabScenePreview.GetPartKey(
                    entry.PrefabSiblingIndex, entry.SiblingPath));
            var previewTooltip = I18N.Get(previewVisible
                ? "workflow.assets.clickToHide"
                : "workflow.assets.clickToShow");
            state.PreviewVisibility.tooltip = previewTooltip;
            state.PreviewVisibility.SetIcon(FluentUiIcons.CreateState(
                previewVisible ? "eye.png" : "eye_off.png",
                UiSizeTokens.Size18,
                previewTooltip));
        }

        private void TogglePartPreviewVisibility(PrefabObjectEntry entry)
        {
            var key = PrefabScenePreview.GetPartKey(
                entry.PrefabSiblingIndex, entry.SiblingPath);
            if (!_hiddenPreviewParts.Add(key))
            {
                _hiddenPreviewParts.Remove(key);
            }
            _scenePreview?.SetHiddenParts(_hiddenPreviewParts);
            if (_objectRows.TryGetValue(entry, out var row))
            {
                UpdateObjectRow(entry, row);
            }
        }

        private IReadOnlyList<PrefabObjectEntry> ReadPrefabObjects()
        {
            if (_objectEntriesCache != null)
            {
                return _objectEntriesCache;
            }
            var root = _workingObject;
            {
                var result = new List<PrefabObjectEntry>();
                var classifier = new PrefabPartClassifier(root);
                var meshPresence = new Dictionary<Transform, bool>();
                var excludedPrefixes =
                    AssetManagerSettings.ExcludedPartPrefixes;
                var scopes = _selectedPrefabSiblingIndex.HasValue
                    ? new[] { _selectedPrefabSiblingIndex.Value }
                    : new[] { -1 }.Concat(_prefabSiblingIndices).ToArray();
                foreach (var prefabSiblingIndex in scopes)
                {
                    var selected = ResolveObjectPrefab(
                        root,
                        prefabSiblingIndex,
                        _selectedPrefabSiblingIndex.HasValue
                            ? _selectedPrefabName
                            : null);
                    if (selected == null)
                    {
                        throw new InvalidOperationException(
                            "The selected Prefab is no longer present.");
                    }
                    if (IsExcludedPartName(
                            selected.name,
                            excludedPrefixes))
                    {
                        continue;
                    }

                    void AddEntry(
                        Transform target,
                        int[] indices,
                        string objectPath)
                    {
                        var gameObject = target.gameObject;
                        result.Add(new PrefabObjectEntry
                        {
                            PrefabSiblingIndex = prefabSiblingIndex,
                            PrefabName = selected.name,
                            SiblingPath = indices,
                            Name = target.name,
                            Path = objectPath,
                            IsVisible = !string.Equals(
                                gameObject.tag, "EditorOnly",
                                StringComparison.Ordinal),
                            IsActiveSelf = gameObject.activeSelf,
                            ParentActiveInHierarchy =
                                target.parent.gameObject.activeInHierarchy,
                            IsVisibleInHierarchy =
                                gameObject.activeInHierarchy,
                            HasMeshInSubtree = HasMeshInSubtree(
                                target, meshPresence),
                            Categories = classifier.Classify(target)
                        });
                    }

                    if (!_selectedPrefabSiblingIndex.HasValue &&
                        prefabSiblingIndex >= 0)
                    {
                        AddEntry(selected.transform, Array.Empty<int>(),
                            selected.name);
                    }

                    void Visit(
                        Transform parent,
                        IReadOnlyList<int> parentIndices,
                        string parentPath)
                    {
                        for (var index = 0; index < parent.childCount; index++)
                        {
                            var child = parent.GetChild(index);
                            if (IsExcludedPartName(
                                    child.name,
                                    excludedPrefixes))
                            {
                                continue;
                            }
                            if (parent == selected.transform &&
                                prefabSiblingIndex == -1 &&
                                PrefabUtility.IsAnyPrefabInstanceRoot(
                                    child.gameObject))
                            {
                                continue;
                            }
                            var indices = parentIndices.Concat(
                                new[] { index }).ToArray();
                            var objectPath = string.IsNullOrEmpty(parentPath)
                                ? child.name
                                : parentPath + "/" + child.name;
                            AddEntry(child, indices, objectPath);
                            Visit(child, indices, objectPath);
                        }
                    }
                    Visit(
                        selected.transform,
                        Array.Empty<int>(),
                        _selectedPrefabSiblingIndex.HasValue
                            ? string.Empty
                            : selected.name);
                }
                _objectEntriesCache = result.ToArray();
                return _objectEntriesCache;
            }
        }

        private static bool IsExcludedPartName(
            string name,
            IReadOnlyList<string> excludedPrefixes)
        {
            return !string.IsNullOrEmpty(name) &&
                   excludedPrefixes.Any(prefix =>
                       name.StartsWith(
                           prefix,
                           StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasMeshInSubtree(
            Transform target,
            Dictionary<Transform, bool> meshPresence)
        {
            if (meshPresence.TryGetValue(target, out var hasMesh))
            {
                return hasMesh;
            }
            hasMesh = HasPartMesh(target);
            for (var index = 0; index < target.childCount; index++)
            {
                var childHasMesh = HasMeshInSubtree(
                    target.GetChild(index), meshPresence);
                hasMesh |= childHasMesh;
            }
            meshPresence.Add(target, hasMesh);
            return hasMesh;
        }

        private static bool HasPartMesh(Transform target)
        {
            if (target.GetComponents<SkinnedMeshRenderer>().Any(renderer =>
                    renderer.sharedMesh != null))
            {
                return true;
            }
            return target.GetComponent<MeshRenderer>() != null &&
                   target.GetComponents<MeshFilter>().Any(filter =>
                       filter.sharedMesh != null);
        }

        private void TogglePrefabObjectActiveSelf(PrefabObjectEntry entry)
        {
            try
            {
                EndBodyScaleDrag();
                SaveBodyScalePrefab();
                if (_bodyScaleDirty)
                {
                    throw new InvalidOperationException(
                        "The derived Prefab size could not be saved.");
                }
                if (!IsEditableWorkflowPrefab() ||
                    !FlushPendingPartVisibility())
                {
                    throw new InvalidOperationException(
                        "The selected Prefab cannot be edited.");
                }
                var gameObject = ResolvePartObject(_workingObject, entry);
                Undo.RecordObject(gameObject, "Toggle Prefab object");
                gameObject.SetActive(!gameObject.activeSelf);
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(
                        gameObject);
                }
                _workingSceneDirty = true;
                if (entry.PrefabSiblingIndex >= 0 &&
                    entry.SiblingPath.Length == 0)
                {
                    if (_workingObject.transform
                            .GetChild(entry.PrefabSiblingIndex)
                            .gameObject.activeSelf &&
                        IsPrefabGroupCheckboxChecked(entry.PrefabSiblingIndex))
                    {
                        _hiddenPrefabSiblingIndices.Remove(
                            entry.PrefabSiblingIndex);
                    }
                    else
                    {
                        _hiddenPrefabSiblingIndices.Add(
                            entry.PrefabSiblingIndex);
                    }
                    _scenePreview?.SetHiddenPrefabs(
                        _basePrefabHidden, _hiddenPrefabSiblingIndices);
                    RefreshPrefabPreviewVisibilityControls(
                        entry.PrefabSiblingIndex);
                    RefreshPrefabHeaderActiveSelf(
                        entry.PrefabSiblingIndex,
                        _workingObject.transform
                            .GetChild(entry.PrefabSiblingIndex)
                            .gameObject.activeSelf);
                }
                _scenePreview?.ReloadPrefabPreservingView(_workingObject);
                _assetFeedback = string.Empty;
                ClearAppearanceCaches();
                InvalidateVariantSaveStatus();
                ShowCategory(_currentCategory, false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowAssetError("workflow.assets.saveFailed");
            }
        }

        private void ChangePrefabObject(
            PrefabObjectEntry entry,
            bool visible)
        {
            if (entry.IsVisible == visible)
            {
                return;
            }
            try
            {
                if (!IsEditableWorkflowPrefab())
                {
                    throw new InvalidOperationException(
                        "The selected Prefab is not a derived asset.");
                }
                var assetPath = GetWorkingAssetPath();
                if (_pendingPartVisibility.Count > 0 &&
                    !string.Equals(_pendingPartAssetPath, assetPath,
                        StringComparison.Ordinal) &&
                    !FlushPendingPartVisibility())
                {
                    BuildWindow();
                    return;
                }
                if (!_pendingPartVisibility.TryGetValue(entry,
                        out var pending))
                {
                    pending = new PendingPartVisibility
                    {
                        Entry = entry,
                        OriginalVisible = entry.IsVisible,
                        OriginalActiveSelf = entry.IsActiveSelf,
                        RestoreKey = "ee4v.asset-manager.object-tag." +
                            AssetDatabase.AssetPathToGUID(assetPath) + "." +
                            entry.PrefabSiblingIndex + "." +
                            string.Join(".", entry.SiblingPath.Select(index =>
                                index.ToString()).ToArray()) + "." +
                            entry.Name
                    };
                    _pendingPartVisibility.Add(entry, pending);
                }
                pending.Visible = visible;
                _pendingPartAssetPath = assetPath;
                var returningToOriginal =
                    visible == pending.OriginalVisible;
                var activeSelf = returningToOriginal
                    ? pending.OriginalActiveSelf
                    : visible;
                if (returningToOriginal)
                {
                    _pendingPartVisibility.Remove(entry);
                }
                UpdateCachedObjectVisibility(entry, visible, activeSelf);
                if (entry.PrefabSiblingIndex >= 0 &&
                    entry.SiblingPath.Length == 0)
                {
                    RefreshPrefabHeaderActiveSelf(
                        entry.PrefabSiblingIndex, activeSelf);
                    RefreshPrefabGroupVisibility(
                        entry.PrefabSiblingIndex, activeSelf, visible);
                }
                _avatarMaterialsCache = null;
                InvalidateAppearanceControls(AppearancePanel.Material);
                _scenePreview?.SetPartVisibility(
                    _workingObject,
                    entry.PrefabSiblingIndex,
                    entry.SiblingPath,
                    entry.Name,
                    activeSelf,
                    visible);
                var restoreTags = new List<PartTagChange>();
                ApplyPartVisibility(_workingObject, pending, restoreTags);
                foreach (var tag in restoreTags)
                {
                    if (tag.Visible)
                    {
                        EditorPrefs.DeleteKey(tag.RestoreKey);
                    }
                    else if (tag.OriginalTag != null)
                    {
                        EditorPrefs.SetString(tag.RestoreKey,
                            tag.OriginalTag);
                    }
                }
                _pendingPartVisibility.Clear();
                _pendingPartAssetPath = null;
                _workingSceneDirty = true;
                InvalidateVariantSaveStatus();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowAssetError("workflow.assets.saveFailed");
            }
        }

        private void UpdateCachedObjectVisibility(
            PrefabObjectEntry entry,
            bool visible,
            bool activeSelf)
        {
            var previousActiveSelf = entry.IsActiveSelf;
            entry.IsVisible = visible;
            entry.IsActiveSelf = activeSelf;
            entry.IsVisibleInHierarchy =
                entry.ParentActiveInHierarchy && activeSelf;
            if (_objectRows.TryGetValue(entry, out var changedRow))
            {
                UpdateObjectRow(entry, changedRow, false);
            }
            if (_objectEntriesCache == null ||
                previousActiveSelf == activeSelf)
            {
                return;
            }
            foreach (var candidate in _objectEntriesCache)
            {
                if (candidate == entry ||
                    candidate.PrefabSiblingIndex !=
                        entry.PrefabSiblingIndex ||
                    !IsAncestorPath(
                        entry.SiblingPath, candidate.SiblingPath))
                {
                    continue;
                }
                var parentActive =
                    IsCurrentPartParentActive(candidate);
                if (candidate.ParentActiveInHierarchy == parentActive)
                {
                    continue;
                }
                candidate.ParentActiveInHierarchy = parentActive;
                candidate.IsVisibleInHierarchy =
                    candidate.ParentActiveInHierarchy &&
                    candidate.IsActiveSelf;
                if (_objectRows.TryGetValue(candidate, out var row))
                {
                    UpdateObjectRow(candidate, row, false);
                }
            }
        }

        private bool IsCurrentPartParentActive(PrefabObjectEntry entry)
        {
            var selected = ResolveObjectPrefab(
                _workingObject, entry.PrefabSiblingIndex, entry.PrefabName);
            if (selected == null)
            {
                return false;
            }
            var active = _workingObject.activeSelf;
            var current = selected.transform;
            if (entry.PrefabSiblingIndex >= 0 &&
                entry.SiblingPath.Length > 0)
            {
                active &= GetCurrentPartActiveSelf(
                    entry.PrefabSiblingIndex, Array.Empty<int>(), current);
            }
            var path = new int[entry.SiblingPath.Length];
            for (var depth = 0; depth < entry.SiblingPath.Length - 1;
                 depth++)
            {
                var index = entry.SiblingPath[depth];
                if (index < 0 || index >= current.childCount)
                {
                    return false;
                }
                current = current.GetChild(index);
                path[depth] = index;
                active &= GetCurrentPartActiveSelf(
                    entry.PrefabSiblingIndex,
                    path.Take(depth + 1).ToArray(),
                    current);
            }
            return active;
        }

        private bool GetCurrentPartActiveSelf(
            int prefabSiblingIndex,
            IReadOnlyList<int> siblingPath,
            Transform target)
        {
            foreach (var pending in _pendingPartVisibility.Values)
            {
                if (pending.Entry.PrefabSiblingIndex ==
                        prefabSiblingIndex &&
                    pending.Entry.SiblingPath.SequenceEqual(siblingPath))
                {
                    return pending.Entry.IsActiveSelf;
                }
            }
            return target.gameObject.activeSelf;
        }

        private bool FlushPendingPartVisibility()
        {
            if (_pendingPartVisibility.Count == 0)
            {
                return true;
            }

            EndBodyScaleDrag();
            SaveBodyScalePrefab();
            if (_bodyScaleDirty)
            {
                return false;
            }

            var changes = _pendingPartVisibility.Values.ToArray();
            _pendingPartVisibility.Clear();
            _pendingPartAssetPath = null;
            try
            {
                var restoreTags = new List<PartTagChange>();
                foreach (var change in changes)
                {
                    ApplyPartVisibility(_workingObject, change,
                        restoreTags);
                }

                foreach (var tag in restoreTags)
                {
                    if (tag.Visible)
                    {
                        EditorPrefs.DeleteKey(tag.RestoreKey);
                    }
                    else if (tag.OriginalTag != null)
                    {
                        EditorPrefs.SetString(
                            tag.RestoreKey, tag.OriginalTag);
                    }
                }
                _workingSceneDirty = true;
                _assetFeedback = string.Empty;
                InvalidateVariantSaveStatus();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _assetFeedback = I18N.Get("workflow.assets.saveFailed");
                return false;
            }
        }

        private static void ApplyPartVisibility(
            GameObject root,
            PendingPartVisibility change,
            ICollection<PartTagChange> restoreTags)
        {
            var gameObject = ResolvePartObject(root, change.Entry);
            Undo.RecordObject(gameObject, "Change Variant part visibility");
            if (change.Visible)
            {
                if (string.Equals(gameObject.tag, "EditorOnly",
                        StringComparison.Ordinal))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(
                        gameObject) as GameObject;
                    var tag = EditorPrefs.HasKey(change.RestoreKey)
                        ? EditorPrefs.GetString(change.RestoreKey)
                        : source != null &&
                          !string.Equals(source.tag, "EditorOnly",
                              StringComparison.Ordinal)
                            ? source.tag
                            : "Untagged";
                    gameObject.tag = tag;
                }
                gameObject.SetActive(true);
            }
            else
            {
                restoreTags.Add(new PartTagChange
                {
                    RestoreKey = change.RestoreKey,
                    OriginalTag = string.Equals(gameObject.tag, "EditorOnly",
                        StringComparison.Ordinal)
                        ? null
                        : gameObject.tag
                });
                gameObject.SetActive(false);
                gameObject.tag = "EditorOnly";
            }
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    gameObject);
            }
            if (change.Visible)
            {
                restoreTags.Add(new PartTagChange
                {
                    RestoreKey = change.RestoreKey,
                    Visible = true
                });
            }
        }

        private static GameObject ResolvePartObject(
            GameObject root,
            PrefabObjectEntry entry)
        {
            var selected = ResolveObjectPrefab(
                root, entry.PrefabSiblingIndex, entry.PrefabName);
            if (selected == null)
            {
                throw new InvalidOperationException(
                    "The selected Prefab is no longer present.");
            }
            var current = selected.transform;
            foreach (var index in entry.SiblingPath)
            {
                if (index < 0 || index >= current.childCount)
                {
                    throw new InvalidOperationException(
                        "The selected object changed before editing.");
                }
                current = current.GetChild(index);
            }
            if (!string.Equals(current.name, entry.Name,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The selected object changed before editing.");
            }
            return current.gameObject;
        }

        private static GameObject ResolveObjectPrefab(
            GameObject root,
            int prefabSiblingIndex,
            string prefabName)
        {
            if (prefabSiblingIndex == -1)
            {
                return root;
            }
            if (prefabSiblingIndex < 0 ||
                prefabSiblingIndex >= root.transform.childCount)
            {
                return null;
            }
            var child = root.transform.GetChild(prefabSiblingIndex).gameObject;
            return PrefabUtility.IsAnyPrefabInstanceRoot(child) &&
                   (prefabName == null ||
                    string.Equals(child.name, prefabName,
                        StringComparison.Ordinal))
                ? child
                : null;
        }

        private IReadOnlyList<AssetChildEntry> ReadAssetChildren()
        {
            var root = _workingObject;
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
            var target = _workingObject;
            PrefabSelector.ShowPicker(
                anchor,
                prefabs,
                null,
                prefab =>
                {
                    if (_workingObject == target)
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
            var previousSelection = _selectedPrefabSiblingIndex;
            var previousName = _selectedPrefabName;
            var previousCategory = _currentCategory;
            var previousSection = _shapePartsSection;
            var previousBodyPart = _selectedBodyPart;
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
                    _selectedPrefabSiblingIndex = null;
                    _selectedPrefabName = string.Empty;
                    _selectedBodyPart = null;
                    _currentCategory = _mode == ModificationEditorMode.All
                        ? WorkflowCategory.Overview : WorkflowCategory.ShapeParts;
                    _shapePartsSection = ShapePartsSection.Parts;
                    _hiddenPrefabSiblingIndices.Clear();
                    _prefabPreviewVisibilityInitialized = false;
                });
            }
            catch (Exception exception)
            {
                _selectedPrefabSiblingIndex = previousSelection;
                _selectedPrefabName = previousName;
                _currentCategory = previousCategory;
                _shapePartsSection = previousSection;
                _selectedBodyPart = previousBodyPart;
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
            var previousSelection = _selectedPrefabSiblingIndex;
            var previousName = _selectedPrefabName;
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
                    _selectedPrefabSiblingIndex = null;
                    _selectedPrefabName = string.Empty;
                    _currentCategory = previousCategory == WorkflowCategory.Overview ||
                        previousCategory == WorkflowCategory.Material
                            ? previousCategory : WorkflowCategory.ShapeParts;
                    _hiddenPrefabSiblingIndices.Clear();
                    _prefabPreviewVisibilityInitialized = false;
                });
            }
            catch (Exception exception)
            {
                _selectedPrefabSiblingIndex = previousSelection;
                _selectedPrefabName = previousName;
                _currentCategory = previousCategory;
                Debug.LogException(exception);
                ShowAssetError("workflow.assets.saveFailed");
            }
        }

        private void EditAssetChildren(
            Action<GameObject> edit)
        {
            EndBodyScaleDrag();
            SaveBodyScalePrefab();
            if (_bodyScaleDirty)
            {
                throw new InvalidOperationException(
                    "The derived Prefab size could not be saved.");
            }
            if (!FlushPendingPartVisibility())
            {
                throw new InvalidOperationException(
                    "Pending part visibility could not be saved.");
            }
            if (!IsEditableWorkflowPrefab())
            {
                throw new InvalidOperationException(
                    "The selected Prefab is not a derived asset.");
            }
            edit(_workingObject);
            _workingSceneDirty = true;
            _scenePreview?.ReloadPrefabPreservingView(_workingObject);
            InvalidateVariantSaveStatus();
            _bodyScaleBaseScales.Clear();
            _baseAvatarViewPosition = null;
            _avatarDescriptor = null;
            _selectedMaterial = null;
            _hiddenMaterials.Clear();
            _hiddenPreviewParts.Clear();
            _expandedPartPrefabGroups.Clear();
            _expandedObjectGroups.Clear();
            _expandedMaterialPrefabGroups.Clear();
            _feedback = string.Empty;
            _assetFeedback = string.Empty;
            BuildWindow();
        }

        private void ShowAssetError(string key)
        {
            _assetFeedback = I18N.Get(key);
            BuildWindow();
        }
    }
}
