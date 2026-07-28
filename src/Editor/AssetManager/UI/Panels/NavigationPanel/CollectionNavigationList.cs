using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class CollectionNavigationList : VisualElement
    {
        private const float DragStartDistance = 5f;
        private const float SiblingDropZoneRatio = 0.25f;
        private const string RootClassName =
            "ee4v-asset-manager-collection-list";
        private const string RowClassName =
            "ee4v-asset-manager-collection-list__row";
        private const string RowDraggingClassName =
            "ee4v-asset-manager-collection-list__row--dragging";
        private const string RowDropBeforeClassName =
            "ee4v-asset-manager-collection-list__row--drop-before";
        private const string RowDropAfterClassName =
            "ee4v-asset-manager-collection-list__row--drop-after";
        private const string DisclosureClassName =
            "ee4v-asset-manager-collection-list__disclosure";
        private const string ButtonClassName =
            "ee4v-asset-manager-collection-list__button";
        private const string ButtonDropTargetClassName =
            "ee4v-asset-manager-collection-list__button--drop-target";
        private const string DepthLineClassName =
            "ee4v-asset-manager-collection-list__depth-line";
        private const string DepthLineCurrentClassName =
            "ee4v-asset-manager-collection-list__depth-line--current";
        private const string DepthLineLastClassName =
            "ee4v-asset-manager-collection-list__depth-line--last";
        private const string DepthLineChildrenClassName =
            "ee4v-asset-manager-collection-list__depth-line--children";
        private const string DepthBranchClassName =
            "ee4v-asset-manager-collection-list__depth-branch";
        private readonly Action<string> _selected;
        private readonly Action<IReadOnlyList<string>, string, int>
            _moveRequested;
        private readonly List<CollectionButton> _buttons =
            new List<CollectionButton>();
        private readonly Dictionary<string, AssetCollection> _collections =
            new Dictionary<string, AssetCollection>(
                StringComparer.Ordinal);
        private readonly Dictionary<string, AssetCollection[]> _children =
            new Dictionary<string, AssetCollection[]>(
                StringComparer.Ordinal);
        private readonly HashSet<string> _collapsedCollectionIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _selectedCollectionIds =
            new HashSet<string>(StringComparer.Ordinal);
        private string _selectedViewId = string.Empty;
        private string _pendingDragId = string.Empty;
        private IReadOnlyList<string> _pendingDragIds =
            Array.Empty<string>();
        private string _selectionAnchorId = string.Empty;
        private Vector2 _dragStartPosition;
        private int _dragPointerId = -1;
        private bool _isDragging;
        private bool _selectOnlyOnPointerUp;
        private bool _selectionExplicitlyCleared;

        public CollectionNavigationList(
            Action<string> selected,
            Action<IReadOnlyList<string>, string, int>
                moveRequested = null)
        {
            _selected = selected;
            _moveRequested = moveRequested;
            AddToClassList(RootClassName);
            focusable = true;
            pickingMode = PickingMode.Position;
            RegisterCallback<PointerDownEvent>(
                OnBackgroundPointerDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void SetState(
            IReadOnlyList<AssetCollection> collections,
            string selectedViewId)
        {
            _selectedViewId = selectedViewId ?? string.Empty;
            EndPointerInteraction(releasePointer: true);
            _collections.Clear();
            _children.Clear();

            var items = (collections ?? Array.Empty<AssetCollection>())
                .Where(item =>
                    item != null &&
                    !string.IsNullOrWhiteSpace(item.Id))
                .ToArray();
            for (var i = 0; i < items.Length; i++)
            {
                _collections[items[i].Id] = items[i];
            }

            var ids = new HashSet<string>(
                items.Select(item => item.Id),
                StringComparer.Ordinal);
            var children = items
                .GroupBy(item =>
                    !string.IsNullOrWhiteSpace(item.ParentCollectionId) &&
                    ids.Contains(item.ParentCollectionId)
                        ? item.ParentCollectionId
                        : string.Empty)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(
                            item => item.SortOrder)
                        .ThenBy(
                            item => item.Name,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item => item.Id,
                            StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);

            foreach (var pair in children)
            {
                _children[pair.Key] = pair.Value;
            }

            _collapsedCollectionIds.RemoveWhere(
                collectionId => !_collections.ContainsKey(
                    collectionId));
            _selectedCollectionIds.RemoveWhere(
                collectionId => !_collections.ContainsKey(
                    collectionId));
            if (!string.IsNullOrWhiteSpace(_selectionAnchorId) &&
                !_collections.ContainsKey(_selectionAnchorId))
            {
                _selectionAnchorId = string.Empty;
            }

            string selectedCollectionId;
            if (AssetManagerCollectionViewId.TryDecode(
                    _selectedViewId,
                    out selectedCollectionId) &&
                _collections.ContainsKey(selectedCollectionId) &&
                !_selectedCollectionIds.Contains(selectedCollectionId) &&
                !_selectionExplicitlyCleared)
            {
                _selectedCollectionIds.Clear();
                _selectedCollectionIds.Add(selectedCollectionId);
                _selectionAnchorId = selectedCollectionId;
            }
            else if (!AssetManagerCollectionViewId.TryDecode(
                         _selectedViewId,
                         out selectedCollectionId))
            {
                _selectedCollectionIds.Clear();
                _selectionAnchorId = string.Empty;
                _selectionExplicitlyCleared = false;
            }
            RebuildRows();
        }

        public void SetSelectedItem(string viewId)
        {
            _selectedViewId = viewId ?? string.Empty;
            _selectionExplicitlyCleared = false;
            string collectionId;
            if (AssetManagerCollectionViewId.TryDecode(
                    _selectedViewId,
                    out collectionId) &&
                _collections.ContainsKey(collectionId))
            {
                if (!_selectedCollectionIds.Contains(collectionId))
                {
                    _selectedCollectionIds.Clear();
                    _selectedCollectionIds.Add(collectionId);
                    _selectionAnchorId = collectionId;
                }
            }
            else
            {
                _selectedCollectionIds.Clear();
                _selectionAnchorId = string.Empty;
            }

            RefreshSelection();
        }

        internal IReadOnlyCollection<string> SelectedCollectionIds
        {
            get { return _selectedCollectionIds; }
        }

        internal void SelectCollection(
            string collectionId,
            bool toggle,
            bool range)
        {
            if (string.IsNullOrWhiteSpace(collectionId) ||
                !_collections.ContainsKey(collectionId))
            {
                return;
            }

            if (range &&
                !string.IsNullOrWhiteSpace(_selectionAnchorId))
            {
                var anchorIndex = _buttons.FindIndex(item =>
                    string.Equals(
                        item.CollectionId,
                        _selectionAnchorId,
                        StringComparison.Ordinal));
                var targetIndex = _buttons.FindIndex(item =>
                    string.Equals(
                        item.CollectionId,
                        collectionId,
                        StringComparison.Ordinal));
                if (anchorIndex >= 0 && targetIndex >= 0)
                {
                    _selectedCollectionIds.Clear();

                    var first = Math.Min(anchorIndex, targetIndex);
                    var last = Math.Max(anchorIndex, targetIndex);
                    for (var i = first; i <= last; i++)
                    {
                        _selectedCollectionIds.Add(
                            _buttons[i].CollectionId);
                    }
                }
                else
                {
                    SelectOnly(collectionId);
                }
            }
            else if (toggle)
            {
                if (!_selectedCollectionIds.Add(collectionId))
                {
                    _selectedCollectionIds.Remove(collectionId);
                }

                _selectionAnchorId = collectionId;
            }
            else
            {
                SelectOnly(collectionId);
            }

            _selectionExplicitlyCleared =
                _selectedCollectionIds.Count == 0;
            var activeCollectionId =
                _selectedCollectionIds.Contains(collectionId)
                    ? collectionId
                    : GetOrderedSelectedCollectionIds()
                        .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(activeCollectionId))
            {
                _selectedViewId =
                    AssetManagerCollectionViewId.Encode(
                        activeCollectionId);
            }

            RefreshSelection();
            if (!string.IsNullOrWhiteSpace(
                    activeCollectionId))
            {
                _selected?.Invoke(_selectedViewId);
            }
        }

        private void SelectOnly(string collectionId)
        {
            _selectedCollectionIds.Clear();
            _selectedCollectionIds.Add(collectionId);
            _selectionAnchorId = collectionId;
            _selectionExplicitlyCleared = false;
        }

        internal void ClearSelection()
        {
            if (_selectedCollectionIds.Count == 0)
            {
                return;
            }

            EndPointerInteraction(releasePointer: true);
            _selectedCollectionIds.Clear();
            _selectionAnchorId = string.Empty;
            _selectionExplicitlyCleared = true;
            RefreshSelection();
        }

        internal bool CanMoveCollection(
            string collectionId,
            string parentCollectionId)
        {
            AssetCollection collection;
            if (!_collections.TryGetValue(
                    collectionId ?? string.Empty,
                    out collection))
            {
                return false;
            }

            return !string.Equals(
                       NormalizeParentId(
                           collection.ParentCollectionId),
                       NormalizeParentId(parentCollectionId),
                       StringComparison.Ordinal) &&
                   CanPlaceCollection(
                       collectionId,
                       parentCollectionId,
                       -1);
        }

        internal bool CanPlaceCollection(
            string collectionId,
            string parentCollectionId,
            int siblingIndex)
        {
            return CanPlaceCollections(
                new[] { collectionId },
                parentCollectionId,
                siblingIndex);
        }

        internal bool CanPlaceCollections(
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex)
        {
            var movingIds = GetTopLevelCollectionIds(
                collectionIds);
            if (movingIds.Count == 0)
            {
                return false;
            }

            var targetParentId =
                NormalizeParentId(parentCollectionId);
            if (targetParentId.Length > 0 &&
                !_collections.ContainsKey(targetParentId))
            {
                return false;
            }

            for (var i = 0; i < movingIds.Count; i++)
            {
                var collectionId = movingIds[i];
                var visited = new HashSet<string>(
                    StringComparer.Ordinal);
                var ancestorId = targetParentId;
                while (!string.IsNullOrWhiteSpace(ancestorId) &&
                       visited.Add(ancestorId))
                {
                    if (string.Equals(
                            collectionId,
                            ancestorId,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    AssetCollection ancestor;
                    if (!_collections.TryGetValue(
                            ancestorId,
                            out ancestor))
                    {
                        break;
                    }

                    ancestorId = ancestor.ParentCollectionId;
                }
            }

            var movingIdSet = new HashSet<string>(
                movingIds,
                StringComparer.Ordinal);
            var siblings = GetSiblings(targetParentId)
                .Where(item =>
                    !movingIdSet.Contains(item.Id))
                .ToArray();
            var targetIndex = siblingIndex < 0
                ? siblings.Length
                : Mathf.Clamp(
                    siblingIndex,
                    0,
                    siblings.Length);
            for (var i = 0; i < movingIds.Count; i++)
            {
                var currentParentId = NormalizeParentId(
                    _collections[movingIds[i]]
                        .ParentCollectionId);
                if (!string.Equals(
                        currentParentId,
                        targetParentId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            var currentOrder = GetSiblings(targetParentId)
                .Select(item => item.Id)
                .ToArray();
            var nextOrder = siblings
                .Select(item => item.Id)
                .ToList();
            nextOrder.InsertRange(targetIndex, movingIds);
            return !currentOrder.SequenceEqual(
                nextOrder,
                StringComparer.Ordinal);
        }

        internal bool TryRequestMove(
            string collectionId,
            string parentCollectionId,
            int siblingIndex = -1)
        {
            return TryRequestMoves(
                new[] { collectionId },
                parentCollectionId,
                siblingIndex);
        }

        internal bool TryRequestMoves(
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex = -1)
        {
            var movingIds = GetTopLevelCollectionIds(
                collectionIds);
            if (_moveRequested == null ||
                !CanPlaceCollections(
                    movingIds,
                    parentCollectionId,
                    siblingIndex))
            {
                return false;
            }

            _moveRequested?.Invoke(
                movingIds,
                string.IsNullOrWhiteSpace(parentCollectionId)
                    ? null
                    : parentCollectionId,
                siblingIndex);
            return true;
        }

        internal void SetCollectionExpanded(
            string collectionId,
            bool expanded)
        {
            if (string.IsNullOrWhiteSpace(collectionId) ||
                !_children.ContainsKey(collectionId))
            {
                return;
            }

            if (expanded)
            {
                _collapsedCollectionIds.Remove(collectionId);
            }
            else
            {
                _collapsedCollectionIds.Add(collectionId);
            }

            RebuildRows();
        }

        private void RebuildRows()
        {
            EndPointerInteraction(releasePointer: true);
            _buttons.Clear();
            Clear();
            AddChildren(string.Empty, 0);
            RefreshSelection();
        }

        private void AddChildren(
            string parentId,
            int depth)
        {
            AssetCollection[] items;
            if (!_children.TryGetValue(
                    parentId ?? string.Empty,
                    out items))
            {
                return;
            }

            for (var i = 0; i < items.Length; i++)
            {
                var collection = items[i];
                var hasChildren =
                    _children.ContainsKey(collection.Id);
                var expanded =
                    !_collapsedCollectionIds.Contains(
                        collection.Id);
                var viewId =
                    AssetManagerCollectionViewId.Encode(collection.Id);
                var button = new UiButton(
                    new UiButtonState(
                        collection.Name,
                        iconState:
                        AssetCollectionIconPresenter.CreateState(
                            collection,
                            UiSizeTokens.Size14),
                        selected:
                            _selectedCollectionIds.Contains(
                                collection.Id),
                        variant: UiButtonVariant.Ghost,
                        size: UiButtonSize.Compact),
                    UiClassNames.CollectionNavigationLabel);
                button.AddToClassList(ButtonClassName);
                button.pickingMode = PickingMode.Ignore;
                button.style.paddingLeft =
                    UiSpacingTokens.Xxs;

                var row = new VisualElement
                {
                    focusable = true,
                    pickingMode = PickingMode.Position
                };
                row.AddToClassList(RowClassName);
                row.Add(CreateDisclosure(
                    collection.Id,
                    depth,
                    hasChildren,
                    expanded));
                row.Add(button);
                AddDepthLines(
                    row,
                    depth,
                    hasChildren && expanded,
                    isLastSibling: i == items.Length - 1);
                var collectionButton = new CollectionButton(
                    collection.Id,
                    viewId,
                    row,
                    button);
                RegisterPointerCallbacks(collectionButton);
                Add(row);
                _buttons.Add(collectionButton);
                if (hasChildren && expanded)
                {
                    AddChildren(
                        collection.Id,
                        depth + 1);
                }
            }
        }

        private VisualElement CreateDisclosure(
            string collectionId,
            int depth,
            bool hasChildren,
            bool expanded)
        {
            VisualElement disclosure;
            if (hasChildren)
            {
                var button = new UiButton(
                    new UiButtonState(
                        iconState: IconState.FromBuiltinIcon(
                            expanded
                                ? UiBuiltinIcon.DisclosureOpen
                                : UiBuiltinIcon.DisclosureClosed,
                            UiSizeTokens.Size10),
                        variant: UiButtonVariant.Ghost,
                        size: UiButtonSize.Compact));
                button.clicked += () =>
                    SetCollectionExpanded(
                        collectionId,
                        !expanded);
                button.RegisterCallback<PointerDownEvent>(
                    evt => evt.StopPropagation());
                button.RegisterCallback<PointerUpEvent>(
                    evt => evt.StopPropagation());
                disclosure = button;
            }
            else
            {
                disclosure = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
            }

            disclosure.AddToClassList(DisclosureClassName);
            disclosure.style.marginLeft =
                depth * UiSpacingTokens.Xl;
            return disclosure;
        }

        private static void AddDepthLines(
            VisualElement row,
            int depth,
            bool hasChildren,
            bool isLastSibling)
        {
            if (depth > 0)
            {
                var currentLine = CreateDepthElement(
                    DepthLineClassName,
                    DepthLineCurrentClassName);
                currentLine.EnableInClassList(
                    DepthLineLastClassName,
                    isLastSibling);
                currentLine.style.left =
                    GetDepthLineLeft(depth);
                row.Add(currentLine);

                var branch = CreateDepthElement(
                    DepthBranchClassName);
                branch.style.left =
                    GetDepthLineLeft(depth);
                branch.style.width =
                    UiSpacingTokens.Xl * 0.5f;
                row.Add(branch);
            }

            if (!hasChildren)
            {
                return;
            }

            var childrenLine = CreateDepthElement(
                DepthLineClassName,
                DepthLineChildrenClassName);
            childrenLine.style.left =
                GetDepthLineLeft(depth + 1);
            row.Add(childrenLine);
        }

        private static VisualElement CreateDepthElement(
            params string[] classNames)
        {
            var element = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            for (var i = 0; i < classNames.Length; i++)
            {
                element.AddToClassList(classNames[i]);
            }

            return element;
        }

        private static float GetDepthLineLeft(int depth)
        {
            return UiSizeTokens.Size16 +
                   UiSpacingTokens.Xxs +
                   UiSizeTokens.Size14 * 0.5f +
                   (depth - 1) * UiSpacingTokens.Xl;
        }

        private void RegisterPointerCallbacks(
            CollectionButton item)
        {
            item.Row.RegisterCallback<PointerDownEvent>(
                evt => OnRowPointerDown(evt, item));
            item.Row.RegisterCallback<PointerMoveEvent>(
                evt => OnRowPointerMove(evt, item));
            item.Row.RegisterCallback<PointerUpEvent>(
                evt => OnRowPointerUp(evt, item));
            item.Row.RegisterCallback<PointerCancelEvent>(_ =>
                EndPointerInteraction(releasePointer: false));
            item.Row.RegisterCallback<PointerCaptureOutEvent>(_ =>
                EndPointerInteraction(releasePointer: false));
            item.Row.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.LeftArrow &&
                    _children.ContainsKey(item.CollectionId))
                {
                    SetCollectionExpanded(
                        item.CollectionId,
                        false);
                    evt.StopPropagation();
                    return;
                }

                if (evt.keyCode == KeyCode.RightArrow &&
                    _children.ContainsKey(item.CollectionId))
                {
                    SetCollectionExpanded(
                        item.CollectionId,
                        true);
                    evt.StopPropagation();
                    return;
                }

                if (evt.keyCode != KeyCode.Return &&
                    evt.keyCode != KeyCode.KeypadEnter &&
                    evt.keyCode != KeyCode.Space)
                {
                    return;
                }

                SelectCollection(
                    item.CollectionId,
                    evt.ctrlKey || evt.commandKey,
                    evt.shiftKey);
                evt.StopPropagation();
            });
        }

        private void OnBackgroundPointerDown(
            PointerDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse ||
                !ReferenceEquals(evt.target, this))
            {
                return;
            }

            ClearSelection();
            Focus();
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            ClearSelection();
            evt.StopPropagation();
        }

        private void OnRowPointerDown(
            PointerDownEvent evt,
            CollectionButton item)
        {
            if (evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            EndPointerInteraction(releasePointer: true);
            _selectOnlyOnPointerUp =
                !evt.ctrlKey &&
                !evt.commandKey &&
                !evt.shiftKey &&
                _selectedCollectionIds.Count > 1 &&
                _selectedCollectionIds.Contains(
                    item.CollectionId);
            if (!_selectOnlyOnPointerUp)
            {
                SelectCollection(
                    item.CollectionId,
                    evt.ctrlKey || evt.commandKey,
                    evt.shiftKey);
            }

            _pendingDragId = item.CollectionId;
            _pendingDragIds =
                _selectedCollectionIds.Contains(
                    item.CollectionId)
                    ? GetTopLevelCollectionIds(
                        GetOrderedSelectedCollectionIds())
                    : Array.Empty<string>();
            _dragStartPosition = ToVector2(evt.position);
            _dragPointerId = evt.pointerId;
            item.Row.Focus();
            item.Row.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnRowPointerMove(
            PointerMoveEvent evt,
            CollectionButton item)
        {
            if (!string.Equals(
                    _pendingDragId,
                    item.CollectionId,
                    StringComparison.Ordinal) ||
                evt.pointerId != _dragPointerId)
            {
                return;
            }

            var position = ToVector2(evt.position);
            if (!_isDragging &&
                _pendingDragIds.Count > 0 &&
                (position - _dragStartPosition).sqrMagnitude >=
                DragStartDistance * DragStartDistance)
            {
                _isDragging = true;
                for (var i = 0; i < _buttons.Count; i++)
                {
                    _buttons[i].Row.EnableInClassList(
                        RowDraggingClassName,
                        _pendingDragIds.Contains(
                            _buttons[i].CollectionId,
                            StringComparer.Ordinal));
                }
            }

            if (_isDragging)
            {
                UpdateDropHighlight(position);
            }

            evt.StopPropagation();
        }

        private void OnRowPointerUp(
            PointerUpEvent evt,
            CollectionButton item)
        {
            if (evt.pointerId != _dragPointerId ||
                !string.Equals(
                    item.CollectionId,
                    _pendingDragId,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (_isDragging)
            {
                RequestMoveAtPosition(
                    ToVector2(evt.position));
            }
            else if (_selectOnlyOnPointerUp)
            {
                SelectCollection(
                    item.CollectionId,
                    toggle: false,
                    range: false);
            }

            EndPointerInteraction(releasePointer: true);
            evt.StopPropagation();
        }

        private void UpdateDropHighlight(
            Vector2 position)
        {
            ResetDropHighlights();
            var target = FindDropTarget(position);
            if (target == null ||
                !CanPlaceCollections(
                    _pendingDragIds,
                    target.ParentCollectionId,
                    target.SiblingIndex))
            {
                return;
            }

            if (target.Placement == DropPlacement.Into)
            {
                target.Item.Button.EnableInClassList(
                    ButtonDropTargetClassName,
                    true);
            }
            else if (target.Placement ==
                     DropPlacement.Before)
            {
                target.Item.Row.EnableInClassList(
                    RowDropBeforeClassName,
                    true);
            }
            else if (target.Placement ==
                     DropPlacement.After)
            {
                target.Item.Row.EnableInClassList(
                    RowDropAfterClassName,
                    true);
            }
        }

        private void RequestMoveAtPosition(
            Vector2 position)
        {
            var target = FindDropTarget(position);
            if (target == null)
            {
                return;
            }

            var requested = TryRequestMoves(
                _pendingDragIds,
                target.ParentCollectionId,
                target.SiblingIndex);
            if (requested &&
                target.Placement == DropPlacement.Into)
            {
                _collapsedCollectionIds.Remove(
                    target.ParentCollectionId);
            }
        }

        private void ResetDropHighlights()
        {
            for (var i = 0; i < _buttons.Count; i++)
            {
                var item = _buttons[i];
                item.Button.EnableInClassList(
                    ButtonDropTargetClassName,
                    false);
                item.Row.EnableInClassList(
                    RowDropBeforeClassName,
                    false);
                item.Row.EnableInClassList(
                    RowDropAfterClassName,
                    false);
            }
        }

        private CollectionDropTarget FindDropTarget(
            Vector2 position)
        {
            var item = FindRowAtPosition(position);
            if (item == null)
            {
                var movingIds = new HashSet<string>(
                    _pendingDragIds,
                    StringComparer.Ordinal);
                return worldBound.Contains(position)
                    ? new CollectionDropTarget(
                        null,
                        DropPlacement.Root,
                        null,
                        GetSiblings(string.Empty)
                            .Count(sibling =>
                                !movingIds.Contains(sibling.Id)))
                    : null;
            }

            if (_pendingDragIds.Contains(
                    item.CollectionId,
                    StringComparer.Ordinal))
            {
                return null;
            }

            var normalizedPosition =
                (position.y - item.Row.worldBound.yMin) /
                Mathf.Max(1f, item.Row.worldBound.height);
            if (normalizedPosition < SiblingDropZoneRatio)
            {
                return CreateSiblingDropTarget(
                    item,
                    DropPlacement.Before);
            }

            if (normalizedPosition >
                1f - SiblingDropZoneRatio)
            {
                return CreateSiblingDropTarget(
                    item,
                    DropPlacement.After);
            }

            return new CollectionDropTarget(
                item,
                DropPlacement.Into,
                item.CollectionId,
                GetSiblings(item.CollectionId)
                    .Count(sibling =>
                        !_pendingDragIds.Contains(
                            sibling.Id,
                            StringComparer.Ordinal)));
        }

        private CollectionDropTarget CreateSiblingDropTarget(
            CollectionButton item,
            DropPlacement placement)
        {
            AssetCollection targetCollection;
            if (!_collections.TryGetValue(
                    item.CollectionId,
                    out targetCollection))
            {
                return null;
            }

            var parentId = NormalizeParentId(
                targetCollection.ParentCollectionId);
            var movingIds = new HashSet<string>(
                _pendingDragIds,
                StringComparer.Ordinal);
            var siblings = GetSiblings(parentId)
                .Where(sibling =>
                    !movingIds.Contains(sibling.Id))
                .ToArray();
            var targetIndex = Array.FindIndex(
                siblings,
                sibling => string.Equals(
                    sibling.Id,
                    item.CollectionId,
                    StringComparison.Ordinal));
            if (targetIndex < 0)
            {
                return null;
            }

            return new CollectionDropTarget(
                item,
                placement,
                parentId,
                placement == DropPlacement.After
                    ? targetIndex + 1
                    : targetIndex);
        }

        private CollectionButton FindRowAtPosition(
            Vector2 position)
        {
            for (var i = _buttons.Count - 1; i >= 0; i--)
            {
                if (_buttons[i].Row.worldBound.Contains(position))
                {
                    return _buttons[i];
                }
            }

            return null;
        }

        private AssetCollection[] GetSiblings(
            string parentCollectionId)
        {
            AssetCollection[] siblings;
            return _children.TryGetValue(
                    NormalizeParentId(parentCollectionId),
                    out siblings)
                ? siblings
                : Array.Empty<AssetCollection>();
        }

        private IReadOnlyList<string>
            GetOrderedSelectedCollectionIds()
        {
            var result = new List<string>();
            AddOrderedSelectedChildren(
                string.Empty,
                result);
            return result;
        }

        private void AddOrderedSelectedChildren(
            string parentCollectionId,
            ICollection<string> result)
        {
            var children = GetSiblings(parentCollectionId);
            for (var i = 0; i < children.Length; i++)
            {
                if (_selectedCollectionIds.Contains(
                        children[i].Id))
                {
                    result.Add(children[i].Id);
                }

                AddOrderedSelectedChildren(
                    children[i].Id,
                    result);
            }
        }

        private IReadOnlyList<string> GetTopLevelCollectionIds(
            IReadOnlyList<string> collectionIds)
        {
            if (collectionIds == null)
            {
                return Array.Empty<string>();
            }

            var requestedIds = new HashSet<string>(
                collectionIds.Where(id =>
                    !string.IsNullOrWhiteSpace(id) &&
                    _collections.ContainsKey(id)),
                StringComparer.Ordinal);
            var result = new List<string>();
            var added = new HashSet<string>(
                StringComparer.Ordinal);
            for (var i = 0; i < collectionIds.Count; i++)
            {
                var collectionId = collectionIds[i];
                if (!requestedIds.Contains(collectionId) ||
                    !added.Add(collectionId) ||
                    HasSelectedAncestor(
                        collectionId,
                        requestedIds))
                {
                    continue;
                }

                result.Add(collectionId);
            }

            return result;
        }

        private bool HasSelectedAncestor(
            string collectionId,
            ISet<string> selectedIds)
        {
            AssetCollection collection;
            if (!_collections.TryGetValue(
                    collectionId,
                    out collection))
            {
                return false;
            }

            var visited = new HashSet<string>(
                StringComparer.Ordinal);
            var parentId = NormalizeParentId(
                collection.ParentCollectionId);
            while (parentId.Length > 0 &&
                   visited.Add(parentId))
            {
                if (selectedIds.Contains(parentId))
                {
                    return true;
                }

                AssetCollection parent;
                if (!_collections.TryGetValue(
                        parentId,
                        out parent))
                {
                    break;
                }

                parentId = NormalizeParentId(
                    parent.ParentCollectionId);
            }

            return false;
        }

        private static string NormalizeParentId(
            string parentCollectionId)
        {
            return string.IsNullOrWhiteSpace(parentCollectionId)
                ? string.Empty
                : parentCollectionId;
        }

        private void EndPointerInteraction(
            bool releasePointer)
        {
            var pointerId = _dragPointerId;
            CollectionButton source = null;
            for (var i = 0; i < _buttons.Count; i++)
            {
                var item = _buttons[i];
                item.Row.EnableInClassList(
                    RowDraggingClassName,
                    false);
                if (string.Equals(
                        item.CollectionId,
                        _pendingDragId,
                        StringComparison.Ordinal))
                {
                    source = item;
                }
            }

            _pendingDragId = string.Empty;
            _pendingDragIds = Array.Empty<string>();
            _dragPointerId = -1;
            _isDragging = false;
            _selectOnlyOnPointerUp = false;
            ResetDropHighlights();

            if (releasePointer &&
                source != null &&
                source.Row.HasPointerCapture(pointerId))
            {
                source.Row.ReleasePointer(pointerId);
            }
        }

        private void RefreshSelection()
        {
            for (var i = 0; i < _buttons.Count; i++)
            {
                var item = _buttons[i];
                item.Button.SetSelected(
                    _selectedCollectionIds.Contains(
                        item.CollectionId));
            }
        }

        private static Vector2 ToVector2(Vector3 position)
        {
            return new Vector2(position.x, position.y);
        }

        private enum DropPlacement
        {
            Root,
            Before,
            Into,
            After
        }

        private sealed class CollectionDropTarget
        {
            public CollectionDropTarget(
                CollectionButton item,
                DropPlacement placement,
                string parentCollectionId,
                int siblingIndex)
            {
                Item = item;
                Placement = placement;
                ParentCollectionId =
                    NormalizeParentId(parentCollectionId);
                SiblingIndex = siblingIndex;
            }

            public CollectionButton Item { get; }

            public DropPlacement Placement { get; }

            public string ParentCollectionId { get; }

            public int SiblingIndex { get; }
        }

        private sealed class CollectionButton
        {
            public CollectionButton(
                string collectionId,
                string viewId,
                VisualElement row,
                UiButton button)
            {
                CollectionId = collectionId;
                ViewId = viewId;
                Row = row;
                Button = button;
            }

            public string CollectionId { get; }

            public string ViewId { get; }

            public VisualElement Row { get; }

            public UiButton Button { get; }
        }
    }
}
