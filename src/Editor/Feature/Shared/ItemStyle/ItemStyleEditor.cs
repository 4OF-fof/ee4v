using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.ItemStyle
{
    internal sealed class ItemStyleEditorText
    {
        public string ColorTitle;
        public string ColorTooltip;
        public string CustomColorLabel;
        public string ClearColorLabel;
        public string IconTitle;
        public string IconTooltip;
        public string RecentIconsLabel;
        public string ChooseIconLabel;
        public string ClearIconLabel;
        public string ActionLabel;
        public string ActionTooltip;
    }

    internal sealed class ItemStyleIconCandidate
    {
        public Texture Texture;
        public string Tooltip;
        public bool IsApplied;
    }

    internal sealed class ItemStyleEditorState
    {
        public Color Color;
        public bool ColorIsMixed;
        public Texture Icon;
        public bool IconIsMixed;
        public Texture DefaultIcon;
        public IReadOnlyList<Color> ColorPresets = Array.Empty<Color>();
        public IReadOnlyList<ItemStyleIconCandidate> RecentIcons =
            Array.Empty<ItemStyleIconCandidate>();
        public bool PreviewColorAsBackground;
        public Type IconType = typeof(Texture);
    }

    internal sealed class ItemStyleEditor : VisualElement
    {
        private const string RootClassName = "ee4v-item-style";
        private const string SelectedClassName =
            "ee4v-item-style__choice--selected";
        private readonly ItemStyleEditorText _text;
        private readonly VisualElement _preview;
        private readonly Image _previewImage;
        private readonly VisualElement _palette;
        private readonly VisualElement _recentIcons;
        private readonly ColorField _colorField;
        private readonly ObjectField _iconField;

        public ItemStyleEditor(
            ItemStyleEditorText text,
            Action actionRequested)
        {
            _text = text ??
                throw new ArgumentNullException(nameof(text));
            AddToClassList(RootClassName);

            _preview = new VisualElement();
            _preview.AddToClassList("ee4v-item-style__preview");
            _previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _previewImage.AddToClassList(
                "ee4v-item-style__preview-image");
            _preview.Add(_previewImage);

            var colorSection = CreateSection(
                text.ColorTitle,
                text.ColorTooltip);
            _palette = new VisualElement();
            _palette.AddToClassList("ee4v-item-style__palette");
            colorSection.Add(_palette);
            _colorField = UiTextFactory.CreateColorField();
            _colorField.showAlpha = true;
            _colorField.hdr = false;
            _colorField.tooltip = text.ColorTooltip;
            _colorField.AddToClassList("ee4v-item-style__color-field");
            _colorField.RegisterValueChangedCallback(
                evt => ColorChanged?.Invoke(evt.newValue));
            colorSection.Add(CreateFieldRow(
                text.CustomColorLabel,
                text.ColorTooltip,
                _colorField));
            Add(colorSection);

            var iconSection = CreateSection(
                text.IconTitle,
                text.IconTooltip);
            iconSection.AddToClassList(
                "ee4v-item-style__section--last");
            iconSection.Add(UiTextFactory.Create(
                text.RecentIconsLabel,
                UiClassNames.SecondaryText,
                "ee4v-item-style__caption"));
            _recentIcons = new VisualElement();
            _recentIcons.AddToClassList(
                "ee4v-item-style__recent-icons");
            iconSection.Add(_recentIcons);
            _iconField = UiTextFactory.CreateObjectField();
            _iconField.allowSceneObjects = false;
            _iconField.tooltip = text.IconTooltip;
            _iconField.AddToClassList("ee4v-item-style__object-field");
            _iconField.RegisterValueChangedCallback(
                evt => IconChanged?.Invoke(evt.newValue as Texture));
            iconSection.Add(CreateFieldRow(
                text.ChooseIconLabel,
                text.IconTooltip,
                _iconField));
            Add(iconSection);

            if (actionRequested != null &&
                !string.IsNullOrWhiteSpace(text.ActionLabel))
            {
                var action = new UiButton(
                    text.ActionLabel,
                    actionRequested,
                    text.ActionTooltip,
                    FluentUiIcons.CreateState(
                        "eye_off.png",
                        UiSizeTokens.Size16));
                action.AddToClassList("ee4v-item-style__action");
                Add(action);
            }
        }

        public event Action<Color> ColorChanged;
        public event Action<Texture> IconChanged;
        public event Action<Texture> RemoveRecentIconRequested;
        public event Action ClearColorRequested;
        public event Action ClearIconRequested;

        internal VisualElement HeaderPreview => _preview;

        public void SetState(ItemStyleEditorState state)
        {
            if (state == null)
            {
                return;
            }

            _colorField.showMixedValue = state.ColorIsMixed;
            _colorField.SetValueWithoutNotify(
                state.Color == Color.clear
                    ? new Color(1f, 1f, 1f, 0.7f)
                    : state.Color);
            _iconField.objectType = state.IconType;
            _iconField.showMixedValue = state.IconIsMixed;
            _iconField.SetValueWithoutNotify(state.Icon);
            RebuildPalette(state);
            RebuildRecentIcons(state);

            _previewImage.image = state.Icon ?? state.DefaultIcon;
            _previewImage.tintColor =
                !state.PreviewColorAsBackground &&
                state.Icon == null &&
                !state.ColorIsMixed &&
                state.Color != Color.clear
                    ? state.Color
                    : Color.white;
            _preview.style.backgroundColor =
                state.PreviewColorAsBackground &&
                !state.ColorIsMixed &&
                state.Color != Color.clear
                    ? new StyleColor(state.Color)
                    : StyleKeyword.Null;
        }

        private void RebuildPalette(ItemStyleEditorState state)
        {
            _palette.Clear();
            var clear = CreateChoice(
                _text.ClearColorLabel,
                () => ClearColorRequested?.Invoke());
            clear.EnableInClassList(
                SelectedClassName,
                !state.ColorIsMixed && state.Color == Color.clear);
            clear.Add(new Icon(FluentUiIcons.CreateState(
                "dismiss.png",
                UiSizeTokens.Size12)));
            _palette.Add(clear);
            for (var i = 0; i < state.ColorPresets.Count; i++)
            {
                var color = state.ColorPresets[i];
                var captured = color;
                var button = CreateChoice(
                    _text.ColorTooltip + " #" +
                    ColorUtility.ToHtmlStringRGB(color),
                    () => ColorChanged?.Invoke(captured));
                button.EnableInClassList(
                    SelectedClassName,
                    !state.ColorIsMixed && state.Color == color);
                var swatch = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                swatch.AddToClassList("ee4v-item-style__swatch-color");
                swatch.style.backgroundColor = color;
                button.Add(swatch);
                _palette.Add(button);
            }
        }

        private void RebuildRecentIcons(ItemStyleEditorState state)
        {
            _recentIcons.Clear();
            var clear = CreateChoice(
                _text.ClearIconLabel,
                () => ClearIconRequested?.Invoke());
            clear.EnableInClassList(
                SelectedClassName,
                !state.IconIsMixed && state.Icon == null);
            clear.Add(new Icon(FluentUiIcons.CreateState(
                "dismiss.png",
                UiSizeTokens.Size12)));
            _recentIcons.Add(clear);
            for (var i = 0; i < state.RecentIcons.Count; i++)
            {
                var candidate = state.RecentIcons[i];
                if (candidate?.Texture == null)
                {
                    continue;
                }

                var captured = candidate;
                var button = UiTextFactory.CreateButton(
                    string.Empty,
                    () => IconChanged?.Invoke(captured.Texture));
                button.tooltip = candidate.Tooltip;
                button.AddToClassList("ee4v-item-style__icon-choice");
                button.EnableInClassList(
                    SelectedClassName,
                    candidate.IsApplied);
                button.Add(new Image
                {
                    image = candidate.Texture,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                });
                button.RegisterCallback<ContextClickEvent>(evt =>
                {
                    if (!captured.IsApplied)
                    {
                        RemoveRecentIconRequested?.Invoke(
                            captured.Texture);
                    }

                    evt.StopPropagation();
                });
                _recentIcons.Add(button);
            }
        }

        private static VisualElement CreateSection(
            string title,
            string tooltip)
        {
            var section = new VisualElement();
            section.AddToClassList("ee4v-item-style__section");
            var heading = new SectionHeader(title);
            heading.TitleText.AddToClassList(
                "ee4v-item-style__section-title");
            heading.tooltip = tooltip;
            section.Add(heading);
            return section;
        }

        private static VisualElement CreateFieldRow(
            string label,
            string tooltip,
            VisualElement field)
        {
            var row = new FormField(label, field);
            row.AddToClassList("ee4v-item-style__field-row");
            row.LabelText.AddToClassList(
                "ee4v-item-style__field-label");
            row.LabelText.tooltip = tooltip;
            return row;
        }

        private static UiTextButton CreateChoice(
            string tooltip,
            Action onClick)
        {
            var button = UiTextFactory.CreateButton(
                string.Empty,
                onClick);
            button.tooltip = tooltip;
            button.AddToClassList("ee4v-item-style__choice");
            return button;
        }
    }
}
