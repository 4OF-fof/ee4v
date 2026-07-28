using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class NavigationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--navigation";
        private const string PickerSectionClassName = "ee4v-asset-manager-panel__navigation-picker";
        private const string SectionHeaderClassName =
            "ee4v-asset-manager-panel__section-header";
        private const string SectionTitleClassName =
            "ee4v-asset-manager-panel__section-title";
        private const string HeaderActionClassName =
            "ee4v-asset-manager-panel__header-action";
        private const string CollectionsScrollClassName =
            "ee4v-asset-manager-panel__collections-scroll";
        private const string CollectionsContentClassName =
            "ee4v-asset-manager-panel__collections-content";
        private const string CollectionSectionClassName =
            "ee4v-asset-manager-panel__collection-section";
        private const string CollectionSectionToggleClassName =
            "ee4v-asset-manager-panel__collection-section-toggle";
        private const string CollectionSectionHeaderClassName =
            "ee4v-asset-manager-panel__collection-section-header";
        private const string CollectionSectionDisclosureClassName =
            "ee4v-asset-manager-panel__collection-section-disclosure";
        private const string CollectionSectionContentClassName =
            "ee4v-asset-manager-panel__collection-section-content";
        private const string CollectionErrorClassName =
            "ee4v-asset-manager-panel__collection-error";
        private readonly SingleSelectButtonGroup _group;
        private readonly CollectionNavigationList _collectionList;
        private readonly CollectionNavigationList _smartCollectionList;
        private readonly UiTextElement _error;

        public NavigationPanel(
            AssetManagerViewItemState[] items = null,
            string selectedItemId = null)
        {
            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);

            var pickerSection = new VisualElement();
            pickerSection.AddToClassList(PickerSectionClassName);

            var libraryHeader = CreateHeader(
                I18N.Get("assetManager.navigation.library"),
                I18N.Get("assetManager.navigation.manualSync"),
                UiBuiltinIcon.Refresh,
                button => ManualSyncRequested?.Invoke());
            pickerSection.Add(libraryHeader);

            _group = new SingleSelectButtonGroup(
                CreateGroupState(
                    items ?? AssetManagerNavigationCatalog.Items,
                    selectedItemId ?? AssetManagerNavigationCatalog.DefaultItemId),
                itemId => SelectionChanged?.Invoke(itemId));
            pickerSection.Add(_group);

            Add(pickerSection);

            var collectionsScroll = new ScrollView();
            collectionsScroll.AddToClassList(CollectionsScrollClassName);
            collectionsScroll.contentContainer.AddToClassList(
                CollectionsContentClassName);

            _error = UiTextFactory.Create(
                string.Empty,
                UiClassNames.NavigationItemLabel,
                CollectionErrorClassName);
            _error.SetWhiteSpace(WhiteSpace.Normal);
            collectionsScroll.Add(_error);

            _collectionList = new CollectionNavigationList(
                itemId => SelectionChanged?.Invoke(itemId));
            collectionsScroll.Add(CreateCollectionSection(
                I18N.Get("assetManager.navigation.collections.title"),
                I18N.Get(
                    "assetManager.navigation.collections.createCollection"),
                _collectionList,
                button => CreateCollectionRequested?.Invoke(button)));

            _smartCollectionList = new CollectionNavigationList(
                itemId => SelectionChanged?.Invoke(itemId));
            collectionsScroll.Add(CreateCollectionSection(
                I18N.Get(
                    "assetManager.navigation.smartCollections.title"),
                I18N.Get(
                    "assetManager.navigation.collections.createSmartCollection"),
                _smartCollectionList,
                button => CreateSmartCollectionRequested?.Invoke(button)));
            Add(collectionsScroll);
        }

        public event Action<string> SelectionChanged;

        public event Action<VisualElement> CreateCollectionRequested;

        public event Action<VisualElement> CreateSmartCollectionRequested;

        public event Action ManualSyncRequested;

        public void SetSelectedItem(string itemId)
        {
            _group.SetSelectedItem(itemId, notify: false);
            _collectionList.SetSelectedItem(itemId);
            _smartCollectionList.SetSelectedItem(itemId);
        }

        public void SetCollections(
            IReadOnlyList<AssetCollection> collections,
            string selectedItemId)
        {
            var snapshot =
                collections ?? Array.Empty<AssetCollection>();
            _collectionList.SetState(
                snapshot.Where(item =>
                    item != null && !item.IsSmartCollection).ToArray(),
                selectedItemId);
            _smartCollectionList.SetState(
                snapshot.Where(item =>
                    item != null && item.IsSmartCollection).ToArray(),
                selectedItemId);
        }

        public void SetCollectionError(string message)
        {
            _error.SetText(message ?? string.Empty);
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

        private static Foldout CreateCollectionSection(
            string title,
            string createTooltip,
            CollectionNavigationList list,
            Action<VisualElement> create)
        {
            var section = new Foldout { value = true };
            section.AddToClassList(CollectionSectionClassName);
            UiTextFactory.AttachToFoldout(
                section,
                title,
                UiClassNames.SectionTitle,
                SectionTitleClassName);

            var toggle = section.Q<Toggle>();
            toggle.AddToClassList(CollectionSectionToggleClassName);
            var disclosure = toggle.Q<VisualElement>(
                className: "unity-foldout__checkmark");
            disclosure?.AddToClassList(
                CollectionSectionDisclosureClassName);
            var header = toggle.Q<VisualElement>(
                className: "unity-foldout__input") ?? toggle;
            header.AddToClassList(CollectionSectionHeaderClassName);

            var createButton = CreateActionButton(
                createTooltip,
                UiBuiltinIcon.Add,
                create);
            createButton.RegisterCallback<PointerDownEvent>(
                evt => evt.StopPropagation());
            createButton.RegisterCallback<PointerUpEvent>(
                evt => evt.StopPropagation());
            createButton.RegisterCallback<ClickEvent>(
                evt => evt.StopPropagation());
            header.Add(createButton);

            section.contentContainer.AddToClassList(
                CollectionSectionContentClassName);
            section.Add(list);
            return section;
        }

        private static VisualElement CreateHeader(
            string title,
            string actionTooltip,
            UiBuiltinIcon actionIcon,
            Action<VisualElement> action)
        {
            var header = new VisualElement();
            header.AddToClassList(SectionHeaderClassName);
            header.Add(UiTextFactory.Create(
                title,
                UiClassNames.SectionTitle,
                SectionTitleClassName));

            var button = CreateActionButton(
                actionTooltip,
                actionIcon,
                action);
            header.Add(button);
            return header;
        }

        private static UiButton CreateActionButton(
            string tooltip,
            UiBuiltinIcon icon,
            Action<VisualElement> action)
        {
            var button = new UiButton(
                new UiButtonState(
                    tooltip: tooltip,
                    iconState: IconState.FromBuiltinIcon(
                        icon,
                        size: UiSizeTokens.Size12),
                    variant: UiButtonVariant.Ghost,
                    size: UiButtonSize.Compact));
            button.AddToClassList(HeaderActionClassName);
            button.clicked += () => action?.Invoke(button);
            return button;
        }
    }
}
