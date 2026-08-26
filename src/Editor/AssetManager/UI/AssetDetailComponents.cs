using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetDetailHeader : InfoCard
    {
        internal AssetDetailHeader(
            string title,
            string eyebrow,
            string subtitle = null,
            StatusBadgeState status = null)
            : base(new InfoCardState(
                title,
                subtitle,
                eyebrow,
                status))
        {
            AddToClassList("ee4v-asset-manager__detail-header");
            EyebrowText.AddToClassList("ee4v-asset-manager__eyebrow");
            EyebrowText.tooltip = eyebrow ?? string.Empty;
            TitleText.AddToClassList("ee4v-asset-manager__detail-title");
            TitleText.SetFontSize(UiTypographyTokens.TitleFontSize);
            TitleText.tooltip = title ?? string.Empty;
            DescriptionText.AddToClassList(
                "ee4v-asset-manager__detail-subtitle");
            DescriptionText.SetWhiteSpace(WhiteSpace.NoWrap);
            DescriptionText.SetColor(UiColorTokens.TextMuted);
            DescriptionText.SetTextAlign(TextAnchor.MiddleLeft);
            DescriptionText.tooltip = subtitle ?? string.Empty;
            if (status != null)
            {
                Badge.AddToClassList(
                    "ee4v-asset-manager__detail-status");
            }

            Body.AddToClassList("ee4v-asset-manager__actions");
        }

        internal void AddAction(VisualElement action)
        {
            if (action == null)
            {
                return;
            }

            Body.Add(action);
        }
    }

    internal sealed class AssetDetailSection : VisualElement
    {
        internal AssetDetailSection(string title, string note = null)
        {
            AddToClassList(
                "ee4v-asset-manager__item-detail-section");
            var header = new SectionHeader(title);
            header.AddToClassList(
                "ee4v-asset-manager__section-header");
            header.TitleText.AddToClassList(
                "ee4v-asset-manager__section-title");
            if (!string.IsNullOrWhiteSpace(note))
            {
                var noteText = UiTextFactory.Create(
                    note,
                    UiClassNames.SecondaryText,
                    "ee4v-asset-manager__section-note");
                noteText.SetTextAlign(TextAnchor.MiddleRight);
                header.Actions.Add(noteText);
            }
            Add(header);
        }
    }

    internal sealed class AssetDetailFact : VisualElement
    {
        internal AssetDetailFact(string label, string value)
        {
            AddToClassList("ee4v-asset-manager__overview-fact");
            Add(UiTextFactory.Create(
                label,
                UiClassNames.SecondaryText,
                "ee4v-asset-manager__overview-fact-label"));
            var valueText = UiTextFactory.Create(
                value,
                UiClassNames.SectionTitle,
                "ee4v-asset-manager__overview-fact-value");
            valueText.SetWhiteSpace(WhiteSpace.Normal);
            Add(valueText);
        }
    }

    internal sealed class AssetDetailSettingList : VisualElement
    {
        internal AssetDetailSettingList()
        {
            AddToClassList("ee4v-asset-manager__setting-list");
        }
    }

    internal sealed class AssetDetailSettingRow : LabeledContentRow
    {
        internal AssetDetailSettingRow(
            string label,
            VisualElement value,
            VisualElement action = null)
            : this(label)
        {
            if (value != null)
            {
                value.AddToClassList(
                    "ee4v-asset-manager__setting-value-content");
                Content.Add(value);
            }
            if (action != null)
            {
                action.AddToClassList(
                    "ee4v-asset-manager__setting-action");
                Content.Add(action);
            }
        }

        private AssetDetailSettingRow(string label)
            : base(label)
        {
            AddToClassList("ee4v-asset-manager__setting-row");
            LabelText.AddToClassList(
                "ee4v-asset-manager__setting-label");
            Content.AddToClassList(
                "ee4v-asset-manager__setting-value");
        }

        internal static AssetDetailSettingRow Editable(
            string label,
            string summary,
            AssetManagerTextField editor,
            VisualElement saveAction,
            string editLabel)
        {
            var row = new AssetDetailSettingRow(label);
            var display = new VisualElement();
            display.AddToClassList(
                "ee4v-asset-manager__setting-display");
            var summaryText = UiTextFactory.Create(
                summary,
                "ee4v-asset-manager__setting-summary");
            summaryText.SetWhiteSpace(WhiteSpace.Normal);
            display.Add(summaryText);

            var editorRow = new VisualElement();
            editorRow.AddToClassList(
                "ee4v-asset-manager__setting-editor");
            editorRow.style.display = DisplayStyle.None;
            editorRow.Add(editor);
            saveAction.AddToClassList(
                "ee4v-asset-manager__setting-action");
            editorRow.Add(saveAction);

            display.Add(AssetManagerControls.CreateButton(
                editLabel,
                () =>
                {
                    display.style.display = DisplayStyle.None;
                    editorRow.style.display = DisplayStyle.Flex;
                    editor.FocusInput();
                },
                "ee4v-asset-manager__inline-action"));
            row.Content.Add(display);
            row.Content.Add(editorRow);
            return row;
        }
    }

    internal sealed class AssetDetailKeyValueRow : LabeledContentRow
    {
        internal AssetDetailKeyValueRow(string key, string value)
            : this(key, CreateValue(value))
        {
        }

        internal AssetDetailKeyValueRow(
            string key,
            VisualElement value)
            : base(key)
        {
            AddToClassList("ee4v-asset-manager__key-value");
            LabelText.AddToClassList("ee4v-asset-manager__key");
            if (value != null)
            {
                value.AddToClassList("ee4v-asset-manager__value");
                Content.Add(value);
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
