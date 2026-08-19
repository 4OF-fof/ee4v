using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetCollectionCreationPopup : EditorWindow
    {
        private static readonly Vector2 PopupSize = new Vector2(620f, 520f);

        private Func<string, AssetFilterNode, bool> _save;
        private AssetCollection _initialCollection;
        private AssetManagerTextField _name;
        private FilterGroupEditor _rootGroup;
        private UiTextElement _error;

        public static void Show(
            VisualElement anchor,
            AssetCollection initialCollection,
            Func<string, AssetFilterNode, bool> save)
        {
            if (anchor == null || save == null)
            {
                return;
            }

            anchor.Blur();
            var window = CreateInstance<AssetCollectionCreationPopup>();
            window._initialCollection = initialCollection;
            window._save = save;
            window.minSize = PopupSize;
            window.maxSize = PopupSize;
            window.ShowAsDropDown(
                ResolveElementAnchor(anchor),
                PopupSize);
            window.Focus();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            AssetManagerWindowSession.PrepareRoot(root);
            root.AddToClassList("ee4v-asset-manager");
            root.AddToClassList(UiClassNames.PopupSurface);
            root.AddToClassList("ee4v-asset-manager__collection-popup");
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            var form = new ScrollView(ScrollViewMode.Vertical);
            form.AddToClassList(
                "ee4v-asset-manager__collection-popup-form");
            form.Add(UiTextFactory.Create(
                _initialCollection == null
                    ? I18N.Get("common.newCollection")
                    : _initialCollection.Name,
                UiClassNames.SectionTitle,
                "ee4v-asset-manager__collection-popup-title"));

            _name = AssetManagerControls.CreateTextField(
                I18N.Get("field.name"));
            _name.value = _initialCollection?.Name ?? string.Empty;
            form.Add(_name);

            form.Add(UiTextFactory.Create(
                I18N.Get("filterEditor.conditions"),
                UiClassNames.FormLabel,
                "ee4v-asset-manager__collection-popup-conditions-title"));
            _rootGroup = CreateRootEditor(_initialCollection?.Root);
            form.Add(_rootGroup.Root);

            _error = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormError,
                "ee4v-asset-manager__collection-popup-error");
            _error.SetWhiteSpace(WhiteSpace.Normal);
            form.Add(_error);
            root.Add(form);

            var actions = new VisualElement();
            actions.AddToClassList(
                "ee4v-asset-manager__collection-popup-actions");
            actions.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.cancel"),
                Close));
            actions.Add(AssetManagerControls.CreateButton(
                I18N.Get(
                    _initialCollection == null
                        ? "action.createCollection"
                        : "action.saveCollection"),
                Submit,
                "ee4v-asset-manager__primary-action"));
            root.Add(actions);
            root.schedule.Execute(_name.FocusInput);
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

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Return ||
                     evt.keyCode == KeyCode.KeypadEnter)
            {
                Submit();
                evt.StopPropagation();
            }
        }

        private void Submit()
        {
            var name = (_name.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetError(I18N.Get("notice.collectionNameRequired"));
                return;
            }

            if (!_rootGroup.TryCreateNode(
                    out var root,
                    out var errorKey))
            {
                SetError(I18N.Get(errorKey));
                return;
            }
            if (_save(name, root))
            {
                Close();
                return;
            }

            SetError(I18N.Get("notice.collectionSaveFailed"));
        }

        private void SetError(string message)
        {
            _error.SetText(message ?? string.Empty);
        }

        private static string FormatMatchMode(Enum value)
        {
            return I18N.Get(
                value is FilterMatchMode mode &&
                mode == FilterMatchMode.Any
                    ? "filterEditor.matchAny"
                    : "filterEditor.matchAll");
        }

        private static Rect ResolveElementAnchor(VisualElement anchor)
        {
            if (anchor == null || anchor.panel == null)
            {
                var point = GUIUtility.GUIToScreenPoint(Vector2.zero);
                return new Rect(point, Vector2.zero);
            }

            var root = anchor.panel.visualTree;
            var rootOffset = root != null
                ? root.worldBound.position
                : Vector2.zero;
            var localPosition = anchor.worldBound.position - rootOffset;
            var owner = FindOwnerWindow(anchor);
            var screenPosition = owner != null
                ? owner.position.position + localPosition
                : GUIUtility.GUIToScreenPoint(localPosition);
            return new Rect(screenPosition, anchor.worldBound.size);
        }

        private static EditorWindow FindOwnerWindow(VisualElement target)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window != null &&
                    window.rootVisualElement != null &&
                    window.rootVisualElement.panel == target.panel)
                {
                    return window;
                }
            }

            return EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
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

            public FilterGroupEditor(
                AssetFilterNodeType type,
                IReadOnlyList<AssetFilterNode> initialChildren,
                bool inverted,
                Action<IFilterNodeEditor> remove,
                bool isRoot = false)
            {
                Root = new VisualElement();
                Root.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group");
                if (isRoot)
                {
                    Root.AddToClassList(
                        "ee4v-asset-manager__collection-popup-group--root");
                }

                var header = new VisualElement();
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
                header.Add(_matchMode);
                header.Add(_inverted);
                if (!isRoot)
                {
                    header.Add(AssetManagerControls.CreateIconButton(
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

                var actions = new VisualElement();
                actions.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group-actions");
                actions.Add(AssetManagerControls.CreateIconTextButton(
                    I18N.Get("filterEditor.addCondition"),
                    "add.png",
                    AddCondition));
                actions.Add(AssetManagerControls.CreateIconTextButton(
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
                AddEditor(new ConditionRow(
                    null,
                    false,
                    RemoveChild));
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

            public ConditionRow(
                AssetFilterNode initialCondition,
                bool inverted,
                Action<IFilterNodeEditor> remove)
            {
                Root = new VisualElement();
                Root.AddToClassList(
                    "ee4v-asset-manager__collection-popup-condition");

                var controls = new VisualElement();
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
                controls.Add(_condition);
                controls.Add(_inverted);
                controls.Add(AssetManagerControls.CreateIconButton(
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
