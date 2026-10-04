using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using static Ee4v.AvatarEditing.AvatarEditingUi;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private VisualElement BuildObjectControls()
        {
            _objectRows.Clear();
            var panel = new VisualElement();
            panel.AddToClassList(
                "ee4v-modification-workflow__objects-content");
            if (!_context.CanEditPrefab())
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
            if (_context.SelectedBodyPart.HasValue)
            {
                displayed = FilterPrefabObjectsByBodyPart(
                    entries, _context.SelectedBodyPart.Value);
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
            foreach (var prefabSiblingIndex in _context.GetDisplayedPrefabScopes())
            {
                var group = displayed.Where(entry =>
                    entry.PrefabSiblingIndex == prefabSiblingIndex).ToArray();
                if (group.Length == 0)
                {
                    continue;
                }
                if (!_context.SelectedPrefabSiblingIndex.HasValue &&
                    prefabSiblingIndex >= 0)
                {
                    var content = new VisualElement();
                    AddObjectHierarchy(content, group);
                    list.Add(_context.BuildPrefabGroup(
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
                container.Add(BuildObjectNode(root, 0));
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

        private VisualElement BuildObjectNode(PrefabObjectNode node, int depth)
        {
            var row = BuildObjectRow(node.Entry);
            row.style.paddingLeft = UiSpacingTokens.Small +
                depth * UiSpacingTokens.Medium;
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
                content.Add(BuildObjectNode(child, depth + 1));
            }

            var key = PrefabScenePreview.GetPartKey(
                node.Entry.PrefabSiblingIndex,
                node.Entry.SiblingPath);
            UiButton toggle = null;
            void UpdateFoldout()
            {
                var expanded = _expandedObjectGroups.Contains(key);
                toggle.SetIcon(FluentUiIcons.CreateState(
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

        private PrefabObjectEntry CreatePrefabRootEntry(
            int prefabSiblingIndex)
        {
            var child = _context.Root.transform
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

        public bool IsPrefabGroupCheckboxChecked(int prefabSiblingIndex)
        {
            if (_context.Root == null || prefabSiblingIndex < 0 ||
                prefabSiblingIndex >= _context.Root.transform.childCount)
            {
                return false;
            }
            var child = _context.Root.transform
                .GetChild(prefabSiblingIndex).gameObject;
            return !string.Equals(child.tag, "EditorOnly",
                StringComparison.Ordinal);
        }

        public void ChangePrefabGroupVisibility(
            int prefabSiblingIndex,
            bool visible)
        {
            var entry = _objectEntriesCache?.FirstOrDefault(candidate =>
                candidate.PrefabSiblingIndex == prefabSiblingIndex &&
                candidate.SiblingPath.Length == 0);
            if (entry == null)
            {
                _context.ShowAssetError("workflow.assets.saveFailed");
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
                _context.HiddenPrefabSiblingIndices.Remove(prefabSiblingIndex);
            }
            else
            {
                _context.HiddenPrefabSiblingIndices.Add(prefabSiblingIndex);
            }
            _context.Preview?.SetHiddenPrefabs(
                _context.BasePrefabHidden, _context.HiddenPrefabSiblingIndices);
            _context.RefreshPrefabPreviewVisibilityControls(prefabSiblingIndex);
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
                key == _context.SelectedPartKey);
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
                previewVisibility = AvatarEditingUi.CreatePrefabVisibilityButton(
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

        private void SelectPartRow(string key)
        {
            _context.SelectedPartKey = key;
            foreach (var pair in _objectRows)
            {
                pair.Value.Row.EnableInClassList(
                    "ee4v-modification-workflow__object-row--selected",
                    PrefabScenePreview.GetPartKey(
                        pair.Key.PrefabSiblingIndex,
                        pair.Key.SiblingPath) == key);
            }
            _context.SyncPreviewSelection();
        }

        private void ScrollToSelectedPart()
        {
            var row = _objectRows.FirstOrDefault(pair =>
                PrefabScenePreview.GetPartKey(
                    pair.Key.PrefabSiblingIndex,
                    pair.Key.SiblingPath) == _context.SelectedPartKey).Value?.Row;
            if (row != null)
            {
                _context.ControlsHost.schedule.Execute(() =>
                {
                    if (row.panel != null)
                    {
                        _context.ControlsHost.ScrollTo(row);
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
            PrefabObjectRowState state)
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
            if (state.PreviewVisibility == null)
            {
                return;
            }
            var previewVisible = entry.IsVisibleInHierarchy &&
                !_context.HiddenPreviewParts.Contains(
                    PrefabScenePreview.GetPartKey(
                        entry.PrefabSiblingIndex, entry.SiblingPath));
            var previewTooltip = entry.IsVisibleInHierarchy
                ? I18N.Get(previewVisible
                    ? "workflow.assets.clickToHide"
                    : "workflow.assets.clickToShow")
                : string.Empty;
            state.PreviewVisibility.tooltip = previewTooltip;
            state.PreviewVisibility.SetEnabled(entry.IsVisibleInHierarchy);
            state.PreviewVisibility.SetIcon(FluentUiIcons.CreateState(
                previewVisible ? "eye.png" : "eye_off.png",
                UiSizeTokens.Size18,
                previewTooltip));
        }

        private void TogglePartPreviewVisibility(PrefabObjectEntry entry)
        {
            if (!entry.IsVisibleInHierarchy)
            {
                return;
            }
            var key = PrefabScenePreview.GetPartKey(
                entry.PrefabSiblingIndex, entry.SiblingPath);
            if (!_context.HiddenPreviewParts.Add(key))
            {
                _context.HiddenPreviewParts.Remove(key);
            }
            _context.Preview?.SetHiddenParts(_context.HiddenPreviewParts);
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
            var root = _context.Root;
            {
                var result = new List<PrefabObjectEntry>();
                var classifier = new PrefabPartClassifier(root);
                var meshPresence = new Dictionary<Transform, bool>();
                var excludedPrefixes =
                    _context.GetExcludedPartPrefixes();
                var scopes = _context.SelectedPrefabSiblingIndex.HasValue
                    ? new[] { _context.SelectedPrefabSiblingIndex.Value }
                    : new[] { -1 }.Concat(_context.PrefabSiblingIndices).ToArray();
                foreach (var prefabSiblingIndex in scopes)
                {
                    var selected = ResolveObjectPrefab(
                        root,
                        prefabSiblingIndex,
                        _context.SelectedPrefabSiblingIndex.HasValue
                            ? _context.SelectedPrefabName
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

                    if (!_context.SelectedPrefabSiblingIndex.HasValue &&
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
                        _context.SelectedPrefabSiblingIndex.HasValue
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

        public static bool HasMeshInSubtree(
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

        public static bool HasPartMesh(Transform target)
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
                if (!_context.CanEditPrefab() ||
                    !FlushPendingPartVisibility())
                {
                    throw new InvalidOperationException(
                        "The selected Prefab cannot be edited.");
                }
                var gameObject = ResolvePartObject(_context.Root, entry);
                Undo.RecordObject(gameObject, "Toggle Prefab object");
                gameObject.SetActive(!gameObject.activeSelf);
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(
                        gameObject);
                }
                _context.WorkingSceneDirty = true;
                if (entry.PrefabSiblingIndex >= 0 &&
                    entry.SiblingPath.Length == 0)
                {
                    if (_context.Root.transform
                            .GetChild(entry.PrefabSiblingIndex)
                            .gameObject.activeSelf &&
                        IsPrefabGroupCheckboxChecked(entry.PrefabSiblingIndex))
                    {
                        _context.HiddenPrefabSiblingIndices.Remove(
                            entry.PrefabSiblingIndex);
                    }
                    else
                    {
                        _context.HiddenPrefabSiblingIndices.Add(
                            entry.PrefabSiblingIndex);
                    }
                    _context.Preview?.SetHiddenPrefabs(
                        _context.BasePrefabHidden, _context.HiddenPrefabSiblingIndices);
                    _context.RefreshPrefabPreviewVisibilityControls(
                        entry.PrefabSiblingIndex);
                    _context.RefreshPrefabHeaderActiveSelf(
                        entry.PrefabSiblingIndex,
                        _context.Root.transform
                            .GetChild(entry.PrefabSiblingIndex)
                            .gameObject.activeSelf);
                }
                _context.Preview?.ReloadPrefabPreservingView(_context.Root);
                _context.AssetFeedback = string.Empty;
                _context.ClearCaches();
                _context.Changed();
                _context.Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _context.ShowAssetError("workflow.assets.saveFailed");
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
                if (!_context.CanEditPrefab())
                {
                    throw new InvalidOperationException(
                        "The selected Prefab is not a derived asset.");
                }
                var assetPath = _context.GetAssetPath();
                if (_pendingPartVisibility.Count > 0 &&
                    !string.Equals(_pendingPartAssetPath, assetPath,
                        StringComparison.Ordinal) &&
                    !FlushPendingPartVisibility())
                {
                    _context.Rebuild();
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
                    _context.RefreshPrefabHeaderActiveSelf(
                        entry.PrefabSiblingIndex, activeSelf);
                    _context.RefreshPrefabGroupVisibility(
                        entry.PrefabSiblingIndex, activeSelf, visible);
                }
                _context.InvalidateMaterialData();
                _context.InvalidateControls(AvatarEditorPanel.Material);
                _context.Preview?.SetPartVisibility(
                    _context.Root,
                    entry.PrefabSiblingIndex,
                    entry.SiblingPath,
                    entry.Name,
                    activeSelf,
                    visible);
                var restoreTags = new List<PartTagChange>();
                ApplyPartVisibility(_context.Root, pending, restoreTags);
                if (entry.PrefabSiblingIndex >= 0 &&
                    entry.SiblingPath.Length == 0)
                {
                    _context.RefreshPrefabPreviewVisibilityControls(
                        entry.PrefabSiblingIndex);
                }
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
                _context.WorkingSceneDirty = true;
                _context.Changed();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _context.ShowAssetError("workflow.assets.saveFailed");
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
                UpdateObjectRow(entry, changedRow);
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
                    UpdateObjectRow(candidate, row);
                }
            }
        }

        private bool IsCurrentPartParentActive(PrefabObjectEntry entry)
        {
            var selected = ResolveObjectPrefab(
                _context.Root, entry.PrefabSiblingIndex, entry.PrefabName);
            if (selected == null)
            {
                return false;
            }
            var active = _context.Root.activeSelf;
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

        public bool FlushPendingPartVisibility()
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
                    ApplyPartVisibility(_context.Root, change,
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
                _context.WorkingSceneDirty = true;
                _context.AssetFeedback = string.Empty;
                _context.Changed();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _context.AssetFeedback = I18N.Get("workflow.assets.saveFailed");
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
    }
}
