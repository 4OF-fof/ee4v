using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Toast : VisualElement {
        private readonly IVisualElementScheduledItem _autoCloseScheduler;
        private readonly Label _messageLabel;

        public Toast(string message, float? duration = null) {
            style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
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
            style.borderLeftColor = new Color(0.3f, 0.7f, 1.0f);

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
                    color = Color.white
                }
            };
            contentContainer1.Add(_messageLabel);

            var closeButton = new Button(Close) {
                text = "×",
                style = {
                    width = 20,
                    height = 20,
                    marginLeft = 8,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f),
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0
                }
            };
            contentContainer1.Add(closeButton);

            closeButton.RegisterCallback<MouseEnterEvent>(_ =>
            {
                closeButton.style.backgroundColor = new Color(0.5f, 0.3f, 0.3f);
            });
            closeButton.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                closeButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            });

            if (duration.HasValue && duration.Value > 0)
                _autoCloseScheduler = schedule.Execute(Close).StartingIn((long)(duration.Value * 1000));

            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target != closeButton) Close();
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
                ToastType.Info    => new Color(0.3f, 0.7f, 1.0f),
                ToastType.Success => new Color(0.3f, 0.8f, 0.4f),
                ToastType.Warning => new Color(1.0f, 0.8f, 0.3f),
                ToastType.Error   => new Color(1.0f, 0.3f, 0.3f),
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
                var newOpacity = currentOpacity - (opacityStep * frame);
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