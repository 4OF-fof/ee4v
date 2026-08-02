using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Internal.EditorAPI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class PopupActionState
    {
        internal PopupActionState(
            string label,
            Action execute,
            bool enabled = true)
        {
            Label = label ?? string.Empty;
            Execute = execute;
            Enabled = enabled;
        }

        internal string Label { get; }

        internal Action Execute { get; }

        internal bool Enabled { get; }
    }

    internal sealed class PopupLayout : VisualElement
    {
        private const string RootClassName =
            "ee4v-popup-layout";
        private const string ContentClassName =
            "ee4v-popup-layout__content";
        private const string FooterClassName =
            "ee4v-popup-layout__footer";
        private const string ActionClassName =
            "ee4v-popup-layout__action";

        internal PopupLayout(
            VisualElement content,
            PopupActionState cancelAction,
            PopupActionState primaryAction)
        {
            AddToClassList(RootClassName);
            var body = content ?? new VisualElement();
            body.AddToClassList(ContentClassName);
            Add(body);

            Footer = new VisualElement();
            Footer.AddToClassList(FooterClassName);
            AddAction(
                Footer,
                CreateAction(
                    cancelAction,
                    UiButtonVariant.Ghost));
            _primaryAction = CreateAction(
                primaryAction,
                UiButtonVariant.Solid);
            AddAction(Footer, _primaryAction);
            Add(Footer);
        }

        private readonly UiButton _primaryAction;

        internal VisualElement Footer { get; }

        internal void SetPrimaryActionEnabled(bool enabled)
        {
            _primaryAction?.SetInteractable(enabled);
        }

        private static UiButton CreateAction(
            PopupActionState state,
            UiButtonVariant variant)
        {
            return state == null
                ? null
                : new UiButton(
                    new UiButtonState(
                        state.Label,
                        enabled: state.Enabled,
                        variant: variant),
                    state.Execute);
        }

        private static void AddAction(
            VisualElement footer,
            UiButton action)
        {
            if (action == null)
            {
                return;
            }

            action.AddToClassList(ActionClassName);
            footer.Add(action);
        }
    }

    public sealed class DecorationRecentIconSession
    {
        private readonly List<string> _iconGuids;

        public DecorationRecentIconSession(
            IReadOnlyList<string> iconGuids)
        {
            _iconGuids = iconGuids == null
                ? new List<string>()
                : new List<string>(iconGuids);
        }

        public IReadOnlyList<string> IconGuids => _iconGuids;

        public void Remove(string iconGuid)
        {
            DecorationRecentIconHistory.Remove(
                _iconGuids,
                iconGuid);
        }
    }

    public static class DecorationRecentIconHistory
    {
        public static IReadOnlyList<string> Snapshot(
            IReadOnlyList<string> iconGuids)
        {
            return iconGuids == null
                ? Array.Empty<string>()
                : iconGuids.ToArray();
        }

        public static void Record(
            IList<string> iconGuids,
            string iconGuid,
            int maximumCount)
        {
            if (iconGuids == null ||
                string.IsNullOrWhiteSpace(iconGuid) ||
                maximumCount <= 0)
            {
                return;
            }

            Remove(iconGuids, iconGuid);
            iconGuids.Insert(0, iconGuid);
            while (iconGuids.Count > maximumCount)
            {
                iconGuids.RemoveAt(iconGuids.Count - 1);
            }
        }

        public static bool Remove(
            IList<string> iconGuids,
            string iconGuid)
        {
            if (iconGuids == null ||
                string.IsNullOrWhiteSpace(iconGuid))
            {
                return false;
            }

            var removed = false;
            for (var i = iconGuids.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(
                        iconGuids[i],
                        iconGuid,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                iconGuids.RemoveAt(i);
                removed = true;
            }

            return removed;
        }
    }

    public static class PopupWindowLayout
    {
        public static Rect ClampToDesktop(
            Vector2 anchor,
            Vector2 size,
            Rect desktopBounds)
        {
            var maximumX = Mathf.Max(
                desktopBounds.xMin,
                desktopBounds.xMax - size.x);
            var maximumY = Mathf.Max(
                desktopBounds.yMin,
                desktopBounds.yMax - size.y);
            return new Rect(
                Mathf.Clamp(
                    anchor.x,
                    desktopBounds.xMin,
                    maximumX),
                Mathf.Clamp(
                    anchor.y,
                    desktopBounds.yMin,
                    maximumY),
                size.x,
                size.y);
        }
    }

    public sealed class TransientPopupFocusController :
        IDisposable
    {
        private readonly EditorWindow _owner;
        private bool _watching;

        public TransientPopupFocusController(EditorWindow owner)
        {
            _owner = owner ??
                     throw new ArgumentNullException(nameof(owner));
        }

        public void OnLostFocus()
        {
            EditorApplication.delayCall += EvaluateFocusLoss;
        }

        public void Dispose()
        {
            StopWatching();
            EditorApplication.delayCall -= EvaluateFocusLoss;
        }

        private void EvaluateFocusLoss()
        {
            if (_owner == null)
            {
                return;
            }

            var focused = EditorWindow.focusedWindow;
            if (focused == _owner)
            {
                StopWatching();
                return;
            }

            if (IsTransientWindow(focused))
            {
                StartWatching();
                return;
            }

            _owner.Close();
        }

        private void StartWatching()
        {
            if (_watching)
            {
                return;
            }

            _watching = true;
            EditorApplication.update += WatchFocus;
        }

        private void WatchFocus()
        {
            if (_owner == null)
            {
                StopWatching();
                return;
            }

            var focused = EditorWindow.focusedWindow;
            if (focused == _owner)
            {
                StopWatching();
                return;
            }

            if (IsTransientWindow(focused))
            {
                return;
            }

            StopWatching();
            _owner.Focus();
        }

        private void StopWatching()
        {
            if (!_watching)
            {
                return;
            }

            _watching = false;
            EditorApplication.update -= WatchFocus;
        }

        private static bool IsTransientWindow(EditorWindow focused)
        {
            return EditorPopupWindow.IsTransientPicker(focused) ||
                   EditorPopupWindow.HasOpenTransientPicker() ||
                   EditorPopupWindow.IsEyeDropperOpen() ||
                   focused is ImageTooltipWindow;
        }
    }
}
