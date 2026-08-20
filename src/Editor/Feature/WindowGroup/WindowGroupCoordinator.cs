using System;

namespace Ee4v.WindowGroup
{
    internal sealed class WindowGroupCoordinator<TWindow>
        where TWindow : class
    {
        private readonly WindowGroupRegistry<TWindow> _registry;
        private readonly Action<TWindow> _focus;
        private TWindow _lastFocusedWindow;
        private string _activeGroupId;

        internal WindowGroupCoordinator(
            WindowGroupRegistry<TWindow> registry,
            Action<TWindow> focus)
        {
            _registry = registry ??
                throw new ArgumentNullException(nameof(registry));
            _focus = focus ??
                throw new ArgumentNullException(nameof(focus));
        }

        internal void ProcessFocus(TWindow focusedWindow)
        {
            if (ReferenceEquals(
                    focusedWindow,
                    _lastFocusedWindow))
            {
                return;
            }

            _lastFocusedWindow = focusedWindow;
            if (focusedWindow == null ||
                !_registry.TryGetGroupId(
                    focusedWindow,
                    out var groupId))
            {
                _activeGroupId = null;
                return;
            }

            if (string.Equals(
                    _activeGroupId,
                    groupId,
                    StringComparison.Ordinal))
            {
                return;
            }

            _activeGroupId = groupId;
            var windows = _registry.GetWindows(groupId);
            if (windows.Count < 2)
            {
                return;
            }

            foreach (var window in windows)
            {
                if (!ReferenceEquals(window, focusedWindow))
                {
                    _focus(window);
                }
            }

            _focus(focusedWindow);
        }

        internal void Reset()
        {
            _lastFocusedWindow = null;
            _activeGroupId = null;
        }
    }
}
