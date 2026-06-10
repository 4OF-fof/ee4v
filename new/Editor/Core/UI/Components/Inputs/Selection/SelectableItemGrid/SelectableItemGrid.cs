using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal class SelectableItemGrid : ItemGrid
    {
        private const string SelectedSlotClassName = "ee4v-ui-selectable-item-grid__row-slot--selected";
        private readonly HashSet<int> _selectedItemIndices = new HashSet<int>();
        private readonly List<int> _selectedItemOrder = new List<int>();
        private readonly HashSet<int> _dragAppliedItemIndices = new HashSet<int>();
        private bool _isDragging;
        private bool _dragValue;
        private int _dragPointerId = -1;
        private int _selectionAnchorIndex = -1;
        private Vector2 _dragStartPosition;

        public SelectableItemGrid(ItemGridState state = null)
            : base(state)
        {
            AddToClassList("ee4v-ui-selectable-item-grid");
            focusable = true;
            pickingMode = PickingMode.Position;

            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public event Action<IReadOnlyList<ItemCardState>> SelectionChanged;

        public IReadOnlyList<ItemCardState> SelectedItems
        {
            get { return CreateSelectedItems(); }
        }

        public ItemCardState SelectedItem
        {
            get
            {
                var selectedItems = CreateSelectedItems();
                return selectedItems.Count > 0 ? selectedItems[0] : null;
            }
        }

        public override void SetState(ItemGridState state)
        {
            base.SetState(state);
            PruneSelection();
            RefreshVisibleSelection();
        }

        public void ClearSelection(bool notify = true)
        {
            if (_selectedItemIndices.Count == 0)
            {
                return;
            }

            _selectedItemIndices.Clear();
            _selectedItemOrder.Clear();
            _selectionAnchorIndex = -1;
            RefreshVisibleSelection();
            if (notify)
            {
                NotifySelectionChanged();
            }
        }

        protected override void OnCreateSlot(VisualElement slot)
        {
            slot.pickingMode = PickingMode.Position;
            slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown, TrickleDown.TrickleDown);
            slot.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
        }

        protected override void OnBindSlot(VisualElement slot, ItemCardState item, int itemIndex, bool hasItem)
        {
            ApplySlotSelection(slot, hasItem && _selectedItemIndices.Contains(itemIndex));
        }

        private void OnSlotPointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse || !enabledInHierarchy)
            {
                return;
            }

            var slot = evt.currentTarget as VisualElement;
            if (!(slot != null && slot.userData is int))
            {
                return;
            }

            var itemIndex = (int)slot.userData;
            if (itemIndex < 0 || itemIndex >= Items.Count)
            {
                return;
            }

            Focus();
            var additive = evt.ctrlKey || evt.commandKey;
            if (evt.shiftKey)
            {
                SelectRange(itemIndex);
                evt.StopPropagation();
                return;
            }

            if (additive)
            {
                BeginDragSelection(evt, itemIndex);
                evt.StopPropagation();
                return;
            }

            SelectSingle(itemIndex);
            evt.StopPropagation();
        }

        private void SelectSingle(int itemIndex)
        {
            var changed = _selectedItemIndices.Count != 1 || !_selectedItemIndices.Contains(itemIndex);
            _selectedItemIndices.Clear();
            _selectedItemOrder.Clear();
            _selectedItemIndices.Add(itemIndex);
            _selectedItemOrder.Add(itemIndex);
            _selectionAnchorIndex = itemIndex;

            if (changed)
            {
                RefreshVisibleSelection();
                NotifySelectionChanged();
            }
        }

        private void SelectRange(int itemIndex)
        {
            var anchorIndex = _selectionAnchorIndex >= 0 && _selectionAnchorIndex < Items.Count
                ? _selectionAnchorIndex
                : itemIndex;
            var startIndex = Mathf.Min(anchorIndex, itemIndex);
            var endIndex = Mathf.Max(anchorIndex, itemIndex);

            _selectedItemIndices.Clear();
            _selectedItemOrder.Clear();
            for (var i = startIndex; i <= endIndex; i++)
            {
                _selectedItemIndices.Add(i);
                _selectedItemOrder.Add(i);
            }

            RefreshVisibleSelection();
            NotifySelectionChanged();
        }

        private void BeginDragSelection(PointerDownEvent evt, int itemIndex)
        {
            _isDragging = true;
            _dragPointerId = evt.pointerId;
            _dragStartPosition = ToVector2(evt.position);
            _dragAppliedItemIndices.Clear();
            _dragValue = !_selectedItemIndices.Contains(itemIndex);
            _selectionAnchorIndex = itemIndex;
            this.CapturePointer(_dragPointerId);
            ApplyDraggedItem(itemIndex);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || evt.pointerId != _dragPointerId)
            {
                return;
            }

            ApplyDragSelection(CreateSelectionRect(_dragStartPosition, ToVector2(evt.position)));
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging || evt.pointerId != _dragPointerId)
            {
                return;
            }

            ApplyDragSelection(CreateSelectionRect(_dragStartPosition, ToVector2(evt.position)));
            EndDrag();
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_isDragging)
            {
                EndDragWithoutRelease();
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!enabledInHierarchy || evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            if (_isDragging)
            {
                EndDrag();
            }

            ClearSelection();
            evt.StopPropagation();
        }

        private void EndDrag()
        {
            if (this.HasPointerCapture(_dragPointerId))
            {
                this.ReleasePointer(_dragPointerId);
            }

            EndDragWithoutRelease();
        }

        private void EndDragWithoutRelease()
        {
            _isDragging = false;
            _dragPointerId = -1;
            _dragAppliedItemIndices.Clear();
        }

        private void ApplyDragSelection(Rect selectionRect)
        {
            var slots = ListView.Query<VisualElement>(className: RowSlotClassName).ToList();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var itemIndex = slot.userData is int ? (int)slot.userData : -1;
                if (itemIndex < 0 || itemIndex >= Items.Count || _dragAppliedItemIndices.Contains(itemIndex))
                {
                    continue;
                }

                if (SelectionOverlaps(selectionRect, slot.worldBound))
                {
                    ApplyDraggedItem(itemIndex);
                }
            }
        }

        private void ApplyDraggedItem(int itemIndex)
        {
            _dragAppliedItemIndices.Add(itemIndex);
            var changed = _dragValue ? AddSelectedItem(itemIndex) : RemoveSelectedItem(itemIndex);

            if (!changed)
            {
                return;
            }

            RefreshVisibleSelection();
            NotifySelectionChanged();
        }

        private void RefreshVisibleSelection()
        {
            var slots = ListView.Query<VisualElement>(className: RowSlotClassName).ToList();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var itemIndex = slot.userData is int ? (int)slot.userData : -1;
                ApplySlotSelection(slot, itemIndex >= 0 && _selectedItemIndices.Contains(itemIndex));
            }
        }

        private void PruneSelection()
        {
            if (_selectedItemIndices.Count == 0)
            {
                return;
            }

            var removed = false;
            var staleIndices = new List<int>();
            foreach (var index in _selectedItemIndices)
            {
                if (index < 0 || index >= Items.Count)
                {
                    staleIndices.Add(index);
                }
            }

            for (var i = 0; i < staleIndices.Count; i++)
            {
                removed |= RemoveSelectedItem(staleIndices[i]);
            }

            if (removed)
            {
                NotifySelectionChanged();
            }
        }

        private List<ItemCardState> CreateSelectedItems()
        {
            var items = new List<ItemCardState>(_selectedItemOrder.Count);
            for (var i = 0; i < _selectedItemOrder.Count; i++)
            {
                var index = _selectedItemOrder[i];
                if (index >= 0 && index < Items.Count)
                {
                    items.Add(Items[index]);
                }
            }

            return items;
        }

        private bool AddSelectedItem(int itemIndex)
        {
            if (!_selectedItemIndices.Add(itemIndex))
            {
                return false;
            }

            _selectedItemOrder.Add(itemIndex);
            return true;
        }

        private bool RemoveSelectedItem(int itemIndex)
        {
            if (!_selectedItemIndices.Remove(itemIndex))
            {
                return false;
            }

            _selectedItemOrder.Remove(itemIndex);
            return true;
        }

        private void NotifySelectionChanged()
        {
            SelectionChanged?.Invoke(CreateSelectedItems());
        }

        private static void ApplySlotSelection(VisualElement slot, bool selected)
        {
            if (selected)
            {
                slot.AddToClassList(SelectedSlotClassName);
                return;
            }

            slot.RemoveFromClassList(SelectedSlotClassName);
        }

        private static Vector2 ToVector2(Vector3 position)
        {
            return new Vector2(position.x, position.y);
        }

        private static Rect CreateSelectionRect(Vector2 start, Vector2 end)
        {
            return Rect.MinMaxRect(
                Mathf.Min(start.x, end.x),
                Mathf.Min(start.y, end.y),
                Mathf.Max(start.x, end.x),
                Mathf.Max(start.y, end.y));
        }

        private static bool SelectionOverlaps(Rect selectionRect, Rect targetRect)
        {
            if (selectionRect.width <= 0f && selectionRect.height <= 0f)
            {
                return targetRect.Contains(selectionRect.position);
            }

            return selectionRect.Overlaps(targetRect, true);
        }
    }
}
