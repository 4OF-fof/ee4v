using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class ExpressionTargetPickerWindow : CustomPopupWindow
    {
        private BlendShapeChannel[] _targets;
        private int _mapping;
        private Action<int> _select;
        private PickerItem _selected;
        private UiButton _selectButton;

        internal static ExpressionTargetPickerWindow Show(VisualElement anchor,
            BlendShapeChannel[] targets, int mapping, Action<int> select)
        {
            foreach (var existing in Resources.FindObjectsOfTypeAll<ExpressionTargetPickerWindow>())
            { existing.Close(); }
            var window = CreateInstance<ExpressionTargetPickerWindow>();
            window._targets = targets;
            window._mapping = mapping;
            window._select = select;
            window.ShowAsPopup(anchor, new Vector2(520f, 460f));
            return window;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(root, "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");
            ConfigureCloseAndSubmitKeys(root, SelectCurrent);
            var popup = new CustomPopup(I18N.Get("conversion.pickerTitle"), showFooter: true,
                closeTooltip: I18N.Get("conversion.pickerCancel"));
            var tree = new SearchableTreeView<PickerItem>(() => UiTextFactory.Create(),
                (element, item) => ((UiTextElement)element).SetText(item.Name),
                selection =>
                {
                    _selected = selection.FirstOrDefault();
                    _selectButton?.SetPrimaryActionEnabled(_selected?.Mapping >= 1);
                },
                emptyText: I18N.Get("conversion.pickerEmpty"),
                searchPlaceholder: I18N.Get("conversion.pickerSearch"),
                canInteractWithItem: item => item.Mapping >= 1,
                onItemDoubleClicked: item => Select(item.Mapping),
                searchTooltip: I18N.Get("conversion.pickerSearch"),
                clearTooltip: I18N.Get("conversion.pickerClearSearch"));
            tree.SetItems(BuildItems());
            tree.Q<TreeView>()?.ExpandAll();
            popup.Content.Add(tree);
            popup.Footer.Add(new UiButton(I18N.Get("conversion.pickerCancel"), Close));
            _selectButton = new UiButton(I18N.Get("conversion.pickerSelect"), SelectCurrent);
            _selectButton.SetPrimaryActionEnabled(false);
            popup.Footer.Add(_selectButton);
            if (_mapping >= 1) { tree.SetSelectionById(new[] { _mapping }); }
            SetPopup(popup);
            tree.schedule.Execute(() => tree.Q<SearchField>()?.Q<TextField>()?.Focus());
        }

        private IReadOnlyList<SearchableTreeItemData<PickerItem>> BuildItems()
        {
            var groupId = _targets.Length + 2;
            var items = new List<SearchableTreeItemData<PickerItem>>
            {
                new SearchableTreeItemData<PickerItem>(1,
                    new PickerItem(I18N.Get("conversion.skip"), 1), I18N.Get("conversion.skip"))
            };
            items.AddRange(_targets.Select((channel, index) => new { Channel = channel, Mapping = index + 2 })
                .GroupBy(item => item.Channel.RendererPath, StringComparer.Ordinal)
                .Select(group => new SearchableTreeItemData<PickerItem>(groupId++,
                    new PickerItem(string.IsNullOrEmpty(group.Key) ? I18N.Get("conversion.pickerRoot") : group.Key, -1),
                    group.Key,
                    group.Select(item => new SearchableTreeItemData<PickerItem>(item.Mapping,
                        new PickerItem(item.Channel.Name, item.Mapping),
                        item.Channel.RendererPath + " / " + item.Channel.Name)).ToArray()))
                .ToArray());
            return items;
        }

        private void SelectCurrent()
        {
            if (_selected?.Mapping >= 1) { Select(_selected.Mapping); }
        }

        private void Select(int mapping)
        {
            if (mapping < 1 || mapping >= _targets.Length + 2) { return; }
            _select?.Invoke(mapping);
            Close();
        }

        private sealed class PickerItem
        {
            internal PickerItem(string name, int mapping) { Name = name; Mapping = mapping; }
            internal string Name { get; }
            internal int Mapping { get; }
        }
    }
}
