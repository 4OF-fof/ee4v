using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectTreeItemViewState
    {
        public HiddenObjectTreeItemViewState(
            long key,
            bool isScene,
            int instanceId,
            string name,
            string meta,
            bool isHidden,
            bool isSelected,
            IconState icon)
        {
            Key = key;
            IsScene = isScene;
            InstanceId = instanceId;
            Name = name ?? string.Empty;
            Meta = meta ?? string.Empty;
            IsHidden = isHidden;
            IsSelected = isSelected;
            Icon = icon;
        }

        public long Key { get; }

        public bool IsScene { get; }

        public int InstanceId { get; }

        public string Name { get; }

        public string Meta { get; }

        public bool IsHidden { get; }

        public bool IsSelected { get; }

        public IconState Icon { get; }
    }

    internal sealed class HiddenObjectTreeRow : VisualElement
    {
        private const string RootClassName =
            "ee4v-hidden-object-tree-row";
        private const string SceneClassName =
            "ee4v-hidden-object-tree-row--scene";
        private const string AncestorClassName =
            "ee4v-hidden-object-tree-row--ancestor";
        private const string SelectionClassName =
            "ee4v-hidden-object-tree-row__selection";
        private const string IconClassName =
            "ee4v-hidden-object-tree-row__icon";
        private const string NameClassName =
            "ee4v-hidden-object-tree-row__name";
        private const string MetaClassName =
            "ee4v-hidden-object-tree-row__meta";

        private readonly Toggle _selection;
        private readonly Icon _icon;
        private readonly UiTextElement _name;
        private readonly UiTextElement _meta;
        private HiddenObjectTreeItemViewState _state;

        public HiddenObjectTreeRow()
        {
            AddToClassList(RootClassName);

            _selection = new Toggle();
            _selection.AddToClassList(SelectionClassName);
            _selection.RegisterValueChangedCallback(evt =>
            {
                if (_state != null &&
                    !_state.IsScene &&
                    _state.IsHidden)
                {
                    SelectionChanged?.Invoke(
                        _state.InstanceId,
                        evt.newValue);
                }
            });

            _icon = new Icon(IconState.FromBuiltinIcon(
                UiBuiltinIcon.GenericFile,
                UiSizeTokens.Size16));
            _icon.AddToClassList(IconClassName);
            _name = UiTextFactory.Create(string.Empty, NameClassName);
            _meta = UiTextFactory.Create(
                string.Empty,
                MetaClassName,
                UiClassNames.SecondaryText);

            Add(_selection);
            Add(_icon);
            Add(_name);
            Add(_meta);

            RegisterCallback<ClickEvent>(OnClicked);
        }

        public event Action<int, bool> SelectionChanged;

        public event Action<int> FocusRequested;

        public void SetState(HiddenObjectTreeItemViewState state)
        {
            _state = state;
            if (_state == null)
            {
                return;
            }

            EnableInClassList(SceneClassName, _state.IsScene);
            EnableInClassList(
                AncestorClassName,
                !_state.IsScene && !_state.IsHidden);
            _selection.style.display =
                !_state.IsScene && _state.IsHidden
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _selection.SetValueWithoutNotify(_state.IsSelected);
            _icon.SetState(_state.Icon ?? IconState.FromBuiltinIcon(
                UiBuiltinIcon.GenericFile,
                UiSizeTokens.Size16));
            _name.SetText(_state.Name);
            _meta.SetText(_state.Meta);
            _meta.style.display = string.IsNullOrWhiteSpace(_state.Meta)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void OnClicked(ClickEvent evt)
        {
            if (_state == null ||
                _state.IsScene ||
                evt.target is VisualElement target &&
                _selection.Contains(target))
            {
                return;
            }

            FocusRequested?.Invoke(_state.InstanceId);
        }
    }

    internal sealed class HiddenObjectTreeView : VisualElement
    {
        private const string RootClassName =
            "ee4v-hidden-object-tree";
        private const string TreeClassName =
            "ee4v-hidden-object-tree__view";
        private const string EmptyClassName =
            "ee4v-hidden-object-tree__empty";
        private const string EmptyIconClassName =
            "ee4v-hidden-object-tree__empty-icon";
        private const string EmptyTitleClassName =
            "ee4v-hidden-object-tree__empty-title";
        private const string EmptyMessageClassName =
            "ee4v-hidden-object-tree__empty-message";

        private readonly TreeView _treeView;
        private readonly VisualElement _empty;
        private readonly UiTextElement _emptyTitle;
        private readonly UiTextElement _emptyMessage;
        private readonly Dictionary<long, int> _itemIdByKey =
            new Dictionary<long, int>();
        private readonly Dictionary<long, int> _branchIdByKey =
            new Dictionary<long, int>();
        private readonly HashSet<long> _collapsedKeys =
            new HashSet<long>();
        private string _query = string.Empty;
        private int _nextItemId = 1;
        private bool _hasState;

        public HiddenObjectTreeView()
        {
            AddToClassList(RootClassName);

            _treeView = new TreeView
            {
                fixedItemHeight = UiSizeTokens.ControlHeightSmall,
                selectionType = SelectionType.None,
                makeItem = CreateRow,
                bindItem = BindRow,
                viewDataKey = "ee4v-hidden-objects-tree"
            };
            _treeView.AddToClassList(TreeClassName);

            _empty = new VisualElement();
            _empty.AddToClassList(EmptyClassName);
            var emptyIcon = new Icon(IconState.FromBuiltinIcon(
                UiBuiltinIcon.VisibilityHidden,
                UiSizeTokens.Size24));
            emptyIcon.AddToClassList(EmptyIconClassName);
            _emptyTitle = UiTextFactory.Create(
                string.Empty,
                EmptyTitleClassName,
                UiClassNames.SectionTitle);
            _emptyMessage = UiTextFactory.Create(
                string.Empty,
                EmptyMessageClassName,
                UiClassNames.SecondaryText);
            _emptyMessage.SetWhiteSpace(WhiteSpace.Normal);
            _empty.Add(emptyIcon);
            _empty.Add(_emptyTitle);
            _empty.Add(_emptyMessage);

            Add(_treeView);
            Add(_empty);
        }

        public event Action<int, bool> SelectionChanged;

        public event Action<int> FocusRequested;

        public void SetState(HiddenObjectsViewState state)
        {
            state = state ?? new HiddenObjectsViewState(
                null,
                null,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0);

            CaptureCollapsedState();
            _branchIdByKey.Clear();
            var rootItems = BuildSceneItems(state.SceneGroups);
            _treeView.SetRootItems(rootItems);
            _treeView.Rebuild();

            var hasItems = rootItems.Count > 0;
            _treeView.style.display =
                hasItems ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.style.display =
                hasItems ? DisplayStyle.None : DisplayStyle.Flex;
            _emptyTitle.SetText(state.EmptyTitle);
            _emptyMessage.SetText(state.EmptyMessage);

            if (hasItems)
            {
                RestoreExpansion(state.Query);
            }

            _query = state.Query;
            _hasState = true;
        }

        private VisualElement CreateRow()
        {
            var row = new HiddenObjectTreeRow();
            row.SelectionChanged += (instanceId, selected) =>
                SelectionChanged?.Invoke(instanceId, selected);
            row.FocusRequested += instanceId =>
                FocusRequested?.Invoke(instanceId);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            var row = element as HiddenObjectTreeRow;
            if (row == null)
            {
                return;
            }

            row.SetState(
                _treeView.GetItemDataForIndex<
                    HiddenObjectTreeItemViewState>(index));
        }

        private List<TreeViewItemData<HiddenObjectTreeItemViewState>>
            BuildSceneItems(
                IReadOnlyList<HiddenObjectSceneGroupViewState> groups)
        {
            var items =
                new List<TreeViewItemData<HiddenObjectTreeItemViewState>>();
            if (groups == null)
            {
                return items;
            }

            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var key = CreateKey(true, group.SceneHandle);
                var itemId = GetItemId(key);
                var children = BuildNodeItems(group.Roots);
                _branchIdByKey[key] = itemId;
                var data = new HiddenObjectTreeItemViewState(
                    key,
                    true,
                    0,
                    group.SceneName,
                    group.HiddenCountText,
                    false,
                    false,
                    IconState.FromBuiltinIcon(
                        UiBuiltinIcon.UnityFile,
                        UiSizeTokens.Size16));
                items.Add(
                    new TreeViewItemData<HiddenObjectTreeItemViewState>(
                        itemId,
                        data,
                        children));
            }

            return items;
        }

        private List<TreeViewItemData<HiddenObjectTreeItemViewState>>
            BuildNodeItems(
                IReadOnlyList<HiddenObjectNodeViewState> nodes)
        {
            var items =
                new List<TreeViewItemData<HiddenObjectTreeItemViewState>>();
            if (nodes == null)
            {
                return items;
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var key = CreateKey(false, node.InstanceId);
                var itemId = GetItemId(key);
                var children = BuildNodeItems(node.Children);
                if (children.Count > 0)
                {
                    _branchIdByKey[key] = itemId;
                }

                var data = new HiddenObjectTreeItemViewState(
                    key,
                    false,
                    node.InstanceId,
                    node.Name,
                    string.Empty,
                    node.IsHidden,
                    node.IsSelected,
                    node.Icon);
                items.Add(
                    new TreeViewItemData<HiddenObjectTreeItemViewState>(
                        itemId,
                        data,
                        children));
            }

            return items;
        }

        private void CaptureCollapsedState()
        {
            if (!_hasState ||
                !string.IsNullOrWhiteSpace(_query))
            {
                return;
            }

            foreach (var branch in _branchIdByKey)
            {
                if (_treeView.IsExpanded(branch.Value))
                {
                    _collapsedKeys.Remove(branch.Key);
                }
                else
                {
                    _collapsedKeys.Add(branch.Key);
                }
            }
        }

        private void RestoreExpansion(string query)
        {
            _treeView.ExpandAll();
            if (!string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            foreach (var key in _collapsedKeys)
            {
                if (_branchIdByKey.TryGetValue(key, out var itemId))
                {
                    _treeView.CollapseItem(itemId);
                }
            }
        }

        private int GetItemId(long key)
        {
            if (_itemIdByKey.TryGetValue(key, out var itemId))
            {
                return itemId;
            }

            itemId = _nextItemId++;
            _itemIdByKey[key] = itemId;
            return itemId;
        }

        private static long CreateKey(bool isScene, int sourceId)
        {
            return ((long)(isScene ? 1 : 0) << 32) |
                   (uint)sourceId;
        }
    }
}
