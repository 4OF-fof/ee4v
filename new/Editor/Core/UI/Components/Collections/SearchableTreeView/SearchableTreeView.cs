using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class SearchableTreeItemData<TData>
    {
        public SearchableTreeItemData(int id, TData data, string searchText = null, IReadOnlyList<SearchableTreeItemData<TData>> children = null)
            : this(id, data, searchText, null, children)
        {
        }

        public SearchableTreeItemData(int id, TData data, string searchText, string tooltipText, IReadOnlyList<SearchableTreeItemData<TData>> children = null)
        {
            Id = id;
            Data = data;
            SearchText = searchText ?? string.Empty;
            TooltipText = tooltipText ?? SearchText;
            Children = children ?? new SearchableTreeItemData<TData>[0];
        }

        public int Id { get; }

        public TData Data { get; }

        public string SearchText { get; }

        public string TooltipText { get; }

        public IReadOnlyList<SearchableTreeItemData<TData>> Children { get; }
    }

    internal sealed class SearchableTreeView<TData> : VisualElement
    {
        private const string RootClassName = "ee4v-ui-searchable-tree-view";
        private const string SearchClassName = "ee4v-ui-searchable-tree-view__search";
        private const string TreeClassName = "ee4v-ui-searchable-tree-view__tree";
        private const string EmptyClassName = "ee4v-ui-searchable-tree-view__empty";
        private readonly SearchField _searchField;
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
            string emptyText = "",
            string searchPlaceholder = null)
        {
            if (makeItem == null)
            {
                throw new ArgumentNullException(nameof(makeItem));
            }

            _bindItem = bindItem ?? throw new ArgumentNullException(nameof(bindItem));
            _onSelectionChanged = onSelectionChanged;

            AddToClassList(RootClassName);

            _searchField = new SearchField();
            _searchField.AddToClassList(SearchClassName);
            _searchField.ValueChanged += value =>
            {
                if (_onSearchValueChanged != null)
                {
                    _onSearchValueChanged(value ?? string.Empty);
                }
            };
            Add(_searchField);

            _treeView = new TreeView();
            _treeView.AddToClassList(TreeClassName);
            _treeView.selectionType = SelectionType.Single;
            _treeView.fixedItemHeight = 20;
            _treeView.makeItem = makeItem;
            _treeView.bindItem = BindItem;
            _treeView.selectionChanged += OnSelectionChanged;
            Add(_treeView);

            _emptyLabel = UiTextFactory.Create(emptyText, EmptyClassName);
            _emptyLabel.SetWhiteSpace(WhiteSpace.Normal);
            Add(_emptyLabel);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            SetEmptyText(emptyText);
            SetSearchState(string.Empty, searchPlaceholder, _ => RefreshTree());
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
            element.tooltip = item.TooltipText;
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
            var filteredItems = FilterItems(_sourceItems, _searchField.Value);
            _treeView.SetRootItems(filteredItems);
            _treeView.Rebuild();
            if (!string.IsNullOrWhiteSpace(_searchField.Value))
            {
                _treeView.ExpandAll();
            }
            else
            {
                _treeView.CollapseAll();
            }

            var hasItems = filteredItems.Count > 0;
            _treeView.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
            HideScrollbars();
        }

        private void SetSearchState(string value, string placeholder = null, Action<string> onValueChanged = null)
        {
            _onSearchValueChanged = onValueChanged;
            _searchField.SetState(new SearchFieldState(value, placeholder ?? I18N.Get("ui.search.placeholder")));
        }

        private void ClearSearch()
        {
            if (string.IsNullOrEmpty(_searchField.Value))
            {
                return;
            }

            _searchField.ClearValue();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            HideScrollbars();
            schedule.Execute(HideScrollbars);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            HideScrollbars();
        }

        private void HideScrollbars()
        {
            var scrollers = _treeView.Query<Scroller>().ToList();
            for (var i = 0; i < scrollers.Count; i++)
            {
                scrollers[i].style.display = DisplayStyle.None;
                scrollers[i].style.visibility = Visibility.Hidden;
            }

            var scrollViews = _treeView.Query<ScrollView>().ToList();
            for (var i = 0; i < scrollViews.Count; i++)
            {
                scrollViews[i].verticalScrollerVisibility = ScrollerVisibility.Hidden;
                scrollViews[i].horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
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
