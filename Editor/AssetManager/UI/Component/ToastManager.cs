using System.Collections.Generic;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Component {
    public class ToastManager {
        private readonly List<Toast> _activeToasts = new();
        private readonly VisualElement _container;

        public ToastManager(VisualElement rootElement) {
            _container = new VisualElement {
                name = "toast-container",
                pickingMode = PickingMode.Ignore,
                style = {
                    position = Position.Absolute,
                    top = 10,
                    right = 10,
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.FlexEnd
                }
            };
            rootElement.Add(_container);
        }

        public void Show(string message, float? duration = 3f, ToastType type = ToastType.Info) {
            var toast = new Toast(message, duration);
            toast.SetType(type);
            toast.OnClosed += OnToastClosed;
            toast.pickingMode = PickingMode.Position;

            _container.Insert(0, toast);
            _activeToasts.Insert(0, toast);
        }

        private void OnToastClosed(Toast toast) {
            _activeToasts.Remove(toast);
        }

        public void ClearAll() {
            foreach (var toast in _activeToasts.ToArray()) toast.RemoveFromHierarchy();
            _activeToasts.Clear();
        }
    }
}