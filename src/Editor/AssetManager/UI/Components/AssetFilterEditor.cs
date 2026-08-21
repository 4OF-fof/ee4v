using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetFilterEditor : VisualElement
    {
        private readonly FilterGroupEditor _rootGroup;

        internal AssetFilterEditor(AssetFilterNode root)
        {
            AddToClassList("ee4v-asset-manager-filter-editor");
            _rootGroup = CreateRootEditor(root);
            Add(_rootGroup.Root);
        }

        internal bool TryCreateNode(
            out AssetFilterNode node,
            out string errorKey)
        {
            return _rootGroup.TryCreateNode(out node, out errorKey);
        }

        private static FilterGroupEditor CreateRootEditor(
            AssetFilterNode root)
        {
            var inverted = false;
            root = UnwrapNot(root, ref inverted);
            if (root != null &&
                (root.Type == AssetFilterNodeType.And ||
                 root.Type == AssetFilterNodeType.Or))
            {
                return new FilterGroupEditor(
                    root.Type,
                    root.Children,
                    inverted,
                    null,
                    true);
            }

            return new FilterGroupEditor(
                AssetFilterNodeType.And,
                root == null ? null : new[] { root },
                inverted,
                null,
                true);
        }

        private static IFilterNodeEditor CreateNodeEditor(
            AssetFilterNode node,
            Action<IFilterNodeEditor> remove)
        {
            var inverted = false;
            node = UnwrapNot(node, ref inverted);
            if (node != null &&
                (node.Type == AssetFilterNodeType.And ||
                 node.Type == AssetFilterNodeType.Or))
            {
                return new FilterGroupEditor(
                    node.Type,
                    node.Children,
                    inverted,
                    remove);
            }

            return new ConditionRow(
                node != null &&
                node.Type == AssetFilterNodeType.Condition
                    ? node
                    : null,
                inverted,
                remove);
        }

        private static AssetFilterNode UnwrapNot(
            AssetFilterNode node,
            ref bool inverted)
        {
            while (node != null &&
                   node.Type == AssetFilterNodeType.Not &&
                   node.Children != null &&
                   node.Children.Count == 1)
            {
                inverted = !inverted;
                node = node.Children[0];
            }

            return node;
        }

        private static string FormatMatchMode(Enum value)
        {
            return I18N.Get(
                value is FilterMatchMode mode &&
                mode == FilterMatchMode.Any
                    ? "filterEditor.matchAny"
                    : "filterEditor.matchAll");
        }

        private enum FilterMatchMode
        {
            All,
            Any
        }

        private interface IFilterNodeEditor
        {
            VisualElement Root { get; }

            bool TryCreateNode(
                out AssetFilterNode node,
                out string errorKey);
        }

        private sealed class FilterGroupEditor : IFilterNodeEditor
        {
            private readonly List<IFilterNodeEditor> _children =
                new List<IFilterNodeEditor>();
            private readonly AssetManagerEnumField _matchMode;
            private readonly Toggle _inverted;
            private readonly VisualElement _childrenRoot;

            internal FilterGroupEditor(
                AssetFilterNodeType type,
                IReadOnlyList<AssetFilterNode> initialChildren,
                bool inverted,
                Action<IFilterNodeEditor> remove,
                bool isRoot = false)
            {
                Root = new VisualElement();
                Root.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group");
                Root.EnableInClassList(
                    "ee4v-asset-manager__collection-popup-group--root",
                    isRoot);

                var header = new ActionBar();
                header.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group-header");
                _matchMode = AssetManagerControls.CreateEnumField(
                    I18N.Get("filterEditor.matchMode"),
                    type == AssetFilterNodeType.Or
                        ? FilterMatchMode.Any
                        : FilterMatchMode.All,
                    FormatMatchMode,
                    "ee4v-asset-manager__collection-popup-group-mode");
                _inverted = UiTextFactory.CreateToggle(
                    I18N.Get("filterEditor.invertGroup"),
                    "ee4v-asset-manager__collection-popup-group-inverted");
                _inverted.value = inverted;
                header.Leading.Add(_matchMode);
                header.Leading.Add(_inverted);
                if (!isRoot)
                {
                    header.Actions.Add(
                        AssetManagerControls.CreateIconButton(
                            I18N.Get("filterEditor.removeGroup"),
                            "dismiss.png",
                            () => remove?.Invoke(this),
                            "ee4v-asset-manager__collection-popup-remove-group"));
                }
                Root.Add(header);

                _childrenRoot = new VisualElement();
                _childrenRoot.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group-children");
                Root.Add(_childrenRoot);

                var actions = new ActionBar();
                actions.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group-actions");
                actions.Actions.Add(
                    AssetManagerControls.CreateIconTextButton(
                        I18N.Get("filterEditor.addCondition"),
                        "add.png",
                        AddCondition));
                actions.Actions.Add(
                    AssetManagerControls.CreateIconTextButton(
                        I18N.Get("filterEditor.addGroup"),
                        "add.png",
                        AddGroup));
                Root.Add(actions);

                var children = initialChildren ??
                               Array.Empty<AssetFilterNode>();
                for (var i = 0; i < children.Count; i++)
                {
                    AddEditor(CreateNodeEditor(
                        children[i],
                        RemoveChild));
                }

                if (_children.Count == 0)
                {
                    AddCondition();
                }
            }

            public VisualElement Root { get; }

            public bool TryCreateNode(
                out AssetFilterNode node,
                out string errorKey)
            {
                if (_children.Count == 0)
                {
                    node = null;
                    errorKey = "filterEditor.groupConditionRequired";
                    return false;
                }

                var nodes = new AssetFilterNode[_children.Count];
                for (var i = 0; i < _children.Count; i++)
                {
                    if (!_children[i].TryCreateNode(
                            out nodes[i],
                            out errorKey))
                    {
                        node = null;
                        return false;
                    }
                }

                node = nodes.Length == 1
                    ? nodes[0]
                    : (FilterMatchMode)_matchMode.value ==
                      FilterMatchMode.Any
                        ? AssetFilterNode.Or(nodes)
                        : AssetFilterNode.And(nodes);
                if (_inverted.value)
                {
                    node = AssetFilterNode.Not(node);
                }

                errorKey = null;
                return true;
            }

            private void AddCondition()
            {
                AddEditor(new ConditionRow(null, false, RemoveChild));
            }

            private void AddGroup()
            {
                AddEditor(new FilterGroupEditor(
                    AssetFilterNodeType.And,
                    null,
                    false,
                    RemoveChild));
            }

            private void AddEditor(IFilterNodeEditor editor)
            {
                if (editor == null)
                {
                    return;
                }

                _children.Add(editor);
                _childrenRoot.Add(editor.Root);
            }

            private void RemoveChild(IFilterNodeEditor editor)
            {
                if (editor == null || !_children.Remove(editor))
                {
                    return;
                }

                editor.Root.RemoveFromHierarchy();
            }
        }

        private sealed class ConditionRow : IFilterNodeEditor
        {
            private readonly AssetManagerEnumField _condition;
            private readonly AssetManagerTextField _value;
            private readonly Toggle _inverted;

            internal ConditionRow(
                AssetFilterNode initialCondition,
                bool inverted,
                Action<IFilterNodeEditor> remove)
            {
                Root = new VisualElement();
                Root.AddToClassList(
                    "ee4v-asset-manager__collection-popup-condition");

                var controls = new ActionBar();
                controls.AddToClassList(
                    "ee4v-asset-manager__collection-popup-condition-controls");
                _condition = AssetManagerControls.CreateEnumField(
                    string.Empty,
                    initialCondition?.ConditionType ??
                    AssetFilterConditionType.NameContains,
                    AssetManagerControls.FormatFilterCondition,
                    "ee4v-asset-manager__collection-popup-condition-type");
                _inverted = UiTextFactory.CreateToggle(
                    I18N.Get("filterEditor.inverted"),
                    "ee4v-asset-manager__collection-popup-condition-inverted");
                _inverted.value = inverted;
                controls.Leading.Add(_condition);
                controls.Leading.Add(_inverted);
                controls.Actions.Add(
                    AssetManagerControls.CreateIconButton(
                        I18N.Get("filterEditor.removeCondition"),
                        "dismiss.png",
                        () => remove?.Invoke(this),
                        "ee4v-asset-manager__collection-popup-remove-condition"));
                Root.Add(controls);

                _value = AssetManagerControls.CreateTextField(
                    I18N.Get("field.value"),
                    "ee4v-asset-manager__collection-popup-condition-value");
                _value.value = initialCondition?.Value ?? string.Empty;
                Root.Add(_value);
            }

            public VisualElement Root { get; }

            public bool TryCreateNode(
                out AssetFilterNode node,
                out string errorKey)
            {
                var value = (_value.value ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(value))
                {
                    node = null;
                    errorKey = "filterEditor.valueRequired";
                    return false;
                }

                node = AssetFilterNode.Condition(
                    (AssetFilterConditionType)_condition.value,
                    value);
                if (_inverted.value)
                {
                    node = AssetFilterNode.Not(node);
                }

                errorKey = null;
                return true;
            }
        }
    }
}
