using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class StatusOverlayState
    {
        public StatusOverlayState(bool visible, string message)
        {
            Visible = visible;
            Message = message ?? string.Empty;
        }

        public bool Visible { get; }

        public string Message { get; }
    }

    internal sealed class StatusOverlay : VisualElement
    {
        private const string RootClassName = "ee4v-ui-status-overlay";
        private const string SpinnerClassName = "ee4v-ui-status-overlay__spinner";
        private const string MessageClassName = "ee4v-ui-status-overlay__message";
        private readonly VisualElement _spinner;
        private readonly UiTextElement _message;
        private float _rotation;

        public StatusOverlay(StatusOverlayState state = null)
        {
            AddToClassList(RootClassName);
            pickingMode = PickingMode.Ignore;

            _spinner = new VisualElement { pickingMode = PickingMode.Ignore };
            _spinner.AddToClassList(SpinnerClassName);
            _message = UiTextFactory.Create(string.Empty, MessageClassName);
            _message.pickingMode = PickingMode.Ignore;
            _message.SetWhiteSpace(WhiteSpace.NoWrap);

            Add(_spinner);
            Add(_message);
            SetState(state ?? new StatusOverlayState(false, string.Empty));
            schedule.Execute(AdvanceSpinner).Every(50);
        }

        public void SetState(StatusOverlayState state)
        {
            state = state ?? new StatusOverlayState(false, string.Empty);
            _message.SetText(state.Message);
            style.display = state.Visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AdvanceSpinner()
        {
            if (resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            _rotation = (_rotation + 24f) % 360f;
            _spinner.transform.rotation = Quaternion.Euler(0f, 0f, _rotation);
        }
    }

}
