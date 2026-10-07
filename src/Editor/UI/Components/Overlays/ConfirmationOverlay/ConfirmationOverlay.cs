using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class ConfirmationOverlay : VisualElement
    {
        private readonly (VisualElement Element, bool Enabled)[] _background;
        private readonly VisualElement _previousFocus;
        private bool _closed;

        public ConfirmationOverlay(VisualElement host, MessagePanelState state)
        {
            if (host == null) { throw new ArgumentNullException(nameof(host)); }
            _previousFocus = host.panel?.focusController?.focusedElement as VisualElement;
            _background = host.Children().Select(element => (element, element.enabledSelf)).ToArray();
            focusable = true;
            tabIndex = -1;
            UiComposition.Prepare(this);
            AddToClassList("ee4v-ui-confirmation-overlay");
            Notification = new MessagePanel(state);
            Notification.AddToClassList("ee4v-ui-confirmation-overlay__card");
            var content = Notification.TitleText.parent;
            var heading = new VisualElement();
            heading.AddToClassList("ee4v-ui-confirmation-overlay__heading");
            heading.Add(Notification.Children().OfType<Icon>().Single());
            heading.Add(Notification.TitleText);
            content.Insert(0, heading);
            Notification.Add(Notification.Actions);
            SetState(state);
            Add(Notification);
            RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) { Close(); evt.PreventDefault(); }
                evt.StopPropagation();
            });
            RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == this) { Close(); } });
            foreach (var item in _background) { item.Element.SetEnabled(false); }
            host.Add(this);
            schedule.Execute(() => { if (!_closed) { Focus(); } });
        }

        public MessagePanel Notification { get; }
        public event Action Closed;

        public void SetState(MessagePanelState state)
        {
            Notification.SetState(state);
            Notification.TitleText.SetColor(UiColorTokens.TextPrimary);
        }

        public UiButton AddDiscardAction(string label, Action action)
        {
            var button = new UiButton(label, action);
            button.AddToClassList("ee4v-ui-confirmation-overlay__discard");
            button.SetLabelColor(UiColorTokens.TextOnState);
            Notification.Actions.Add(button);
            return button;
        }

        public void Close()
        {
            if (_closed) { return; }
            _closed = true;
            foreach (var item in _background) { item.Element.SetEnabled(item.Enabled); }
            RemoveFromHierarchy();
            Closed?.Invoke();
            if (_previousFocus?.panel != null && _previousFocus.enabledInHierarchy) { _previousFocus.Focus(); }
        }
    }
}
