using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetDetailHeader : VisualElement
    {
        private VisualElement _actions;

        internal AssetDetailHeader(
            string title,
            string eyebrow,
            string subtitle = null,
            StatusBadgeState status = null)
        {
            AddToClassList("ee4v-asset-manager__detail-header");
            var eyebrowText = UiTextFactory.Create(
                eyebrow,
                UiClassNames.InfoCardEyebrow,
                "ee4v-asset-manager__eyebrow");
            eyebrowText.tooltip = eyebrow ?? string.Empty;
            Add(eyebrowText);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("ee4v-asset-manager__title-row");
            var titleText = UiTextFactory.Create(
                title,
                UiClassNames.WindowTitle,
                "ee4v-asset-manager__detail-title");
            titleText.tooltip = title ?? string.Empty;
            titleRow.Add(titleText);
            if (status != null)
            {
                var badge = new StatusBadge(status);
                badge.AddToClassList(
                    "ee4v-asset-manager__detail-status");
                titleRow.Add(badge);
            }
            Add(titleRow);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var subtitleText = UiTextFactory.Create(
                    subtitle,
                    UiClassNames.FormLabel,
                    "ee4v-asset-manager__detail-subtitle");
                subtitleText.SetWhiteSpace(WhiteSpace.NoWrap);
                subtitleText.SetColor(UiColorTokens.TextMuted);
                subtitleText.SetTextAlign(TextAnchor.MiddleLeft);
                subtitleText.tooltip = subtitle;
                Add(subtitleText);
            }
        }

        internal void AddAction(VisualElement action)
        {
            if (action == null)
            {
                return;
            }

            if (_actions == null)
            {
                _actions = new VisualElement();
                _actions.AddToClassList(
                    "ee4v-asset-manager__actions");
                Add(_actions);
            }
            _actions.Add(action);
        }
    }

    internal sealed class AssetDetailSection : VisualElement
    {
        internal AssetDetailSection(string title, string note = null)
        {
            AddToClassList(
                "ee4v-asset-manager__item-detail-section");
            var header = new VisualElement();
            header.AddToClassList(
                "ee4v-asset-manager__section-header");
            header.Add(UiTextFactory.Create(
                title,
                UiClassNames.SectionTitle,
                "ee4v-asset-manager__section-title"));
            if (!string.IsNullOrWhiteSpace(note))
            {
                var noteText = UiTextFactory.Create(
                    note,
                    UiClassNames.SecondaryText,
                    "ee4v-asset-manager__section-note");
                noteText.SetTextAlign(TextAnchor.MiddleRight);
                header.Add(noteText);
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

    internal sealed class AssetDetailSettingRow : VisualElement
    {
        private readonly VisualElement _value;

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
                _value.Add(value);
            }
            if (action != null)
            {
                action.AddToClassList(
                    "ee4v-asset-manager__setting-action");
                _value.Add(action);
            }
        }

        private AssetDetailSettingRow(string label)
        {
            AddToClassList("ee4v-asset-manager__setting-row");
            Add(UiTextFactory.Create(
                label,
                UiClassNames.FormLabel,
                "ee4v-asset-manager__setting-label"));
            _value = new VisualElement();
            _value.AddToClassList(
                "ee4v-asset-manager__setting-value");
            Add(_value);
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
            row._value.Add(display);
            row._value.Add(editorRow);
            return row;
        }
    }

    internal sealed class AssetDetailKeyValueRow : VisualElement
    {
        internal AssetDetailKeyValueRow(string key, string value)
            : this(key, CreateValue(value))
        {
        }

        internal AssetDetailKeyValueRow(
            string key,
            VisualElement value)
        {
            AddToClassList("ee4v-asset-manager__key-value");
            Add(UiTextFactory.Create(
                key,
                UiClassNames.FormLabel,
                "ee4v-asset-manager__key"));
            if (value != null)
            {
                value.AddToClassList("ee4v-asset-manager__value");
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
