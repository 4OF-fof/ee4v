using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class DraggableToggleItemState
    {
        public DraggableToggleItemState(string id, string label, bool value = false, bool enabled = true)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value;
            Enabled = enabled;
        }

        public string Id { get; }

        public string Label { get; }

        public bool Value { get; }

        public bool Enabled { get; }
    }

    internal sealed class DraggableToggleGroupState
    {
        public DraggableToggleGroupState(IReadOnlyList<DraggableToggleItemState> items, bool enabled = true)
        {
            Items = items ?? Array.Empty<DraggableToggleItemState>();
            Enabled = enabled;
        }

        public IReadOnlyList<DraggableToggleItemState> Items { get; }

        public bool Enabled { get; }
    }

    internal sealed class DraggableToggleGroup : VisualElement
    {
        private const string RootClassName = "ee4v-ui-draggable-toggle-group";
        private const string DisabledClassName = "ee4v-ui-draggable-toggle-group--disabled";
        private const string DraggingClassName = "ee4v-ui-draggable-toggle-group--dragging";
        private const string ToggleClassName = "ee4v-ui-draggable-toggle-group__toggle";
        private const string ToggleCheckedClassName = "ee4v-ui-draggable-toggle-group__toggle--checked";
        private const string ToggleDisabledClassName = "ee4v-ui-draggable-toggle-group__toggle--disabled";
        private const string ToggleLabelClassName = "ee4v-ui-draggable-toggle-group__label";
        private const string StandardToggleClassName = "ee4v-ui-draggable-toggle-group__standard-toggle";

        private readonly List<ToggleView> _toggleViews = new List<ToggleView>();
        private readonly HashSet<string> _appliedItemIds = new HashSet<string>(StringComparer.Ordinal);
        private Action<string, bool> _onValueChanged;
        private bool _isDragging;
        private bool _dragValue;
        private int _dragPointerId = -1;
        private Vector2 _dragStartPosition;

        public DraggableToggleGroup(DraggableToggleGroupState state = null, Action<string, bool> onValueChanged = null)
        {
            AddToClassList(RootClassName);
            focusable = true;
            pickingMode = PickingMode.Position;

            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            SetState(state ?? new DraggableToggleGroupState(null), onValueChanged);
        }

        public event Action<string, bool> ValueChanged;

        public void SetState(DraggableToggleGroupState state, Action<string, bool> onValueChanged = null)
        {
            state = state ?? new DraggableToggleGroupState(null);
            if (onValueChanged != null)
            {
                _onValueChanged = onValueChanged;
            }

            _toggleViews.Clear();
            Clear();
            SetEnabled(state.Enabled);

            for (var i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item == null)
                {
                    continue;
                }

                var view = CreateToggleView(item);
                Add(view.Element);
                _toggleViews.Add(view);
            }
        }

        public new void SetEnabled(bool value)
        {
            base.SetEnabled(value);
            EnableInClassList(DisabledClassName, !value);
        }

        public IReadOnlyDictionary<string, bool> GetValues()
        {
            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (var i = 0; i < _toggleViews.Count; i++)
            {
                values[_toggleViews[i].Id] = _toggleViews[i].Value;
            }

            return values;
        }

        public bool TryGetValue(string itemId, out bool value)
        {
            var view = FindView(itemId);
            if (view == null)
            {
                value = false;
                return false;
            }

            value = view.Value;
            return true;
        }

        public void SetValueWithoutNotify(string itemId, bool value)
        {
            var view = FindView(itemId);
            if (view == null || !view.Enabled)
            {
                return;
            }

            SetViewValue(view, value, notify: false);
        }

        public void SetValue(string itemId, bool value)
        {
            var view = FindView(itemId);
            if (view == null || !view.Enabled)
            {
                return;
            }

            SetViewValue(view, value, notify: true);
        }

        private ToggleView CreateToggleView(DraggableToggleItemState item)
        {
            var element = new VisualElement
            {
                focusable = true,
                pickingMode = PickingMode.Position
            };
            element.AddToClassList(ToggleClassName);
            element.EnableInClassList(ToggleCheckedClassName, item.Value);
            element.EnableInClassList(ToggleDisabledClassName, !item.Enabled);
            element.SetEnabled(item.Enabled);

            var label = UiTextFactory.Create(item.Label);
            label.AddToClassList(ToggleLabelClassName);
            label.pickingMode = PickingMode.Ignore;
            label.SetWhiteSpace(WhiteSpace.NoWrap);

            var toggle = new Toggle()
            {
                value = item.Value,
                pickingMode = PickingMode.Ignore
            };
            toggle.AddToClassList(StandardToggleClassName);
            toggle.SetEnabled(item.Enabled);
            element.Add(toggle);
            element.Add(label);
            SetPickingModeRecursive(toggle, PickingMode.Ignore);

            var view = new ToggleView(item.Id, element, toggle, item.Value, item.Enabled);
            element.RegisterCallback<PointerDownEvent>(evt => OnTogglePointerDown(evt, view), TrickleDown.TrickleDown);
            element.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            return view;
        }

        private ToggleView FindView(string itemId)
        {
            itemId = itemId ?? string.Empty;
            for (var i = 0; i < _toggleViews.Count; i++)
            {
                if (string.Equals(_toggleViews[i].Id, itemId, StringComparison.Ordinal))
                {
                    return _toggleViews[i];
                }
            }

            return null;
        }

        private void OnTogglePointerDown(PointerDownEvent evt, ToggleView view)
        {
            if (view == null || !enabledInHierarchy || !view.Enabled || evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            Focus();
            _isDragging = true;
            _dragPointerId = evt.pointerId;
            _dragValue = !view.Value;
            _dragStartPosition = ToVector2(evt.position);
            _appliedItemIds.Clear();
            this.CapturePointer(_dragPointerId);
            EnableInClassList(DraggingClassName, true);

            ApplyToView(view, _dragValue);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || evt.pointerId != _dragPointerId)
            {
                return;
            }

            ApplySelection(CreateSelectionRect(_dragStartPosition, ToVector2(evt.position)));
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging || evt.pointerId != _dragPointerId)
            {
                return;
            }

            ApplySelection(CreateSelectionRect(_dragStartPosition, ToVector2(evt.position)));
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
            _appliedItemIds.Clear();
            EnableInClassList(DraggingClassName, false);
        }

        private void ApplySelection(Rect selectionRect)
        {
            for (var i = 0; i < _toggleViews.Count; i++)
            {
                var view = _toggleViews[i];
                if (!view.Enabled || _appliedItemIds.Contains(view.Id))
                {
                    continue;
                }

                if (SelectionOverlaps(selectionRect, view.Element.worldBound))
                {
                    ApplyToView(view, _dragValue);
                }
            }
        }

        private void ApplyToView(ToggleView view, bool value)
        {
            _appliedItemIds.Add(view.Id);
            SetViewValue(view, value, notify: true);
        }

        private void SetViewValue(ToggleView view, bool value, bool notify)
        {
            if (view.Value == value)
            {
                return;
            }

            view.Value = value;
            view.Element.EnableInClassList(ToggleCheckedClassName, value);
            view.Toggle.SetValueWithoutNotify(value);

            if (notify)
            {
                ValueChanged?.Invoke(view.Id, value);
                _onValueChanged?.Invoke(view.Id, value);
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!enabledInHierarchy || evt.keyCode != KeyCode.Space)
            {
                return;
            }

            for (var i = 0; i < _toggleViews.Count; i++)
            {
                var view = _toggleViews[i];
                if (view.Enabled && view.Element == evt.target)
                {
                    SetViewValue(view, !view.Value, notify: true);
                    evt.StopPropagation();
                    return;
                }
            }
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

        private static void SetPickingModeRecursive(VisualElement element, PickingMode pickingMode)
        {
            if (element == null)
            {
                return;
            }

            element.pickingMode = pickingMode;
            for (var i = 0; i < element.childCount; i++)
            {
                SetPickingModeRecursive(element.ElementAt(i), pickingMode);
            }
        }

        private sealed class ToggleView
        {
            public ToggleView(string id, VisualElement element, Toggle toggle, bool value, bool enabled)
            {
                Id = id ?? string.Empty;
                Element = element;
                Toggle = toggle;
                Value = value;
                Enabled = enabled;
            }

            public string Id { get; }

            public VisualElement Element { get; }

            public Toggle Toggle { get; }

            public bool Value { get; set; }

            public bool Enabled { get; }
        }
    }
}
