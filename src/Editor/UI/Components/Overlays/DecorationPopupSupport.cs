using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Internal.EditorAPI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.UI
{
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
