using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Ee4v.UI
{
    internal sealed class ReorderableListItemState
    {
        public ReorderableListItemState(string id, string label)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public string Id { get; }

        public string Label { get; }
    }

    internal sealed class ReorderableListFieldState
    {
        public ReorderableListFieldState(
            IReadOnlyList<ReorderableListItemState> items,
            string tooltip = null,
            string reorderTooltip = null)
        {
            Items = items ?? Array.Empty<ReorderableListItemState>();
            Tooltip = tooltip ?? string.Empty;
            ReorderTooltip = reorderTooltip ?? string.Empty;
        }

        public IReadOnlyList<ReorderableListItemState> Items { get; }

        public string Tooltip { get; }

        public string ReorderTooltip { get; }
    }

    internal sealed class ReorderableListField : VisualElement
    {
        private const string RootClassName =
            "ee4v-ui-reorderable-list-field";
        private const string RowClassName =
            "ee4v-ui-reorderable-list-field__row";
        private const string DraggingClassName =
            "ee4v-ui-reorderable-list-field__row--dragging";
        private const string HandleClassName =
            "ee4v-ui-reorderable-list-field__handle";
        private const string LabelClassName =
            "ee4v-ui-reorderable-list-field__label";
        private const int ReorderAnimationDurationMs = 160;

        private readonly List<ItemView> _items =
            new List<ItemView>();
        private ItemView _draggedItem;
        private int _dragPointerId = -1;
        private int _reorderRevision;

        public ReorderableListField(
            ReorderableListFieldState state = null)
        {
            AddToClassList(RootClassName);
            SetState(
                state ??
                new ReorderableListFieldState(
                    Array.Empty<ReorderableListItemState>()));
        }

        public event Action<IReadOnlyList<string>> OrderChanged;

        public IReadOnlyList<string> Order
        {
            get
            {
                return _items
                    .Select(item => item.Id)
                    .ToArray();
            }
        }

        public void SetState(ReorderableListFieldState state)
        {
            state = state ??
                new ReorderableListFieldState(
                    Array.Empty<ReorderableListItemState>());
            tooltip = state.Tooltip;
            _reorderRevision++;
            StopPositionAnimations();
            Clear();
            _items.Clear();

            for (var i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item == null)
                {
                    continue;
                }

                AddItem(item, state.ReorderTooltip);
            }
        }

        internal void MoveItem(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 ||
                fromIndex >= _items.Count ||
                toIndex < 0 ||
                toIndex >= _items.Count ||
                fromIndex == toIndex)
            {
                return;
            }

            MoveItem(_items[fromIndex], toIndex);
        }

        private void AddItem(
            ReorderableListItemState state,
            string reorderTooltip)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClassName);

            var handle = UiTextFactory.Create("\u2261");
            handle.AddToClassList(HandleClassName);
            handle.tooltip = reorderTooltip;
            handle.focusable = true;
            handle.pickingMode = PickingMode.Position;

            var label = UiTextFactory.Create(state.Label);
            label.AddToClassList(LabelClassName);
            label.tooltip = tooltip;

            var view = new ItemView(
                state.Id,
                row,
                handle);
            handle.RegisterCallback<PointerDownEvent>(
                evt => BeginDrag(evt, view));
            handle.RegisterCallback<PointerMoveEvent>(OnDragMove);
            handle.RegisterCallback<PointerUpEvent>(OnDragEnd);
            handle.RegisterCallback<PointerCaptureOutEvent>(
                _ => EndDrag());
            handle.RegisterCallback<KeyDownEvent>(
                evt => OnHandleKeyDown(evt, view));

            row.Add(handle);
            row.Add(label);
            Add(row);
            _items.Add(view);
        }

        private void BeginDrag(
            PointerDownEvent evt,
            ItemView view)
        {
            if (evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            _draggedItem = view;
            _dragPointerId = evt.pointerId;
            view.Row.AddToClassList(DraggingClassName);
            view.Handle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnDragMove(PointerMoveEvent evt)
        {
            if (_draggedItem == null ||
                evt.pointerId != _dragPointerId)
            {
                return;
            }

            var targetIndex = FindItemIndex(evt.position);
            if (targetIndex >= 0)
            {
                MoveItem(_draggedItem, targetIndex);
            }

            evt.StopPropagation();
        }

        private void OnDragEnd(PointerUpEvent evt)
        {
            if (_draggedItem == null ||
                evt.pointerId != _dragPointerId)
            {
                return;
            }

            if (_draggedItem.Handle.HasPointerCapture(
                    _dragPointerId))
            {
                _draggedItem.Handle.ReleasePointer(
                    _dragPointerId);
            }

            EndDrag();
            evt.StopPropagation();
        }

        private void EndDrag()
        {
            if (_draggedItem != null)
            {
                _draggedItem.Row.RemoveFromClassList(
                    DraggingClassName);
            }

            _draggedItem = null;
            _dragPointerId = -1;
        }

        private void OnHandleKeyDown(
            KeyDownEvent evt,
            ItemView view)
        {
            var index = _items.IndexOf(view);
            if (evt.keyCode == KeyCode.UpArrow && index > 0)
            {
                MoveItem(view, index - 1);
                evt.StopPropagation();
            }
            else if (
                evt.keyCode == KeyCode.DownArrow &&
                index + 1 < _items.Count)
            {
                MoveItem(view, index + 1);
                evt.StopPropagation();
            }
        }

        private int FindItemIndex(Vector3 worldPosition)
        {
            if (_items.Count == 0)
            {
                return -1;
            }

            var localPosition = this.WorldToLocal(
                new Vector2(
                    worldPosition.x,
                    worldPosition.y));
            return FindClosestSlotIndex(
                _items
                    .Select(item => item.Row.layout.center.y)
                    .ToArray(),
                localPosition.y);
        }

        internal static int FindClosestSlotIndex(
            IReadOnlyList<float> slotCenters,
            float pointerY)
        {
            if (slotCenters == null ||
                slotCenters.Count == 0 ||
                float.IsNaN(pointerY) ||
                float.IsInfinity(pointerY))
            {
                return -1;
            }

            var orderedCenters = slotCenters
                .Where(value =>
                    !float.IsNaN(value) &&
                    !float.IsInfinity(value))
                .OrderBy(value => value)
                .ToArray();
            if (orderedCenters.Length != slotCenters.Count)
            {
                return -1;
            }

            var closestIndex = 0;
            var closestDistance = Mathf.Abs(
                pointerY - orderedCenters[0]);
            for (var i = 1; i < orderedCenters.Length; i++)
            {
                var distance = Mathf.Abs(
                    pointerY - orderedCenters[i]);
                if (distance < closestDistance)
                {
                    closestIndex = i;
                    closestDistance = distance;
                }
            }

            return closestIndex;
        }

        private void MoveItem(ItemView view, int targetIndex)
        {
            var currentIndex = _items.IndexOf(view);
            if (currentIndex < 0 || currentIndex == targetIndex)
            {
                return;
            }

            StopPositionAnimations();
            var previousPositions = _items.ToDictionary(
                item => item,
                item => item.Row.worldBound.position);
            _items.RemoveAt(currentIndex);
            _items.Insert(targetIndex, view);
            view.Row.RemoveFromHierarchy();
            Insert(targetIndex, view.Row);
            var revision = ++_reorderRevision;
            schedule.Execute(
                () =>
                {
                    if (revision == _reorderRevision)
                    {
                        AnimateReorderedItems(
                            previousPositions,
                            _draggedItem);
                    }
                });
            OrderChanged?.Invoke(Order);
        }

        private void AnimateReorderedItems(
            IReadOnlyDictionary<ItemView, Vector2> previousPositions,
            ItemView excludedItem)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == excludedItem)
                {
                    item.Row.transform.position = Vector3.zero;
                    continue;
                }

                if (!previousPositions.TryGetValue(
                        item,
                        out var previousPosition))
                {
                    continue;
                }

                item.Row.transform.position = Vector3.zero;
                var nextPosition = item.Row.worldBound.position;
                if (!IsFinite(previousPosition) ||
                    !IsFinite(nextPosition))
                {
                    continue;
                }

                var offset = previousPosition - nextPosition;
                if (offset.sqrMagnitude <=
                    UiSizeTokens.Size1 * UiSizeTokens.Size1)
                {
                    continue;
                }

                item.Row.transform.position =
                    new Vector3(offset.x, offset.y, 0f);
                var animation =
                    item.Row.experimental.animation.Position(
                    Vector3.zero,
                    ReorderAnimationDurationMs);
                item.PositionAnimation = animation;
                animation.OnCompleted(
                    () =>
                    {
                        if (item.PositionAnimation == animation)
                        {
                            item.Row.transform.position =
                                Vector3.zero;
                            item.PositionAnimation = null;
                        }
                    });
            }
        }

        private void StopPositionAnimations()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var animation = _items[i].PositionAnimation;
                if (animation == null)
                {
                    continue;
                }

                _items[i].PositionAnimation = null;
                animation.Stop();
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y);
        }

        private sealed class ItemView
        {
            public ItemView(
                string id,
                VisualElement row,
                UiTextElement handle)
            {
                Id = id ?? string.Empty;
                Row = row;
                Handle = handle;
            }

            public string Id { get; }

            public VisualElement Row { get; }

            public UiTextElement Handle { get; }

            public ValueAnimation<Vector3> PositionAnimation { get; set; }
        }
    }
}
