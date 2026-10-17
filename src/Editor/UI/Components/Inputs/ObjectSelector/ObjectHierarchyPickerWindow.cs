using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ee4v.UI
{
    internal sealed class ObjectHierarchyPickerWindow : CustomPopupWindow
    {
        private IReadOnlyList<SearchableTreeItemData<Object>> _items;
        private Action<IReadOnlyList<Object>> _select;
        private IReadOnlyList<Object> _selected = Array.Empty<Object>();
        private UiButton _add;

        internal static void Show(VisualElement anchor,
            IReadOnlyList<SearchableTreeItemData<Object>> items, Action<IReadOnlyList<Object>> select)
        {
            if (anchor == null || items == null || items.Count == 0 || select == null) return;
            foreach (var existing in Resources.FindObjectsOfTypeAll<ObjectHierarchyPickerWindow>()) existing.Close();
            var window = CreateInstance<ObjectHierarchyPickerWindow>();
            window._items = items;
            window._select = select;
            window.ShowAsPopup(anchor, new Vector2(520f, 460f));
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(root);
            ConfigureCloseAndSubmitKeys(root, AddSelected);
            var popup = new CustomPopup(UiLocalization.Get("ui.objectPicker.title"), showFooter: true,
                closeTooltip: UiLocalization.Get("ui.objectPicker.close"));
            var body = new VisualElement();
            body.AddToClassList("ee4v-ui-object-picker__body");
            var tree = new SearchableTreeView<Object>(() => UiTextFactory.Create(),
                (element, item) => ((UiTextElement)element).SetText(item != null ? item.name : string.Empty),
                selection =>
                {
                    _selected = selection.Where(item => item != null).Distinct().ToArray();
                    RefreshAdd();
                },
                emptyText: UiLocalization.Get("ui.objectPicker.noMatch"),
                searchPlaceholder: UiLocalization.Get("ui.objectPicker.search"),
                selectionType: SelectionType.Multiple,
                canInteractWithItem: item => item != null,
                searchTooltip: UiLocalization.Get("ui.objectPicker.search"),
                clearTooltip: UiLocalization.Get("ui.clear.tooltip"),
                searchIconState: FluentUiIcons.CreateState("search.png", UiSizeTokens.Size14),
                clearIconState: FluentUiIcons.CreateState("dismiss.png", UiSizeTokens.Size10),
                verticalScrollerVisibility: ScrollerVisibility.Auto)
            { name = "ee4v-object-hierarchy-tree" };
            tree.AddToClassList("ee4v-ui-object-picker__tree");
            tree.SetItems(_items);
            body.Add(tree);
            body.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != (int)MouseButton.LeftMouse) return;
                for (var element = evt.target as VisualElement; element != null && element != body; element = element.parent)
                    if (element is SearchField || element is Scroller ||
                        element.ClassListContains("unity-collection-view__item")) return;
                tree.ClearSelection();
                _selected = Array.Empty<Object>();
                RefreshAdd();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            popup.Content.Add(body);
            popup.Footer.Add(new UiButton(UiLocalization.Get("ui.objectPicker.cancel"), Close));
            _add = new UiButton(string.Empty, AddSelected) { name = "ee4v-object-hierarchy-add" };
            RefreshAdd();
            popup.Footer.Add(_add);
            SetPopup(popup);
            tree.schedule.Execute(() => tree.Q<SearchField>()?.Q<TextField>()?.Focus());
        }

        private void RefreshAdd()
        {
            if (_add == null) return;
            _add.SetLabel(string.Format(UiLocalization.Get("ui.objectPicker.addSelected"), _selected.Count));
            _add.SetPrimaryActionEnabled(_selected.Count > 0);
        }

        private void AddSelected()
        {
            var selected = _selected.Where(item => item != null).ToArray();
            if (selected.Length == 0) return;
            _select?.Invoke(selected);
            Close();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _items = null;
            _select = null;
            _selected = Array.Empty<Object>();
            _add = null;
        }
    }
}
