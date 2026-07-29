using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class DecorationStyleWindowLayoutState
    {
        public DecorationStyleWindowLayoutState(
            string title,
            string subtitle,
            string targetTooltip,
            string closeTooltip,
            DecorationStyleEditorText editorText,
            DecorationStyleEditorState editorState,
            Texture previewImage = null,
            Color? previewTint = null,
            string actionLabel = null,
            string actionTooltip = null,
            IconState actionIcon = null)
        {
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            TargetTooltip = targetTooltip ?? string.Empty;
            CloseTooltip = closeTooltip ?? string.Empty;
            EditorText = editorText ??
                throw new ArgumentNullException(nameof(editorText));
            EditorState = editorState;
            PreviewImage = previewImage;
            PreviewTint = previewTint ?? Color.white;
            ActionLabel = actionLabel ?? string.Empty;
            ActionTooltip = actionTooltip ?? string.Empty;
            ActionIcon = actionIcon;
        }

        public string Title { get; }

        public string Subtitle { get; }

        public string TargetTooltip { get; }

        public string CloseTooltip { get; }

        public DecorationStyleEditorText EditorText { get; }

        public DecorationStyleEditorState EditorState { get; }

        public Texture PreviewImage { get; }

        public Color PreviewTint { get; }

        public string ActionLabel { get; }

        public string ActionTooltip { get; }

        public IconState ActionIcon { get; }
    }

    internal sealed class DecorationStyleWindowLayout
        : VisualElement
    {
        private const string RootClassName =
            "ee4v-ui-decoration-window-layout";
        private const string HeaderClassName =
            "ee4v-ui-decoration-window-layout__header";
        private const string PreviewClassName =
            "ee4v-ui-decoration-window-layout__preview";
        private const string PreviewImageClassName =
            "ee4v-ui-decoration-window-layout__preview-image";
        private const string HeaderTextClassName =
            "ee4v-ui-decoration-window-layout__header-text";
        private const string TitleClassName =
            "ee4v-ui-decoration-window-layout__title";
        private const string SubtitleClassName =
            "ee4v-ui-decoration-window-layout__subtitle";
        private const string CloseClassName =
            "ee4v-ui-decoration-window-layout__close";
        private const string ActionClassName =
            "ee4v-ui-decoration-window-layout__action";

        private readonly VisualElement _preview;
        private readonly Image _previewImage;

        public DecorationStyleWindowLayout(
            DecorationStyleWindowLayoutState state,
            Action closeRequested = null,
            Action actionRequested = null)
        {
            state = state ??
                throw new ArgumentNullException(nameof(state));

            AddToClassList(RootClassName);

            var header = new VisualElement();
            header.AddToClassList(HeaderClassName);

            _preview = new VisualElement();
            _preview.AddToClassList(PreviewClassName);
            _previewImage = new Image
            {
                image = state.PreviewImage,
                tintColor = state.PreviewTint,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _previewImage.AddToClassList(PreviewImageClassName);
            _preview.Add(_previewImage);
            header.Add(_preview);

            var headerText = new VisualElement();
            headerText.AddToClassList(HeaderTextClassName);
            var title = UiTextFactory.Create(
                state.Title,
                UiClassNames.WindowTitle);
            title.AddToClassList(TitleClassName);
            title.tooltip = state.TargetTooltip;
            headerText.Add(title);

            var subtitle = UiTextFactory.Create(
                state.Subtitle,
                UiClassNames.SecondaryText);
            subtitle.AddToClassList(SubtitleClassName);
            headerText.Add(subtitle);
            header.Add(headerText);

            var closeButton = new UiButton(
                new UiButtonState(
                    tooltip: state.CloseTooltip,
                    iconState: IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Close,
                        UiSizeTokens.Size14),
                    variant: UiButtonVariant.Ghost,
                    size: UiButtonSize.Compact),
                closeRequested);
            closeButton.AddToClassList(CloseClassName);
            header.Add(closeButton);
            Add(header);

            Editor = new DecorationStyleEditor(
                state.EditorText,
                state.EditorState);
            Add(Editor);

            if (!string.IsNullOrWhiteSpace(state.ActionLabel) ||
                state.ActionIcon != null)
            {
                var action = new UiButton(
                    new UiButtonState(
                        state.ActionLabel,
                        tooltip: state.ActionTooltip,
                        iconState: state.ActionIcon,
                        variant: UiButtonVariant.Solid),
                    actionRequested);
                action.AddToClassList(ActionClassName);
                Add(action);
            }
        }

        public DecorationStyleEditor Editor { get; }

        public void SetPreview(
            Texture image,
            Color tintColor)
        {
            _previewImage.image = image;
            _previewImage.tintColor = tintColor;
        }

        public void SetPreviewBackground(Color color)
        {
            _preview.style.backgroundColor =
                new StyleColor(color);
        }

        public void ClearPreviewBackground()
        {
            _preview.style.backgroundColor =
                StyleKeyword.Null;
        }
    }
}
