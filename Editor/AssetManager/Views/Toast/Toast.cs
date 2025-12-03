using System;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Toast {
    public class Toast : VisualElement {
        private readonly IVisualElementScheduledItem _autoCloseScheduler;
        private readonly Label _messageLabel;

        public Toast(string message, float? duration = null) {
            style.backgroundColor = ColorPreset.DefaultBackground;
            style.borderTopLeftRadius = 6;
            style.borderTopRightRadius = 6;
            style.borderBottomLeftRadius = 6;
            style.borderBottomRightRadius = 6;
            style.paddingLeft = 12;
            style.paddingRight = 12;
            style.paddingTop = 8;
            style.paddingBottom = 8;
            style.marginBottom = 8;
            style.minWidth = 200;
            style.maxWidth = 400;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.borderLeftWidth = 3;
            style.borderLeftColor = ColorPreset.AccentBlue;

            var contentContainer1 = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1
                }
            };
            Add(contentContainer1);

            _messageLabel = new Label(message) {
                style = {
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.Normal,
                    color = ColorPreset.TextColor
                }
            };
            contentContainer1.Add(_messageLabel);

            var closeLabel = new Label("×") {
                style = {
                    width = 20,
                    height = 20,
                    marginLeft = 8,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0
                },
                pickingMode = PickingMode.Position
            };
            contentContainer1.Add(closeLabel);

            closeLabel.RegisterCallback<MouseEnterEvent>(_ =>
            {
                closeLabel.style.backgroundColor = new StyleColor(ColorPreset.WarningButton);
            });
            closeLabel.RegisterCallback<MouseLeaveEvent>(_ => { closeLabel.style.backgroundColor = Color.clear; });

            closeLabel.RegisterCallback<ClickEvent>(evt =>
            {
                Close();
                evt.StopPropagation();
            });

            if (duration is > 0)
                _autoCloseScheduler = schedule.Execute(Close).StartingIn((long)(duration.Value * 1000));

            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target != closeLabel) Close();
            });

            style.opacity = 0;
            schedule.Execute(() => { style.opacity = 1; }).StartingIn(10);
        }

        public event Action<Toast> OnClosed;

        public void SetMessage(string message) {
            _messageLabel.text = message;
        }

        public void SetType(ToastType type) {
            style.borderLeftColor = type switch {
                ToastType.Info    => ColorPreset.AccentBlue,
                ToastType.Success => ColorPreset.SuccessButton,
                ToastType.Warning => ColorPreset.HighlightColor,
                ToastType.Error   => ColorPreset.WarningButton,
                _                 => style.borderLeftColor
            };
        }

        private void Close() {
            _autoCloseScheduler?.Pause();

            const int fadeDuration = 200;
            const int frameInterval = 16;
            const int totalFrames = fadeDuration / frameInterval;
            var currentOpacity = style.opacity.value;
            var opacityStep = currentOpacity / totalFrames;
            var frame = 0;

            schedule.Execute(() =>
            {
                frame++;
                var newOpacity = currentOpacity - opacityStep * frame;
                style.opacity = Mathf.Max(0, newOpacity);

                if (frame < totalFrames) return;
                OnClosed?.Invoke(this);
                RemoveFromHierarchy();
            }).Every(frameInterval).Until(() => frame >= totalFrames);
        }
    }

    public enum ToastType {
        Info,
        Success,
        Warning,
        Error
    }
}