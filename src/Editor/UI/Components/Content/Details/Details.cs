using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class DetailHeader : InfoCard
    {
        public DetailHeader(
            string title,
            string eyebrow,
            string subtitle = null,
            BadgeState status = null)
            : base(new InfoCardState(
                title,
                subtitle,
                eyebrow,
                status))
        {
            AddToClassList("ee4v-ui-details__detail-header");
            EyebrowText.AddToClassList("ee4v-ui-details__eyebrow");
            EyebrowText.tooltip = eyebrow ?? string.Empty;
            TitleText.AddToClassList("ee4v-ui-details__detail-title");
            TitleText.SetFontSize(UiTypographyTokens.TitleFontSize);
            TitleText.tooltip = title ?? string.Empty;
            DescriptionText.AddToClassList(
                "ee4v-ui-details__detail-subtitle");
            DescriptionText.SetWhiteSpace(WhiteSpace.NoWrap);
            DescriptionText.SetColor(UiColorTokens.TextMuted);
            DescriptionText.SetTextAlign(TextAnchor.MiddleLeft);
            DescriptionText.tooltip = subtitle ?? string.Empty;
            if (status != null)
            {
                Badge.AddToClassList(
                    "ee4v-ui-details__detail-status");
            }

            Body.AddToClassList("ee4v-ui-details__actions");
        }

        public void AddAction(VisualElement action)
        {
            if (action == null)
            {
                return;
            }

            Body.Add(action);
        }

        public void AddHeaderAction(VisualElement action)
        {
            if (action == null)
            {
                return;
            }

            action.AddToClassList(
                "ee4v-ui-details__detail-header-action");
            HeaderRight.Add(action);
            HeaderRight.style.display = DisplayStyle.Flex;
        }
    }

    public sealed class DetailSection : VisualElement
    {
        public DetailSection(string title, string note = null)
        {
            AddToClassList(
                "ee4v-ui-details__item-detail-section");
            var header = new SectionHeader(title);
            header.AddToClassList(
                "ee4v-ui-details__section-header");
            header.TitleText.AddToClassList(
                "ee4v-ui-details__section-title");
            if (!string.IsNullOrWhiteSpace(note))
            {
                var noteText = UiTextFactory.Create(
                    note,
                    UiClassNames.SecondaryText,
                    "ee4v-ui-details__section-note");
                noteText.SetTextAlign(TextAnchor.MiddleRight);
                header.Actions.Add(noteText);
            }
            Add(header);
        }
    }

    public sealed class MetricDisplay : VisualElement
    {
        public MetricDisplay(string label, string value)
        {
            AddToClassList("ee4v-ui-details__overview-fact");
            Add(UiTextFactory.Create(
                label,
                UiClassNames.SecondaryText,
                "ee4v-ui-details__overview-fact-label"));
            var valueText = UiTextFactory.Create(
                value,
                UiClassNames.SectionTitle,
                "ee4v-ui-details__overview-fact-value");
            valueText.SetWhiteSpace(WhiteSpace.Normal);
            Add(valueText);
        }
    }

    public sealed class PropertyList : VisualElement
    {
        public PropertyList()
        {
            AddToClassList("ee4v-ui-details__property-list");
        }

        public void AddRow(PropertyRow row)
        {
            row.EnableInClassList(
                "ee4v-ui-details__property-row--separated",
                childCount > 0);
            Add(row);
        }
    }

    public sealed class PropertyRow : VisualElement
    {
        public PropertyRow(
            string label,
            VisualElement value,
            VisualElement action = null)
            : this(label)
        {
            if (value != null)
            {
                value.AddToClassList(
                    "ee4v-ui-details__property-value-content");
                _value.Add(value);
            }
            if (action != null)
            {
                action.AddToClassList(
                    "ee4v-ui-details__property-action");
                _value.Add(action);
            }
        }

        private PropertyRow(string label)
        {
            AddToClassList("ee4v-ui-details__property-row");
            Add(UiTextFactory.Create(
                label,
                UiClassNames.FormLabel,
                "ee4v-ui-details__property-label"));
            _value = new VisualElement();
            _value.AddToClassList("ee4v-ui-details__property-value");
            Add(_value);
        }

        public static PropertyRow Editable(
            string label,
            string summary,
            FormInput<InputField> editor,
            VisualElement saveAction,
            string editLabel)
        {
            var row = new PropertyRow(label);
            var display = new VisualElement();
            display.AddToClassList(
                "ee4v-ui-details__property-display");
            var summaryText = UiTextFactory.Create(
                summary,
                "ee4v-ui-details__property-summary");
            summaryText.SetWhiteSpace(WhiteSpace.Normal);
            display.Add(summaryText);

            var editorRow = new VisualElement();
            editorRow.AddToClassList(
                "ee4v-ui-details__property-editor");
            editorRow.style.display = DisplayStyle.None;
            editorRow.Add(editor);
            saveAction.AddToClassList(
                "ee4v-ui-details__property-action");
            editorRow.Add(saveAction);

            var edit = new UiButton(
                editLabel,
                () =>
                {
                    display.style.display = DisplayStyle.None;
                    editorRow.style.display = DisplayStyle.Flex;
                    editor.Input.FocusInput();
                });
            edit.AddToClassList("ee4v-ui-details__inline-action");
            display.Add(edit);
            row._value.Add(display);
            row._value.Add(editorRow);
            return row;
        }

        private readonly VisualElement _value;
    }

    public sealed class KeyValueRow : VisualElement
    {
        public KeyValueRow(string key, string value)
            : this(key, CreateValue(value))
        {
        }

        public KeyValueRow(
            string key,
            VisualElement value)
        {
            AddToClassList("ee4v-ui-details__key-value");
            Add(UiTextFactory.Create(
                key,
                UiClassNames.FormLabel,
                "ee4v-ui-details__key"));
            if (value != null)
            {
                value.AddToClassList("ee4v-ui-details__value");
                Add(value);
            }
        }

        private static VisualElement CreateValue(string value)
        {
            var text = UiTextFactory.Create(value);
            text.SetWhiteSpace(WhiteSpace.Normal);
            return text;
        }
    }
}
