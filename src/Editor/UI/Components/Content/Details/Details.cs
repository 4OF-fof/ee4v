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

    public enum ValueDisplayLayout { Inline, Stacked }

    public sealed class ValueDisplay : FormInput
    {
        public ValueDisplay(string label, string value, ValueDisplayLayout layout = ValueDisplayLayout.Inline)
            : this(label, CreateValue(value, layout), layout) { }

        public ValueDisplay(string label, VisualElement value, ValueDisplayLayout layout = ValueDisplayLayout.Inline)
            : base(label, value ?? new VisualElement(), null,
                layout == ValueDisplayLayout.Stacked ? UiClassNames.SecondaryText : UiClassNames.FormLabel, false)
        {
            AddToClassList("ee4v-ui-value-display");
            AddToClassList(layout == ValueDisplayLayout.Stacked
                ? "ee4v-ui-value-display--stacked" : "ee4v-ui-value-display--inline");
            LabelText.AddToClassList("ee4v-ui-value-display__label");
            Input.AddToClassList("ee4v-ui-value-display__value");
        }

        private static UiTextElement CreateValue(string value, ValueDisplayLayout layout)
        {
            var text = layout == ValueDisplayLayout.Stacked
                ? UiTextFactory.Create(value, UiClassNames.SectionTitle) : UiTextFactory.Create(value);
            text.SetWhiteSpace(WhiteSpace.Normal);
            return text;
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

    public sealed class PropertyRow : FormInput
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

        private PropertyRow(string label) : base(label, new VisualElement(), null, UiClassNames.FormLabel, false)
        {
            AddToClassList("ee4v-ui-details__property-row");
            LabelText.AddToClassList("ee4v-ui-details__property-label");
            _value = Input;
            _value.AddToClassList("ee4v-ui-details__property-value");
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

}
