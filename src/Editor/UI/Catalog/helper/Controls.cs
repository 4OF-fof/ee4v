using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private const string CatalogControlLabelClassName = "ee4v-ui-catalog-control-label";

        private static string FormatCatalogToastTitle(string title)
        {
            var normalized = (title ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return "[TEST]";
            }

            return normalized.StartsWith("[TEST]", StringComparison.Ordinal)
                ? normalized
                : "[TEST] " + normalized;
        }

        internal ControlsSectionContext CreatePlainControlsSection(VisualElement parent, string description)
        {
            var card = new InfoCard(new InfoCardState(I18N.Get("catalog.common.controls"), description));
            card.userData = "catalog-controls-section";
            var content = new VisualElement();
            content.AddToClassList("ee4v-ui-catalog-controls");
            content.style.flexDirection = FlexDirection.Column;
            card.Body.Add(content);
            parent.Add(card);
            return new ControlsSectionContext(card, content);
        }

        internal static InputField AddTextField(VisualElement parent, string label, string value, Action<string> onChanged, bool multiline = false, float maxHeight = 0f, string placeholder = null)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                var labelElement = UiTextFactory.Create(label, CatalogControlLabelClassName);
                labelElement.SetWhiteSpace(WhiteSpace.NoWrap);
                parent.Add(labelElement);
            }

            var field = new InputField(new InputFieldState(value, multiline, maxHeight, placeholder));
            field.ValueChanged += onChanged;
            parent.Add(field);
            return field;
        }

        internal static EnumField AddEnumField<TEnum>(VisualElement parent, string label, TEnum value, Action<TEnum> onChanged)
            where TEnum : struct, Enum
        {
            var field = UiTextFactory.CreateEnumField(
                label,
                (Enum)(object)value);
            field.RegisterValueChangedCallback(evt => onChanged((TEnum)(object)evt.newValue));
            parent.Add(field);
            return field;
        }

        internal static Toggle AddToggle(
            VisualElement parent,
            string label,
            bool value,
            Action<bool> onChanged)
        {
            var field = UiTextFactory.CreateToggle(label);
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(
                evt => onChanged(evt.newValue));
            parent.Add(field);
            return field;
        }

        internal static ObjectField AddObjectField<TObject>(VisualElement parent, string label, TObject value, Action<TObject> onChanged)
            where TObject : UnityEngine.Object
        {
            var field = UiTextFactory.CreateObjectField(label);
            field.objectType = typeof(TObject);
            field.allowSceneObjects = false;
            field.value = value;
            field.RegisterValueChangedCallback(evt => onChanged((TObject)evt.newValue));
            parent.Add(field);
            return field;
        }

        internal static void FinalizeControlsSection(VisualElement parent, ControlsSectionContext controls)
        {
            if (controls == null || controls.Content.childCount > 0)
            {
                return;
            }

            parent.Remove(controls.Card);
        }
    }
}
