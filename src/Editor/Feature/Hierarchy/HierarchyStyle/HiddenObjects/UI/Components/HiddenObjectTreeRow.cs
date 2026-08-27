using System;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectTreeRow : ItemRow
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
        private HiddenObjectTreeItemViewState _state;

        public HiddenObjectTreeRow()
        {
            AddToClassList(RootClassName);

            _selection = UiTextFactory.CreateToggle();
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

            Leading.Add(_selection);
            IconElement.AddToClassList(IconClassName);
            TitleText.AddToClassList(NameClassName);
            DescriptionText.AddToClassList(MetaClassName);

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
            base.SetState(new ItemRowState(
                _state.Name,
                _state.Meta,
                _state.Icon ?? IconState.FromBuiltinIcon(
                    UiBuiltinIcon.GameObject,
                    UiSizeTokens.Size16)));
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
}
