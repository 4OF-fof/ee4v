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

        internal ControlsSectionContext CreateTabbedControlsSection(VisualElement parent, string description)
        {
            var card = new InfoCard(new InfoCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            var tabCard = new TabCard();
            tabCard.Content.AddToClassList("ee4v-ui-catalog-controls");
            card.Body.Add(tabCard);
            parent.Add(card);
            return new ControlsSectionContext(card, tabCard.Content, tabCard);
        }

        internal ControlsSectionContext CreatePlainControlsSection(VisualElement parent, string description)
        {
            var card = new InfoCard(new InfoCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            var content = new VisualElement();
            content.AddToClassList("ee4v-ui-catalog-controls");
            content.style.flexDirection = FlexDirection.Column;
            card.Body.Add(content);
            parent.Add(card);
            return new ControlsSectionContext(card, content, null);
        }

        internal static InputField AddTextField(VisualElement parent, string label, string value, Action<string> onChanged, bool multiline = false)
        {
            var field = new InputField(new InputFieldState(value, label, multiline));
            field.ValueChanged += onChanged;
            parent.Add(field);
            return field;
        }

        internal static EnumField AddEnumField<TEnum>(VisualElement parent, string label, TEnum value, Action<TEnum> onChanged)
            where TEnum : struct, Enum
        {
            var field = new EnumField(label, (Enum)(object)value);
            field.RegisterValueChangedCallback(evt => onChanged((TEnum)(object)evt.newValue));
            parent.Add(field);
            return field;
        }

        internal static ObjectField AddObjectField<TObject>(VisualElement parent, string label, TObject value, Action<TObject> onChanged)
            where TObject : UnityEngine.Object
        {
            var field = new ObjectField(label)
            {
                objectType = typeof(TObject),
                allowSceneObjects = false,
                value = value
            };
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
