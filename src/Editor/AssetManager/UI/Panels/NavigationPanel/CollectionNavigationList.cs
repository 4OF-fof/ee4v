using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class CollectionNavigationList : VisualElement
    {
        private const string RootClassName =
            "ee4v-asset-manager-collection-list";
        private const string ButtonClassName =
            "ee4v-asset-manager-collection-list__button";
        private readonly Action<string> _selected;
        private readonly List<CollectionButton> _buttons =
            new List<CollectionButton>();
        private string _selectedViewId = string.Empty;

        public CollectionNavigationList(Action<string> selected)
        {
            _selected = selected;
            AddToClassList(RootClassName);
        }

        public void SetState(
            IReadOnlyList<AssetCollection> collections,
            string selectedViewId)
        {
            _selectedViewId = selectedViewId ?? string.Empty;
            _buttons.Clear();
            Clear();

            var items = (collections ?? Array.Empty<AssetCollection>())
                .Where(item =>
                    item != null &&
                    !string.IsNullOrWhiteSpace(item.Id))
                .ToArray();
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
                        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.Id, StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);

            AddChildren(string.Empty, 0, children);
            RefreshSelection();
        }

        public void SetSelectedItem(string viewId)
        {
            _selectedViewId = viewId ?? string.Empty;
            RefreshSelection();
        }

        private void AddChildren(
            string parentId,
            int depth,
            IReadOnlyDictionary<string, AssetCollection[]> children)
        {
            AssetCollection[] items;
            if (!children.TryGetValue(parentId ?? string.Empty, out items))
            {
                return;
            }

            for (var i = 0; i < items.Length; i++)
            {
                var collection = items[i];
                var viewId =
                    AssetManagerCollectionViewId.Encode(collection.Id);
                var button = new UiButton(
                    new UiButtonState(
                        collection.Name,
                        iconState:
                        AssetCollectionIconPresenter.CreateState(
                            collection,
                            UiSizeTokens.Size12),
                        selected: string.Equals(
                            viewId,
                            _selectedViewId,
                            StringComparison.Ordinal),
                        variant: UiButtonVariant.Ghost),
                    () => _selected?.Invoke(viewId));
                button.AddToClassList(ButtonClassName);
                button.style.paddingLeft =
                    UiSpacingTokens.Medium +
                    depth * UiSpacingTokens.Xl;
                Add(button);
                _buttons.Add(new CollectionButton(viewId, button));
                AddChildren(collection.Id, depth + 1, children);
            }
        }

        private void RefreshSelection()
        {
            for (var i = 0; i < _buttons.Count; i++)
            {
                var item = _buttons[i];
                item.Button.SetSelected(
                    string.Equals(
                        item.ViewId,
                        _selectedViewId,
                        StringComparison.Ordinal));
            }
        }

        private sealed class CollectionButton
        {
            public CollectionButton(string viewId, UiButton button)
            {
                ViewId = viewId;
                Button = button;
            }

            public string ViewId { get; }

            public UiButton Button { get; }
        }
    }
}
