using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class ObjectSelector<TObject> : NavigationItem where TObject : UnityEngine.Object
    {
        private readonly IReadOnlyList<TObject> _candidates;
        private readonly Action<VisualElement, TObject, Action<TObject>> _showPicker;
        private readonly bool _allowEmpty;
        private readonly Func<TObject, bool> _canSelect;
        private readonly string _placeholder;
        private TObject _value;

        public ObjectSelector(
            IReadOnlyList<TObject> candidates,
            Action<VisualElement, TObject, Action<TObject>> showPicker = null,
            bool allowEmpty = false,
            string placeholder = null,
            Func<TObject, bool> canSelect = null)
            : base(onClick: null)
        {
            _candidates = candidates ?? Array.Empty<TObject>();
            _showPicker = showPicker;
            _allowEmpty = allowEmpty;
            if (canSelect != null && showPicker == null)
                throw new ArgumentException("A selection predicate requires a custom picker.", nameof(showPicker));
            _canSelect = canSelect;
            _placeholder = placeholder ?? UiLocalization.Get("ui.objectPicker.select");
            AddToClassList(
                "ee4v-ui-object-selector");
            clicked += OpenPopup;
            Trailing.Add(new Icon(
                FluentUiIcons.CreateState(
                    "chevron_down.png",
                    UiSizeTokens.Size12)));

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            SetEnabled(_candidates.Count > 0 || _canSelect != null);
            Refresh();
        }

        public event Action<TObject> ValueChanged;

        public event Action SelectionRejected;

        public TObject Value => _value;

        public void ClearValue()
        {
            SetValue(null);
        }

        public static void ShowPicker(VisualElement anchor,
            IReadOnlyList<TObject> candidates, TObject selected,
            Action<TObject> select, bool showAsGrid = false)
        {
            if (select == null) { return; }
            ObjectPickerWindow.Show(anchor, candidates?.Cast<UnityEngine.Object>().ToArray(), selected,
                value => select(value as TObject), showAsGrid);
        }

        public static void ShowHierarchyPicker(VisualElement anchor,
            IReadOnlyList<SearchableTreeItemData<TObject>> items,
            Action<IReadOnlyList<TObject>> select)
        {
            if (select == null || items == null) { return; }
            SearchableTreeItemData<UnityEngine.Object> ConvertItem(SearchableTreeItemData<TObject> item) =>
                new SearchableTreeItemData<UnityEngine.Object>(item.Id, item.Data, item.SearchText,
                    item.TooltipText, item.Children.Select(ConvertItem).ToArray());
            ObjectHierarchyPickerWindow.Show(anchor, items.Select(ConvertItem).ToArray(),
                values => select(values.OfType<TObject>().ToArray()));
        }

        public void SetValueWithoutNotify(TObject candidate)
        {
            if (!Accepts(candidate))
            {
                return;
            }

            _value = candidate;
            Refresh();
        }

        private void OpenPopup()
        {
            if (_showPicker != null)
            {
                _showPicker(this, _value, SetValue);
                return;
            }
            ObjectPickerWindow.Show(
                this,
                _candidates.Cast<UnityEngine.Object>().ToArray(),
                _value,
                value => SetValue(value as TObject));
        }

        private void SetValue(TObject candidate)
        {
            if (!Accepts(candidate))
            {
                SelectionRejected?.Invoke();
                return;
            }

            if (_value == candidate)
            {
                return;
            }

            _value = candidate;
            Refresh();
            ValueChanged?.Invoke(_value);
        }

        private void Refresh()
        {
            var path = AssetDatabase.GetAssetPath(_value);
            SetState(new NavigationItemState(
                _value != null
                    ? _value.name
                    : _placeholder,
                path));
            tooltip = path;
        }

        private bool Accepts(TObject candidate)
        {
            if (candidate == null) { return _allowEmpty; }
            return _canSelect != null ? _canSelect(candidate) : _candidates.Contains(candidate);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            var candidate = GetDraggedObject();
            DragAndDrop.visualMode = candidate != null && Accepts(candidate)
                ? DragAndDropVisualMode.Link
                : DragAndDropVisualMode.Rejected;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var candidate = GetDraggedObject();
            if (candidate != null && Accepts(candidate))
            {
                DragAndDrop.AcceptDrag();
                SetValue(candidate);
            }
            else
            {
                SelectionRejected?.Invoke();
            }

            evt.StopPropagation();
        }

        private static TObject GetDraggedObject()
        {
            return DragAndDrop.objectReferences
                .OfType<TObject>()
                .FirstOrDefault();
        }
    }
}
