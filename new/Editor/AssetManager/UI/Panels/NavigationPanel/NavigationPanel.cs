using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class NavigationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--navigation";
        private const string PickerSectionClassName = "ee4v-asset-manager-panel__navigation-picker";
        private readonly SingleSelectButtonGroup _group;

        public NavigationPanel()
        {
            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);

            var pickerSection = new VisualElement();
            pickerSection.AddToClassList(PickerSectionClassName);

            _group = new SingleSelectButtonGroup(CreateGroupState(), OnGroupSelectionChanged);
            pickerSection.Add(_group);

            Add(pickerSection);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged += OnSelectedItemChanged;
            RefreshSelectionVisuals();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerViewState.SelectedItemChanged -= OnSelectedItemChanged;
        }

        private static SingleSelectButtonGroupState CreateGroupState()
        {
            var items = new SingleSelectButtonGroupItemState[AssetManagerViewState.Items.Length];
            for (var i = 0; i < AssetManagerViewState.Items.Length; i++)
            {
                var item = AssetManagerViewState.Items[i];
                items[i] = new SingleSelectButtonGroupItemState(
                    item.Id,
                    item.Label,
                    item.Meta,
                    true,
                    item.IconState);
            }

            return new SingleSelectButtonGroupState(items, AssetManagerViewState.SelectedItemId);
        }

        private void OnGroupSelectionChanged(string itemId)
        {
            AssetManagerViewState.SetSelectedItem(itemId);
        }

        private void OnSelectedItemChanged(string itemId)
        {
            RefreshSelectionVisuals();
        }

        private void RefreshSelectionVisuals()
        {
            _group.SetSelectedItem(AssetManagerViewState.SelectedItemId, notify: false);
        }
    }
}
