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
        private readonly IReadOnlyList<TagOption> _tagOptions;
        private TagPickerWindow _tagPicker;
        private VisualElement _tagPickerAnchor;

        internal AssetFilterEditor(AssetFilterNode root, bool allowEmptyRoot = false,
            IReadOnlyList<TagOption> tagOptions = null)
        {
            _tagOptions = tagOptions ?? Array.Empty<TagOption>();
            AddToClassList("ee4v-asset-manager-filter-editor");
            _rootGroup = CreateRootEditor(root, allowEmptyRoot);
            Add(_rootGroup.Root);
            RegisterCallback<DetachFromPanelEvent>(_ => CloseTagPicker());
        }

        internal bool IsTagPickerOpen { get; private set; }
        internal event Action TagPickerClosed;

        internal void CloseTagPicker() => _tagPicker?.Close();

        private void CloseTagPicker(VisualElement anchor)
        {
            if (_tagPickerAnchor == anchor) { CloseTagPicker(); }
        }

        private void OpenTagPicker(VisualElement anchor, string value, Action<string> selected)
        {
            CloseTagPicker();
            // Set before Show transfers focus away from the containing popup.
            IsTagPickerOpen = true;
            _tagPickerAnchor = anchor;
            _tagPicker = TagPickerWindow.Show(anchor, null, _tagOptions,
                string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value },
                values => selected(values[0]),
                () =>
                {
                    IsTagPickerOpen = false;
                    _tagPicker = null;
                    _tagPickerAnchor = null;
                    TagPickerClosed?.Invoke();
                }, singleSelection: true);
        }

        internal bool TryCreateNode(
            out AssetFilterNode node,
            out string errorKey)
        {
            return _rootGroup.TryCreateNode(out node, out errorKey);
        }

        private FilterGroupEditor CreateRootEditor(
            AssetFilterNode root, bool allowEmptyRoot)
        {
            var inverted = false;
            root = UnwrapNot(root, ref inverted);
            if (root != null &&
                (root.Type == AssetFilterNodeType.And ||
                 root.Type == AssetFilterNodeType.Or))
            {
                return new FilterGroupEditor(
                    this,
                    root.Type,
                    root.Children,
                    inverted,
                    null,
                    true, allowEmptyRoot);
            }

            return new FilterGroupEditor(
                this,
                AssetFilterNodeType.And,
                root == null ? null : new[] { root },
                inverted,
                null,
                true, allowEmptyRoot);
        }

        private IFilterNodeEditor CreateNodeEditor(
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
                    this,
                    node.Type,
                    node.Children,
                    inverted,
                    remove);
            }

            return new ConditionRow(
                this,
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

        private static Toggle CreateInvertToggle(string className)
        {
            var text = I18N.Get("filterEditor.inverted");
            var toggle = UiTextFactory.CreateToggle(string.Empty, className);
            toggle.tooltip = text;
            var label = UiTextFactory.Create(text,
                UiClassNames.FormLabel,
                "ee4v-asset-manager-filter-editor__invert-label");
            label.SetWhiteSpace(WhiteSpace.NoWrap);
            label.pickingMode = PickingMode.Ignore;
            toggle.Q<VisualElement>(className: Toggle.inputUssClassName).Add(label);
            return toggle;
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
            private readonly AssetFilterEditor _owner;
            private readonly List<IFilterNodeEditor> _children =
                new List<IFilterNodeEditor>();
            private FilterMatchMode _matchMode;
            private readonly UiButton _matchAll;
            private readonly UiButton _matchAny;
            private readonly Toggle _inverted;
            private readonly VisualElement _childrenRoot;
            private readonly bool _allowEmpty;
            private readonly VisualElement _header;
            private readonly UiTextElement _empty;

            internal FilterGroupEditor(
                AssetFilterEditor owner,
                AssetFilterNodeType type,
                IReadOnlyList<AssetFilterNode> initialChildren,
                bool inverted,
                Action<IFilterNodeEditor> remove,
                bool isRoot = false, bool allowEmpty = false)
            {
                _owner = owner;
                _allowEmpty = isRoot && allowEmpty;
                Root = new VisualElement();
                Root.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group");
                Root.EnableInClassList(
                    "ee4v-asset-manager__collection-popup-group--root",
                    isRoot);
                Root.EnableInClassList(
                    "ee4v-asset-manager__collection-popup-group--nested",
                    !isRoot);

                var header = new ActionBar();
                _header = header;
                header.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group-header");
                header.Leading.Add(UiTextFactory.Create(
                    I18N.Get(isRoot ? "filterEditor.matchMode" : "filterEditor.group"),
                    UiClassNames.SectionTitle,
                    "ee4v-asset-manager__collection-popup-group-title"));
                var matchModes = new VisualElement();
                matchModes.AddToClassList("ee4v-asset-manager-filter-editor__match-modes");
                _matchAll = new UiButton(I18N.Get("filterEditor.matchAll"),
                    () => SetMatchMode(FilterMatchMode.All), variant: UiButtonVariant.Ghost);
                _matchAny = new UiButton(I18N.Get("filterEditor.matchAny"),
                    () => SetMatchMode(FilterMatchMode.Any), variant: UiButtonVariant.Ghost);
                matchModes.Add(_matchAll);
                matchModes.Add(_matchAny);
                header.Leading.Add(matchModes);
                SetMatchMode(type == AssetFilterNodeType.Or ? FilterMatchMode.Any : FilterMatchMode.All);
                _inverted = CreateInvertToggle(
                    "ee4v-asset-manager__collection-popup-group-inverted");
                _inverted.value = inverted;
                _inverted.RegisterValueChangedCallback(_ => RefreshMatchLabels());
                RefreshMatchLabels();
                header.Leading.Add(_inverted);
                if (!isRoot)
                {
                    header.Actions.Add(
                        AssetManagerControls.CreateIconButton(
                            I18N.Get("filterEditor.removeGroup"),
                            "dismiss.png",
                            UiSizeTokens.Size12,
                            UiButtonVariant.Ghost,
                            () => remove?.Invoke(this),
                            "ee4v-asset-manager__collection-popup-remove-group"));
                }
                Root.Add(header);

                _childrenRoot = new VisualElement();
                _childrenRoot.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group-children");
                Root.Add(_childrenRoot);

                _empty = UiTextFactory.Create(I18N.Get("filterEditor.empty"),
                    UiClassNames.FormLabel,
                    "ee4v-asset-manager-filter-editor__empty");
                _empty.SetTextAlign(UnityEngine.TextAnchor.MiddleCenter);
                Root.Add(_empty);
                var actions = new VisualElement();
                actions.AddToClassList(
                    "ee4v-asset-manager__collection-popup-group-actions");
                actions.Add(UiTextFactory.Create(I18N.Get("filterEditor.addCondition"),
                    UiClassNames.FormLabel,
                    "ee4v-asset-manager-filter-editor__add-label"));
                foreach (AssetFilterConditionType conditionType in Enum.GetValues(typeof(AssetFilterConditionType)))
                {
                    var choice = conditionType;
                    var button = new UiButton(I18N.Get("filterEditor.add." + choice),
                        () => AddCondition(choice),
                        tooltip: AssetManagerControls.FormatFilterCondition(choice),
                        variant: UiButtonVariant.Ghost);
                    button.AddToClassList("ee4v-asset-manager-filter-editor__add-condition");
                    actions.Add(button);
                }
                actions.Add(
                    AssetManagerControls.CreateIconTextButton(
                        I18N.Get("filterEditor.addGroup"),
                        "add.png",
                        AddGroup));
                Root.Add(actions);

                var children = initialChildren ??
                               Array.Empty<AssetFilterNode>();
                for (var i = 0; i < children.Count; i++)
                {
                    AddEditor(_owner.CreateNodeEditor(
                        children[i],
                        RemoveChild));
                }

                if (_children.Count == 0 && !_allowEmpty)
                {
                    AddCondition(AssetFilterConditionType.NameContains);
                }
                RefreshGroup();
            }

            public VisualElement Root { get; }

            public bool TryCreateNode(
                out AssetFilterNode node,
                out string errorKey)
            {
                if (_children.Count == 0)
                {
                    node = null;
                    errorKey = _allowEmpty ? null : "filterEditor.groupConditionRequired";
                    return _allowEmpty;
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
                    : _matchMode == FilterMatchMode.Any
                        ? AssetFilterNode.Or(nodes)
                        : AssetFilterNode.And(nodes);
                if (_inverted.value)
                {
                    node = AssetFilterNode.Not(node);
                }

                errorKey = null;
                return true;
            }

            private void SetMatchMode(FilterMatchMode mode)
            {
                _matchMode = mode;
                _matchAll.EnableInClassList("ee4v-asset-manager-filter-editor__match--selected", mode == FilterMatchMode.All);
                _matchAny.EnableInClassList("ee4v-asset-manager-filter-editor__match--selected", mode == FilterMatchMode.Any);
                _matchAll.SetLabelColor(mode == FilterMatchMode.All ? UiColorTokens.Focus : UiColorTokens.TextPrimary);
                _matchAny.SetLabelColor(mode == FilterMatchMode.Any ? UiColorTokens.Focus : UiColorTokens.TextPrimary);
            }

            private void RefreshMatchLabels()
            {
                _matchAll.SetLabel(I18N.Get(_inverted.value
                    ? "filterEditor.matchAllInverted" : "filterEditor.matchAll"));
                _matchAny.SetLabel(I18N.Get(_inverted.value
                    ? "filterEditor.matchAnyInverted" : "filterEditor.matchAny"));
            }

            private void RefreshGroup()
            {
                _header.style.display = _allowEmpty && _children.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                _empty.style.display = _children.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            private void AddCondition(AssetFilterConditionType type)
            {
                var row = new ConditionRow(_owner, AssetFilterNode.Condition(type, string.Empty), false, RemoveChild);
                AddEditor(row);
                row.FocusValue();
            }

            private void AddGroup()
            {
                AddEditor(new FilterGroupEditor(
                    _owner,
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
                RefreshGroup();
            }

            private void RemoveChild(IFilterNodeEditor editor)
            {
                if (editor == null || !_children.Remove(editor))
                {
                    return;
                }

                editor.Root.RemoveFromHierarchy();
                RefreshGroup();
            }
        }

        private sealed class ConditionRow : IFilterNodeEditor
        {
            private readonly AssetFilterEditor _owner;
            private readonly EnumFormInput _condition;
            private readonly FormInput<InputField> _value;
            private readonly Toggle _inverted;
            private readonly UiButton _tag;

            internal ConditionRow(
                AssetFilterEditor owner,
                AssetFilterNode initialCondition,
                bool inverted,
                Action<IFilterNodeEditor> remove)
            {
                _owner = owner;
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
                    value => AssetManagerControls.FormatFilterCondition(value, _inverted?.value == true),
                    "ee4v-asset-manager__collection-popup-condition-type");
                _inverted = CreateInvertToggle(
                    "ee4v-asset-manager__collection-popup-condition-inverted");
                _inverted.value = inverted;
                _inverted.RegisterValueChangedCallback(_ => RefreshConditionLabel());
                RefreshConditionLabel();
                controls.Add(_condition);
                var valueContainer = new VisualElement();
                valueContainer.AddToClassList("ee4v-asset-manager-filter-editor__value");
                controls.Add(valueContainer);
                controls.Add(
                    AssetManagerControls.CreateIconButton(
                        I18N.Get("filterEditor.removeCondition"),
                        "dismiss.png",
                        UiSizeTokens.Size12,
                        UiButtonVariant.Ghost,
                        () => remove?.Invoke(this),
                        "ee4v-asset-manager__collection-popup-remove-condition"));
                Root.Add(controls);

                _value = AssetManagerControls.CreateTextField(
                    string.Empty,
                    "ee4v-asset-manager__collection-popup-condition-value");
                _value.Input.Value = initialCondition?.Value ?? string.Empty;
                valueContainer.Add(_value);
                _tag = AssetManagerControls.CreateIconTextButton(
                    string.Empty, "tag.png", OpenTagPicker,
                    "ee4v-asset-manager__collection-popup-condition-tag");
                _tag.SetContentAlignment(Justify.FlexStart);
                valueContainer.Add(_tag);
                Root.Add(_inverted);
                _condition.RegisterCallback<ChangeEvent<Enum>>(_ => RefreshValueInput());
                Root.RegisterCallback<DetachFromPanelEvent>(_ => _owner.CloseTagPicker(_tag));
                RefreshValueInput();
            }

            public VisualElement Root { get; }

            private void RefreshConditionLabel()
            {
                _condition.SetValueWithoutNotify(_condition.value);
            }

            internal void FocusValue()
            {
                Root.schedule.Execute(() =>
                {
                    if ((AssetFilterConditionType)_condition.value == AssetFilterConditionType.HasTag)
                    {
                        _tag.Focus();
                    }
                    else { _value.Input.FocusInput(); }
                });
            }

            private void OpenTagPicker()
            {
                _owner.OpenTagPicker(_tag, _value.Input.Value, value =>
                {
                    _value.Input.Value = value;
                    RefreshValueInput();
                });
            }

            private void RefreshValueInput()
            {
                var isTag = (AssetFilterConditionType)_condition.value == AssetFilterConditionType.HasTag;
                _value.Input.SetPlaceholder(I18N.Get(
                    (AssetFilterConditionType)_condition.value == AssetFilterConditionType.HasFileExtension
                        ? "filterEditor.extensionValue" : "filterEditor.textValue"));
                if (!isTag) { _owner.CloseTagPicker(_tag); }
                _value.style.display = isTag ? DisplayStyle.None : DisplayStyle.Flex;
                _tag.style.display = isTag ? DisplayStyle.Flex : DisplayStyle.None;
                var value = (_value.Input.Value ?? string.Empty).Trim();
                _tag.SetLabel(value.Length == 0 ? I18N.Get("filterEditor.selectTag") : value);
                _tag.tooltip = value.Length == 0 ? I18N.Get("filterEditor.selectTag") : value;
            }

            public bool TryCreateNode(
                out AssetFilterNode node,
                out string errorKey)
            {
                var value = (_value.Input.Value ?? string.Empty).Trim();
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
