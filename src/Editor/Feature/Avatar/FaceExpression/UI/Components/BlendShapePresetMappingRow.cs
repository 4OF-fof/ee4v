using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapePresetMappingRow : VisualElement
    {
        private const string RootClassName =
            "ee4v-blend-shape-preset-mapping-row";
        private const string FieldClassName =
            "ee4v-blend-shape-preset-mapping-row__field";
        private static readonly List<string> SideChoices =
            new List<string> { string.Empty, "L", "R" };
        private static readonly List<string> AppearancePartChoices =
            new List<string>
            {
                string.Empty,
                BlendShapeAppearancePart.Expression,
                BlendShapeAppearancePart.Head,
                BlendShapeAppearancePart.Chest,
                BlendShapeAppearancePart.Waist,
                BlendShapeAppearancePart.Shoulders,
                BlendShapeAppearancePart.Arms,
                BlendShapeAppearancePart.Hands,
                BlendShapeAppearancePart.Legs,
                BlendShapeAppearancePart.Feet,
                BlendShapeAppearancePart.Other
            };
        private readonly Action _changed;
        private readonly Func<
            BlendShapeNameMapping,
            BlendShapePresetView.MappingDragPayload> _createPayload;
        private readonly Action<
            BlendShapePresetView.MappingDragPayload,
            BlendShapeNameMapping> _drop;
        private readonly UiTextElement _source;
        private readonly PopupField<string> _side;
        private readonly Toggle _mouthMorph;
        private readonly PopupField<string> _appearancePart;
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
            AddToClassList(RootClassName);

            _source = UiTextFactory.Create();
            Configure(_source, 2.5f);
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

            _side = UiTextFactory.CreatePopupField(
                string.Empty,
                SideChoices,
                0,
                value => string.IsNullOrEmpty(value) ? "-" : value,
                value => string.IsNullOrEmpty(value) ? "-" : value);
            Configure(_side, 0.5f);
            _side.RegisterValueChangedCallback(evt => Change(
                mapping => mapping.side = evt.newValue));
            Add(_side);

            _mouthMorph = UiTextFactory.CreateToggle();
            Configure(_mouthMorph, 0.5f);
            _mouthMorph.RegisterValueChangedCallback(evt => Change(
                mapping => mapping.mouthMorph = evt.newValue));
            Add(_mouthMorph);

            _appearancePart = UiTextFactory.CreatePopupField(
                string.Empty,
                AppearancePartChoices,
                0,
                FormatAppearancePart,
                FormatAppearancePart);
            Configure(_appearancePart, 1.3f);
            _appearancePart.RegisterValueChangedCallback(evt => Change(
                mapping => mapping.appearancePart = evt.newValue));
            Add(_appearancePart);
        }

        internal void Bind(BlendShapeNameMapping mapping)
        {
            _binding = true;
            _mapping = mapping;
            _source.SetText(mapping.meshName + " / " + mapping.shapeName);
            var side = SideChoices.Contains(mapping.side)
                ? mapping.side
                : string.Empty;
            _side.SetValueWithoutNotify(side);
            _mouthMorph.SetValueWithoutNotify(mapping.mouthMorph);
            _appearancePart.SetValueWithoutNotify(
                AppearancePartChoices.Contains(mapping.appearancePart)
                    ? mapping.appearancePart
                    : string.Empty);
            _binding = false;
        }

        private static string FormatAppearancePart(string value)
        {
            return I18N.Get("presetWindow.appearancePart." +
                            (string.IsNullOrEmpty(value) ? "auto" : value));
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
            element.AddToClassList(FieldClassName);
            element.style.flexBasis = 0f;
            element.style.flexGrow = grow;
        }
    }
}
