using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEngine;
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
        private readonly Action<VisualElement, TData, IReadOnlyList<TData>, Vector2> _onContextClick;
        private IReadOnlyList<SearchableTreeItemData<TData>> _sourceItems;
        private IReadOnlyList<SearchableTreeItemData<TData>> _selectedTreeItems = Array.Empty<SearchableTreeItemData<TData>>();
        private Action<string> _onSearchValueChanged;

        public SearchableTreeView(
            Func<VisualElement> makeItem,
            Action<VisualElement, TData> bindItem,
            Action<IReadOnlyList<TData>> onSelectionChanged = null,
            string emptyText = "",
            string searchPlaceholder = null,
            SelectionType selectionType = SelectionType.Single,
            Action<VisualElement, TData, IReadOnlyList<TData>, Vector2> onContextClick = null)
        {
            if (makeItem == null)
            {
                throw new ArgumentNullException(nameof(makeItem));
            }

            _bindItem = bindItem ?? throw new ArgumentNullException(nameof(bindItem));
            _onSelectionChanged = onSelectionChanged;
            _onContextClick = onContextClick;

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
            _treeView.selectionType = selectionType;
            _treeView.fixedItemHeight = 20;
            _treeView.makeItem = () => CreateItemElement(makeItem);
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
            SetItems(items, preserveExpansion: false);
        }

        public void SetItems(IReadOnlyList<SearchableTreeItemData<TData>> items, bool preserveExpansion)
        {
            _sourceItems = items ?? new SearchableTreeItemData<TData>[0];
            _selectedTreeItems = Array.Empty<SearchableTreeItemData<TData>>();
            RefreshTree(preserveExpansion);
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
            _selectedTreeItems = Array.Empty<SearchableTreeItemData<TData>>();
        }

        public void SetViewDataKey(string viewDataKey)
        {
            _treeView.viewDataKey = viewDataKey ?? string.Empty;
        }

        private void BindItem(VisualElement element, int index)
        {
            var item = _treeView.GetItemDataForIndex<SearchableTreeItemData<TData>>(index);
            element.userData = item;
            element.tooltip = item.TooltipText;
            _bindItem(element, item.Data);
        }

        private void OnSelectionChanged(IEnumerable<object> items)
        {
            if (items == null)
            {
                _selectedTreeItems = Array.Empty<SearchableTreeItemData<TData>>();
                _onSelectionChanged?.Invoke(Array.Empty<TData>());
                return;
            }

            var selected = new List<TData>();
            var selectedTreeItems = new List<SearchableTreeItemData<TData>>();
            foreach (var item in items)
            {
                if (item is SearchableTreeItemData<TData> treeItem)
                {
                    selectedTreeItems.Add(treeItem);
                    selected.Add(treeItem.Data);
                }
            }

            _selectedTreeItems = selectedTreeItems;
            _onSelectionChanged?.Invoke(selected);
        }

        private VisualElement CreateItemElement(Func<VisualElement> makeItem)
        {
            var element = makeItem();
            element.RegisterCallback<PointerUpEvent>(OnItemPointerUp);
            return element;
        }

        private void OnItemPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 1 || _onContextClick == null)
            {
                return;
            }

            var element = evt.currentTarget as VisualElement;
            var item = element != null ? element.userData as SearchableTreeItemData<TData> : null;
            if (element == null || item == null)
            {
                return;
            }

            var selected = ResolveContextSelection(item);
            evt.StopPropagation();
            _onContextClick(element, item.Data, selected, element.LocalToWorld(evt.localPosition));
        }

        private IReadOnlyList<TData> ResolveContextSelection(SearchableTreeItemData<TData> item)
        {
            if (_selectedTreeItems == null || _selectedTreeItems.Count == 0 || !ContainsSelectedItem(item))
            {
                _treeView.SetSelectionById(new[] { item.Id });
                _selectedTreeItems = new[] { item };
            }

            var selected = new List<TData>(_selectedTreeItems.Count);
            for (var i = 0; i < _selectedTreeItems.Count; i++)
            {
                selected.Add(_selectedTreeItems[i].Data);
            }

            return selected;
        }

        private bool ContainsSelectedItem(SearchableTreeItemData<TData> item)
        {
            for (var i = 0; i < _selectedTreeItems.Count; i++)
            {
                if (_selectedTreeItems[i].Id == item.Id)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshTree()
        {
            RefreshTree(preserveExpansion: false);
        }

        private void RefreshTree(bool preserveExpansion)
        {
            var filteredItems = FilterItems(_sourceItems, _searchField.Value);
            _treeView.SetRootItems(filteredItems);
            _treeView.Rebuild();
            if (!string.IsNullOrWhiteSpace(_searchField.Value))
            {
                _treeView.ExpandAll();
            }
            else if (preserveExpansion)
            {
                // Leave the TreeView expansion state untouched while rebinding item data.
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
