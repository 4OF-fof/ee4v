using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class SearchableTreeItemData<TData>
    {
        public SearchableTreeItemData(int id, TData data, string searchText = null, IReadOnlyList<SearchableTreeItemData<TData>> children = null)
        {
            Id = id;
            Data = data;
            SearchText = searchText ?? string.Empty;
            Children = children ?? new SearchableTreeItemData<TData>[0];
        }

        public int Id { get; }

        public TData Data { get; }

        public string SearchText { get; }

        public IReadOnlyList<SearchableTreeItemData<TData>> Children { get; }
    }

    internal sealed class SearchableTreeView<TData> : VisualElement
    {
        private readonly VisualElement _searchContainer;
        private readonly TextField _searchInput;
        private readonly Button _clearButton;
        private readonly TreeView _treeView;
        private readonly UiTextElement _emptyLabel;
        private readonly Action<VisualElement, TData> _bindItem;
        private readonly Action<IReadOnlyList<TData>> _onSelectionChanged;
        private IReadOnlyList<SearchableTreeItemData<TData>> _sourceItems;
        private Action<string> _onSearchValueChanged;

        public SearchableTreeView(
            Func<VisualElement> makeItem,
            Action<VisualElement, TData> bindItem,
            Action<IReadOnlyList<TData>> onSelectionChanged = null,
            string emptyText = "")
        {
            if (makeItem == null)
            {
                throw new ArgumentNullException(nameof(makeItem));
            }

            _bindItem = bindItem ?? throw new ArgumentNullException(nameof(bindItem));
            _onSelectionChanged = onSelectionChanged;

            AddToClassList(UiClassNames.SearchableTreeView);

            _searchContainer = new VisualElement();
            _searchContainer.AddToClassList(UiClassNames.SearchableTreeViewSearch);
            _searchContainer.style.minHeight = 18f;
            _searchContainer.style.height = 18f;

            _searchInput = new TextField();
            _searchInput.AddToClassList(UiClassNames.SearchableTreeViewSearchInput);
            _searchInput.style.minHeight = 0f;
            _searchInput.style.height = 14f;
            _searchInput.style.marginTop = 0f;
            _searchInput.style.marginBottom = 0f;
            _searchInput.RegisterValueChangedCallback(evt =>
            {
                RefreshSearchVisualState(evt.newValue);
                if (_onSearchValueChanged != null)
                {
                    _onSearchValueChanged(evt.newValue ?? string.Empty);
                }
            });

            _clearButton = new Button(ClearSearch)
            {
                text = "X"
            };
            _clearButton.AddToClassList(UiClassNames.SearchableTreeViewSearchClear);
            _clearButton.style.width = 12f;
            _clearButton.style.minWidth = 12f;
            _clearButton.style.maxWidth = 12f;
            _clearButton.style.height = 12f;
            _clearButton.style.minHeight = 12f;
            _clearButton.style.maxHeight = 12f;

            _searchContainer.Add(_searchInput);
            _searchContainer.Add(_clearButton);
            Add(_searchContainer);

            _treeView = new TreeView();
            _treeView.AddToClassList(UiClassNames.SearchableTreeViewTree);
            _treeView.selectionType = SelectionType.Single;
            _treeView.fixedItemHeight = 20;
            _treeView.makeItem = makeItem;
            _treeView.bindItem = BindItem;
            _treeView.selectionChanged += OnSelectionChanged;
            Add(_treeView);

            _emptyLabel = UiTextFactory.Create(emptyText, UiClassNames.SearchableTreeViewEmpty);
            _emptyLabel.SetWhiteSpace(WhiteSpace.Normal);
            Add(_emptyLabel);

            SetEmptyText(emptyText);
            SetSearchState(string.Empty, _ => RefreshTree());
            SetItems(null);
        }

        public void SetItems(IReadOnlyList<SearchableTreeItemData<TData>> items)
        {
            _sourceItems = items ?? new SearchableTreeItemData<TData>[0];
            RefreshTree();
        }

        public void SetEmptyText(string emptyText)
        {
            _emptyLabel.SetText(emptyText ?? string.Empty);
        }

        public void SetSelectionById(IEnumerable<int> itemIds)
        {
            if (itemIds == null)
            {
                _treeView.ClearSelection();
                return;
            }

            _treeView.SetSelectionById(new List<int>(itemIds));
        }

        public void ClearSelection()
        {
            _treeView.ClearSelection();
        }

        public void SetViewDataKey(string viewDataKey)
        {
            _treeView.viewDataKey = viewDataKey ?? string.Empty;
        }

        private void BindItem(VisualElement element, int index)
        {
            var item = _treeView.GetItemDataForIndex<SearchableTreeItemData<TData>>(index);
            _bindItem(element, item.Data);
        }

        private void OnSelectionChanged(IEnumerable<object> items)
        {
            if (_onSelectionChanged == null || items == null)
            {
                return;
            }

            var selected = new List<TData>();
            foreach (var item in items)
            {
                if (item is SearchableTreeItemData<TData> treeItem)
                {
                    selected.Add(treeItem.Data);
                }
            }

            _onSelectionChanged(selected);
        }

        private void RefreshTree()
        {
            var filteredItems = FilterItems(_sourceItems, _searchInput.value);
            _treeView.SetRootItems(filteredItems);
            _treeView.Rebuild();
            _treeView.ExpandAll();

            var hasItems = filteredItems.Count > 0;
            _treeView.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void SetSearchState(string value, Action<string> onValueChanged = null)
        {
            _onSearchValueChanged = onValueChanged;
            _searchInput.SetValueWithoutNotify(value ?? string.Empty);
            _searchInput.tooltip = "Search";
            RefreshSearchVisualState(_searchInput.value);
        }

        private void ClearSearch()
        {
            if (string.IsNullOrEmpty(_searchInput.value))
            {
                return;
            }

            _searchInput.value = string.Empty;
        }

        private void RefreshSearchVisualState(string value)
        {
            var hasValue = !string.IsNullOrWhiteSpace(value);
            _searchContainer.EnableInClassList(UiClassNames.SearchableTreeViewSearchHasValue, hasValue);
            _clearButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static List<TreeViewItemData<SearchableTreeItemData<TData>>> FilterItems(IReadOnlyList<SearchableTreeItemData<TData>> sourceItems, string query)
        {
            var results = new List<TreeViewItemData<SearchableTreeItemData<TData>>>();
            if (sourceItems == null)
            {
                return results;
            }

            var normalizedQuery = (query ?? string.Empty).Trim();
            for (var i = 0; i < sourceItems.Count; i++)
            {
                var filteredItem = FilterItem(sourceItems[i], normalizedQuery);
                if (filteredItem.HasValue)
                {
                    results.Add(filteredItem.Value);
                }
            }

            return results;
        }

        private static TreeViewItemData<SearchableTreeItemData<TData>>? FilterItem(SearchableTreeItemData<TData> item, string query)
        {
            if (item == null)
            {
                return null;
            }

            var filteredChildren = FilterItems(item.Children, query);
            var isMatch = string.IsNullOrWhiteSpace(query)
                || item.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isMatch && filteredChildren.Count == 0)
            {
                return null;
            }

            return new TreeViewItemData<SearchableTreeItemData<TData>>(item.Id, item, filteredChildren);
        }
    }
}
