using System;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class NavigationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--navigation";
        private const string PickerSectionClassName = "ee4v-asset-manager-panel__navigation-picker";
        private readonly SingleSelectButtonGroup _group;

        public NavigationPanel(
            AssetManagerViewItemState[] items = null,
            string selectedItemId = null)
        {
            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);

            var pickerSection = new VisualElement();
            pickerSection.AddToClassList(PickerSectionClassName);

            _group = new SingleSelectButtonGroup(
                CreateGroupState(
                    items ?? AssetManagerNavigationCatalog.Items,
                    selectedItemId ?? AssetManagerNavigationCatalog.DefaultItemId),
                itemId => SelectionChanged?.Invoke(itemId));
            pickerSection.Add(_group);

            Add(pickerSection);
        }

        public event Action<string> SelectionChanged;

        public void SetSelectedItem(string itemId)
        {
            _group.SetSelectedItem(itemId, notify: false);
        }

        private static SingleSelectButtonGroupState CreateGroupState(
            AssetManagerViewItemState[] navigationItems,
            string selectedItemId)
        {
            var items = new SingleSelectButtonGroupItemState[navigationItems.Length];
            for (var i = 0; i < navigationItems.Length; i++)
            {
                var item = navigationItems[i];
                items[i] = new SingleSelectButtonGroupItemState(
                    item.Id,
                    item.Label,
                    item.Meta,
                    true,
                    item.IconState);
            }

            return new SingleSelectButtonGroupState(items, selectedItemId);
        }
    }
}
