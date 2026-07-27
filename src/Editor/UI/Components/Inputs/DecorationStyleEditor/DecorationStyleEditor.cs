using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class DecorationStyleEditorText
    {
        public DecorationStyleEditorText(
            string colorTitle,
            string colorTooltip,
            string customColorLabel,
            string clearColorLabel,
            string iconTitle,
            string iconTooltip,
            string recentIconsLabel,
            string chooseIconLabel,
            string clearIconLabel)
        {
            ColorTitle = colorTitle ?? string.Empty;
            ColorTooltip = colorTooltip ?? string.Empty;
            CustomColorLabel = customColorLabel ?? string.Empty;
            ClearColorLabel = clearColorLabel ?? string.Empty;
            IconTitle = iconTitle ?? string.Empty;
            IconTooltip = iconTooltip ?? string.Empty;
            RecentIconsLabel = recentIconsLabel ?? string.Empty;
            ChooseIconLabel = chooseIconLabel ?? string.Empty;
            ClearIconLabel = clearIconLabel ?? string.Empty;
        }

        public string ColorTitle { get; }

        public string ColorTooltip { get; }

        public string CustomColorLabel { get; }

        public string ClearColorLabel { get; }

        public string IconTitle { get; }

        public string IconTooltip { get; }

        public string RecentIconsLabel { get; }

        public string ChooseIconLabel { get; }

        public string ClearIconLabel { get; }
    }

    internal sealed class DecorationColorPresetState
    {
        public DecorationColorPresetState(
            Color color,
            string tooltip = null)
        {
            Color = color;
            Tooltip = tooltip ?? string.Empty;
        }

        public Color Color { get; }

        public string Tooltip { get; }
    }

    internal sealed class DecorationIconCandidateState
    {
        public DecorationIconCandidateState(
            Texture texture,
            string tooltip = null,
            bool isApplied = false,
            bool canRemove = true)
        {
            Texture = texture;
            Tooltip = tooltip ?? string.Empty;
            IsApplied = isApplied;
            CanRemove = canRemove;
        }

        public Texture Texture { get; }

        public string Tooltip { get; }

        public bool IsApplied { get; }

        public bool CanRemove { get; }
    }

    internal sealed class DecorationStyleEditorState
    {
        public DecorationStyleEditorState(
            Color color,
            Texture icon,
            IReadOnlyList<DecorationColorPresetState>
                colorPresets = null,
            IReadOnlyList<DecorationIconCandidateState>
                recentIcons = null,
            bool colorIsMixed = false,
            bool iconIsMixed = false)
        {
            Color = color;
            Icon = icon;
            ColorPresets =
                colorPresets ??
                Array.Empty<DecorationColorPresetState>();
            RecentIcons =
                recentIcons ??
                Array.Empty<DecorationIconCandidateState>();
            ColorIsMixed = colorIsMixed;
            IconIsMixed = iconIsMixed;
        }

        public Color Color { get; }

        public Texture Icon { get; }

        public IReadOnlyList<DecorationColorPresetState>
            ColorPresets { get; }

        public IReadOnlyList<DecorationIconCandidateState>
            RecentIcons { get; }

        public bool ColorIsMixed { get; }

        public bool IconIsMixed { get; }
    }

    internal sealed class DecorationStyleEditor : VisualElement
    {
        private static readonly Color DefaultCustomColor =
            new Color(1f, 1f, 1f, 0.7f);
        private const double RecentIconTooltipHideDelay =
            0.15d;

        private const string RootClassName =
            "ee4v-ui-decoration-style-editor";
        private const string SectionClassName =
            "ee4v-ui-decoration-style-editor__section";
        private const string LastSectionClassName =
            "ee4v-ui-decoration-style-editor__section--last";
        private const string SectionHeaderClassName =
            "ee4v-ui-decoration-style-editor__section-header";
        private const string SectionTitleClassName =
            "ee4v-ui-decoration-style-editor__section-title";
        private const string PaletteClassName =
            "ee4v-ui-decoration-style-editor__palette";
        private const string SwatchClassName =
            "ee4v-ui-decoration-style-editor__swatch";
        private const string SwatchColorClassName =
            "ee4v-ui-decoration-style-editor__swatch-color";
        private const string SwatchSelectedClassName =
            "ee4v-ui-decoration-style-editor__swatch--selected";
        private const string CaptionClassName =
            "ee4v-ui-decoration-style-editor__caption";
        private const string RowClassName =
            "ee4v-ui-decoration-style-editor__row";
        private const string LabelClassName =
            "ee4v-ui-decoration-style-editor__label";
        private const string FieldClassName =
            "ee4v-ui-decoration-style-editor__field";
        private const string ColorFieldClassName =
            "ee4v-ui-decoration-style-editor__color-field";
        private const string ObjectFieldClassName =
            "ee4v-ui-decoration-style-editor__object-field";
        private const string RecentIconsClassName =
            "ee4v-ui-decoration-style-editor__recent-icons";
        private const string IconCandidateClassName =
            "ee4v-ui-decoration-style-editor__icon-candidate";
        private const string IconCandidateSelectedClassName =
            "ee4v-ui-decoration-style-editor__icon-candidate--selected";
        private const string IconResetClassName =
            "ee4v-ui-decoration-style-editor__icon-reset";
        private const string
            IconCandidateRemoveProtectedClassName =
                "ee4v-ui-decoration-style-editor__icon-candidate--remove-protected";
        private const string CandidateImageClassName =
            "ee4v-ui-decoration-style-editor__candidate-image";

        private readonly DecorationStyleEditorText _text;
        private readonly VisualElement _palette;
        private readonly UiTextElement _recentIconsCaption;
        private readonly VisualElement _recentIcons;
        private readonly ColorField _colorField;
        private readonly ObjectField _iconField;
        private ImageTooltipWindow _imageTooltipWindow;
        private double _recentIconTooltipHideDeadline =
            -1d;
        private bool _watchingRecentIconTooltipHide;

        public DecorationStyleEditor(
            DecorationStyleEditorText text,
            DecorationStyleEditorState state = null)
        {
            _text = text ??
                throw new ArgumentNullException(nameof(text));

            AddToClassList(RootClassName);

            var colorSection = CreateSection(
                text.ColorTitle,
                text.ColorTooltip);
            _palette = new VisualElement();
            _palette.AddToClassList(PaletteClassName);
            colorSection.Add(_palette);

            _colorField = new ColorField(string.Empty)
            {
                showAlpha = true,
                hdr = false,
                tooltip = text.ColorTooltip
            };
            _colorField.AddToClassList(FieldClassName);
            _colorField.AddToClassList(ColorFieldClassName);
            _colorField.RegisterValueChangedCallback(
                evt => ColorChanged?.Invoke(evt.newValue));
            colorSection.Add(CreateRow(
                text.CustomColorLabel,
                text.ColorTooltip,
                _colorField));
            Add(colorSection);

            var iconSection = CreateSection(
                text.IconTitle,
                text.IconTooltip);
            iconSection.AddToClassList(
                LastSectionClassName);
            _recentIconsCaption = UiTextFactory.Create(
                text.RecentIconsLabel,
                UiClassNames.SecondaryText);
            _recentIconsCaption.AddToClassList(CaptionClassName);
            iconSection.Add(_recentIconsCaption);

            _recentIcons = new VisualElement();
            _recentIcons.AddToClassList(RecentIconsClassName);
            iconSection.Add(_recentIcons);

            _iconField = new ObjectField(string.Empty)
            {
                objectType = typeof(Texture),
                allowSceneObjects = false,
                tooltip = text.IconTooltip
            };
            _iconField.AddToClassList(FieldClassName);
            _iconField.AddToClassList(ObjectFieldClassName);
            _iconField.RegisterValueChangedCallback(
                evt => IconChanged?.Invoke(
                    evt.newValue as Texture));
            iconSection.Add(CreateRow(
                text.ChooseIconLabel,
                text.IconTooltip,
                _iconField));
            Add(iconSection);

            RegisterCallback<DetachFromPanelEvent>(
                _ => HideRecentIconTooltip());

            SetState(
                state ??
                new DecorationStyleEditorState(
                    Color.clear,
                    null));
        }

        public event Action<Color> ColorChanged;

        public event Action<Texture> IconChanged;

        public event Action<Texture>
            RemoveRecentIconRequested;

        public event Action ClearColorRequested;

        public event Action ClearIconRequested;

        internal ColorField ColorField
        {
            get { return _colorField; }
        }

        internal ObjectField IconField
        {
            get { return _iconField; }
        }

        internal int ColorPresetCount
        {
            get
            {
                return Mathf.Max(
                    0,
                    _palette.childCount - 1);
            }
        }

        internal int RecentIconCount
        {
            get
            {
                return Mathf.Max(
                    0,
                    _recentIcons.childCount - 1);
            }
        }

        internal int RemovableRecentIconCount
        {
            get
            {
                var count = 0;
                foreach (var child in
                         _recentIcons.Children())
                {
                    if (child.ClassListContains(
                            IconResetClassName) ||
                        child.ClassListContains(
                            IconCandidateRemoveProtectedClassName))
                    {
                        continue;
                    }

                    count++;
                }

                return count;
            }
        }

        internal bool HasIconResetCandidate
        {
            get
            {
                return _recentIcons.childCount > 0 &&
                    _recentIcons[0].ClassListContains(
                        IconResetClassName);
            }
        }

        internal bool IconResetUsesSwatchStyle
        {
            get
            {
                return _recentIcons.childCount > 0 &&
                    _recentIcons[0].ClassListContains(
                        SwatchClassName);
            }
        }

        public void SetState(DecorationStyleEditorState state)
        {
            state = state ??
                new DecorationStyleEditorState(
                    Color.clear,
                    null);

            _colorField.showMixedValue = state.ColorIsMixed;
            _colorField.SetValueWithoutNotify(
                state.Color == Color.clear
                    ? DefaultCustomColor
                    : state.Color);
            _iconField.showMixedValue = state.IconIsMixed;
            _iconField.SetValueWithoutNotify(state.Icon);
            RebuildColorPresets(state);
            RebuildRecentIcons(state);
        }

        private void RebuildColorPresets(
            DecorationStyleEditorState state)
        {
            _palette.Clear();
            var clearButton = new Button(
                () => ClearColorRequested?.Invoke())
            {
                tooltip = _text.ClearColorLabel
            };
            clearButton.AddToClassList(SwatchClassName);
            clearButton.EnableInClassList(
                SwatchSelectedClassName,
                !state.ColorIsMixed &&
                state.Color == Color.clear);
            clearButton.Add(
                new Icon(
                    IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Close,
                        UiSizeTokens.Size12)));
            _palette.Add(clearButton);

            for (var i = 0;
                 i < state.ColorPresets.Count;
                 i++)
            {
                var preset = state.ColorPresets[i];
                if (preset == null)
                {
                    continue;
                }

                var capturedColor = preset.Color;
                var button = new Button(
                    () => ColorChanged?.Invoke(
                        capturedColor))
                {
                    tooltip = preset.Tooltip
                };
                button.AddToClassList(SwatchClassName);
                button.EnableInClassList(
                    SwatchSelectedClassName,
                    !state.ColorIsMixed &&
                    state.Color == capturedColor);
                var colorPreview = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                colorPreview.AddToClassList(
                    SwatchColorClassName);
                colorPreview.style.backgroundColor =
                    new StyleColor(capturedColor);
                button.Add(colorPreview);
                _palette.Add(button);
            }
        }

        private void RebuildRecentIcons(
            DecorationStyleEditorState state)
        {
            HideRecentIconTooltip();
            _recentIcons.Clear();
            var clearButton = new Button(
                () => ClearIconRequested?.Invoke())
            {
                tooltip = _text.ClearIconLabel
            };
            clearButton.AddToClassList(SwatchClassName);
            clearButton.AddToClassList(
                IconResetClassName);
            clearButton.EnableInClassList(
                SwatchSelectedClassName,
                !state.IconIsMixed &&
                state.Icon == null);
            clearButton.Add(
                new Icon(
                    IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Close,
                        UiSizeTokens.Size12)));
            _recentIcons.Add(clearButton);

            for (var i = 0;
                 i < state.RecentIcons.Count;
                 i++)
            {
                var candidate = state.RecentIcons[i];
                if (candidate == null ||
                    candidate.Texture == null)
                {
                    continue;
                }

                var capturedTexture = candidate.Texture;
                var capturedTooltip = candidate.Tooltip;
                var isApplied =
                    candidate.IsApplied ||
                    (!state.IconIsMixed &&
                     state.Icon == capturedTexture);
                var canRemove =
                    candidate.CanRemove &&
                    !isApplied;
                var button = new Button(
                    () => IconChanged?.Invoke(
                        capturedTexture));
                button.AddToClassList(
                    IconCandidateClassName);
                button.EnableInClassList(
                    IconCandidateSelectedClassName,
                    isApplied);
                button.EnableInClassList(
                    IconCandidateRemoveProtectedClassName,
                    !canRemove);

                var image = new Image
                {
                    image = capturedTexture,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                image.AddToClassList(
                    CandidateImageClassName);
                button.Add(image);
                button.RegisterCallback<PointerEnterEvent>(
                    evt => ShowRecentIconTooltip(
                        button,
                        capturedTexture,
                        capturedTooltip,
                        button.LocalToWorld(
                            evt.localPosition)));
                button.RegisterCallback<PointerMoveEvent>(
                    evt => MoveRecentIconTooltip(
                        button,
                        button.LocalToWorld(
                            evt.localPosition)));
                button.RegisterCallback<PointerLeaveEvent>(
                    _ =>
                        ScheduleRecentIconTooltipHide());
                button.RegisterCallback<PointerUpEvent>(
                    evt =>
                    {
                        if (evt.button != 1)
                        {
                            return;
                        }

                        evt.StopImmediatePropagation();
                        if (!canRemove)
                        {
                            return;
                        }

                        HideRecentIconTooltip();
                        RemoveRecentIconRequested?.Invoke(
                            capturedTexture);
                    });
                _recentIcons.Add(button);
            }

            _recentIconsCaption.style.display =
                DisplayStyle.Flex;
            _recentIcons.style.display =
                DisplayStyle.Flex;
        }

        private void ShowRecentIconTooltip(
            VisualElement target,
            Texture texture,
            string fileName,
            Vector2 panelPosition)
        {
            CancelScheduledRecentIconTooltipHide();
            HideRecentIconTooltip();
            _imageTooltipWindow =
                ImageTooltipWindow.Show(
                    target,
                    panelPosition,
                    new ImageTooltipState(
                        texture,
                        fileName,
                        true));
        }

        private void MoveRecentIconTooltip(
            VisualElement target,
            Vector2 panelPosition)
        {
            if (_imageTooltipWindow == null)
            {
                return;
            }

            _imageTooltipWindow.SetPointerPosition(
                target,
                panelPosition);
        }

        private void HideRecentIconTooltip()
        {
            CancelScheduledRecentIconTooltipHide();
            if (_imageTooltipWindow == null)
            {
                return;
            }

            _imageTooltipWindow.Close();
            _imageTooltipWindow = null;
        }

        private void ScheduleRecentIconTooltipHide()
        {
            _recentIconTooltipHideDeadline =
                EditorApplication.timeSinceStartup +
                RecentIconTooltipHideDelay;
            if (_watchingRecentIconTooltipHide)
            {
                return;
            }

            _watchingRecentIconTooltipHide = true;
            EditorApplication.update +=
                WatchRecentIconTooltipHide;
        }

        private void WatchRecentIconTooltipHide()
        {
            if (_recentIconTooltipHideDeadline < 0d ||
                EditorApplication.timeSinceStartup <
                _recentIconTooltipHideDeadline)
            {
                return;
            }

            HideRecentIconTooltip();
        }

        private void
            CancelScheduledRecentIconTooltipHide()
        {
            _recentIconTooltipHideDeadline = -1d;
            if (!_watchingRecentIconTooltipHide)
            {
                return;
            }

            _watchingRecentIconTooltipHide = false;
            EditorApplication.update -=
                WatchRecentIconTooltipHide;
        }

        private static VisualElement CreateSection(
            string titleText,
            string tooltip)
        {
            var section = new VisualElement();
            section.AddToClassList(SectionClassName);

            var header = new VisualElement();
            header.AddToClassList(
                SectionHeaderClassName);
            var title = UiTextFactory.Create(
                titleText,
                UiClassNames.SectionTitle);
            title.AddToClassList(
                SectionTitleClassName);
            title.tooltip = tooltip;
            header.Add(title);

            section.Add(header);
            return section;
        }

        private static VisualElement CreateRow(
            string labelText,
            string tooltip,
            VisualElement field)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClassName);

            var label = UiTextFactory.Create(
                labelText,
                UiClassNames.SecondaryText);
            label.AddToClassList(LabelClassName);
            label.tooltip = tooltip;
            row.Add(label);
            row.Add(field);
            return row;
        }
    }
}
