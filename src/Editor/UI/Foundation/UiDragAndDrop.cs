using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public static class UiDragAndDrop
    {
        private const float StartDistance = 4f;

        public static void RegisterStart<T>(
            VisualElement element,
            string dataKey,
            Func<T> createPayload,
            Func<T, string> getLabel)
            where T : class
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            var start = Vector2.zero;
            var ready = false;
            element.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != (int)MouseButton.LeftMouse)
                {
                    return;
                }

                start = evt.mousePosition;
                ready = true;
            });
            element.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!ready ||
                    (evt.pressedButtons & 1) == 0 ||
                    Vector2.Distance(start, evt.mousePosition) <
                    StartDistance)
                {
                    return;
                }

                ready = false;
                var payload = createPayload?.Invoke();
                if (payload == null)
                {
                    return;
                }

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(dataKey, payload);
                DragAndDrop.StartDrag(
                    getLabel?.Invoke(payload) ?? string.Empty);
                evt.StopPropagation();
            });
            element.RegisterCallback<MouseUpEvent>(_ => ready = false);
        }

        public static void RegisterMoveTarget<T>(
            VisualElement element,
            string dataKey,
            Func<T, bool> canDrop,
            Action<T> onDrop,
            Action<bool> setActive)
            where T : class
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            bool TryGetPayload(out T payload)
            {
                payload = DragAndDrop.GetGenericData(dataKey) as T;
                return payload != null &&
                       (canDrop?.Invoke(payload) ?? true);
            }

            element.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!TryGetPayload(out _))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                setActive?.Invoke(true);
                evt.StopPropagation();
            });
            element.RegisterCallback<DragLeaveEvent>(_ =>
                setActive?.Invoke(false));
            element.RegisterCallback<DragExitedEvent>(_ =>
                setActive?.Invoke(false));
            element.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!TryGetPayload(out var payload))
                {
                    return;
                }

                setActive?.Invoke(false);
                DragAndDrop.AcceptDrag();
                onDrop?.Invoke(payload);
                evt.StopPropagation();
            });
        }
    }
}
