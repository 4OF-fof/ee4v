using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapePresetMappingRow : VisualElement
    {
        private static readonly List<string> SideChoices =
            new List<string> { string.Empty, "L", "R" };
        private readonly Action _changed;
        private readonly Func<
            BlendShapeNameMapping,
            BlendShapePresetView.MappingDragPayload> _createPayload;
        private readonly Action<
            BlendShapePresetView.MappingDragPayload,
            BlendShapeNameMapping> _drop;
        private readonly UiTextElement _source;
        private readonly TextField _variation;
        private readonly PopupField<string> _side;
        private BlendShapeNameMapping _mapping;
        private bool _binding;

        internal BlendShapePresetMappingRow(
            Action changed,
            Func<BlendShapeNameMapping,
                BlendShapePresetView.MappingDragPayload> createPayload,
            Action<BlendShapePresetView.MappingDragPayload,
                BlendShapeNameMapping> drop)
        {
            _changed = changed;
            _createPayload = createPayload;
            _drop = drop;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;

            _source = UiTextFactory.Create();
            Configure(_source, 2.2f);
            Add(_source);
            BlendShapePresetView.RegisterPresetDrag(
                _source,
                () => _createPayload?.Invoke(_mapping));
            BlendShapePresetView.RegisterPresetDrop(
                this,
                payload =>
                {
                    var target = _createPayload?.Invoke(_mapping);
                    return target != null && string.Equals(
                        target.SectionKey,
                        payload.SectionKey,
                        StringComparison.Ordinal);
                },
                payload => _drop?.Invoke(payload, _mapping));

            _variation = UiTextFactory.CreateTextField();
            Configure(_variation, 1f);
            _variation.RegisterValueChangedCallback(evt => Change(
                mapping => mapping.variation = evt.newValue));
            Add(_variation);
            _side = UiTextFactory.CreatePopupField(
                string.Empty,
                SideChoices,
                0,
                value => string.IsNullOrEmpty(value) ? "-" : value,
                value => string.IsNullOrEmpty(value) ? "-" : value);
            Configure(_side, 0.55f);
            _side.RegisterValueChangedCallback(evt => Change(
                mapping => mapping.side = evt.newValue));
            Add(_side);
        }

        internal void Bind(BlendShapeNameMapping mapping)
        {
            _binding = true;
            _mapping = mapping;
            _source.SetText(mapping.meshName + " / " + mapping.shapeName);
            _variation.SetValueWithoutNotify(
                mapping.variation ?? string.Empty);
            var side = SideChoices.Contains(mapping.side)
                ? mapping.side
                : string.Empty;
            _side.SetValueWithoutNotify(side);
            _binding = false;
        }

        private void Change(Action<BlendShapeNameMapping> change)
        {
            if (_binding || _mapping == null)
            {
                return;
            }

            change(_mapping);
            _changed?.Invoke();
        }

        private static void Configure(VisualElement element, float grow)
        {
            element.style.flexBasis = 0f;
            element.style.flexGrow = grow;
            element.style.marginRight = 4f;
            element.style.minWidth = 0f;
        }
    }
}
