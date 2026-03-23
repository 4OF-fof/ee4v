using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum WindowToastTone
    {
        Info,
        Success,
        Warning,
        Error
    }

    internal sealed class WindowToastAction
    {
        public WindowToastAction(string label, Action onClick = null, bool closesToast = true)
        {
            Label = label ?? string.Empty;
            OnClick = onClick;
            ClosesToast = closesToast;
        }

        public string Label { get; }

        public Action OnClick { get; }

        public bool ClosesToast { get; }
    }

    internal sealed class WindowToastRequest
    {
        public WindowToastRequest(
            WindowToastTone tone,
            string title,
            string message,
            double? durationSeconds = null,
            bool dismissible = true,
            IReadOnlyList<WindowToastAction> actions = null)
        {
            Tone = tone;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            DurationSeconds = durationSeconds;
            Dismissible = dismissible;
            Actions = actions ?? Array.Empty<WindowToastAction>();
        }

        public WindowToastTone Tone { get; }

        public string Title { get; }

        public string Message { get; }

        public double? DurationSeconds { get; }

        public bool Dismissible { get; }

        public IReadOnlyList<WindowToastAction> Actions { get; }
    }

    internal sealed class WindowToastState
    {
        public WindowToastState(
            WindowToastTone tone,
            string title,
            string message,
            bool dismissible,
            IReadOnlyList<WindowToastAction> actions)
        {
            Tone = tone;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            Dismissible = dismissible;
            Actions = actions ?? Array.Empty<WindowToastAction>();
        }

        public WindowToastTone Tone { get; }

        public string Title { get; }

        public string Message { get; }

        public bool Dismissible { get; }

        public IReadOnlyList<WindowToastAction> Actions { get; }
    }

    internal sealed class WindowToast : VisualElement
    {
        private readonly UiTextElement _titleLabel;
        private readonly UiTextElement _messageLabel;
        private readonly Button _closeButton;
        private readonly Icon _closeIcon;
        private readonly VisualElement _actionsRow;
        private WindowToastState _state;

        public WindowToast(WindowToastState state = null)
        {
            AddToClassList(UiClassNames.WindowToast);

            var header = new VisualElement();
            header.AddToClassList(UiClassNames.WindowToastHeader);

            var headerText = new VisualElement();
            headerText.AddToClassList(UiClassNames.WindowToastHeaderText);

            _titleLabel = UiTextFactory.Create(string.Empty, UiClassNames.WindowToastTitle);
            _messageLabel = UiTextFactory.Create(string.Empty, UiClassNames.WindowToastMessage);
            _messageLabel.SetWhiteSpace(WhiteSpace.Normal);

            headerText.Add(_titleLabel);
            header.Add(headerText);

            _closeButton = new Button(RequestDismiss);
            _closeButton.AddToClassList(UiClassNames.WindowToastCloseButton);
            _closeIcon = new Icon(IconState.FromBuiltinIcon(UiBuiltinIcon.Close, size: 10f, tooltip: "Dismiss"));
            _closeButton.Add(_closeIcon);
            header.Add(_closeButton);

            _actionsRow = new VisualElement();
            _actionsRow.AddToClassList(UiClassNames.WindowToastActions);

            Add(header);
            Add(_messageLabel);
            Add(_actionsRow);

            SetState(state ?? new WindowToastState(WindowToastTone.Info, string.Empty, string.Empty, true, Array.Empty<WindowToastAction>()));
        }

        public event Action<WindowToast> DismissRequested;

        public void SetDismissing(bool isDismissing)
        {
            pickingMode = isDismissing ? PickingMode.Ignore : PickingMode.Position;
            EnableInClassList(UiClassNames.WindowToastDismissing, isDismissing);
        }

        public void SetState(WindowToastState state)
        {
            _state = state ?? new WindowToastState(WindowToastTone.Info, string.Empty, string.Empty, true, Array.Empty<WindowToastAction>());

            _titleLabel.SetText(_state.Title);
            _titleLabel.style.display = string.IsNullOrWhiteSpace(_state.Title) ? DisplayStyle.None : DisplayStyle.Flex;

            _messageLabel.SetText(_state.Message);
            _messageLabel.style.display = string.IsNullOrWhiteSpace(_state.Message) ? DisplayStyle.None : DisplayStyle.Flex;

            _closeButton.style.display = _state.Dismissible ? DisplayStyle.Flex : DisplayStyle.None;

            EnableInClassList(UiClassNames.WindowToastToneInfo, _state.Tone == WindowToastTone.Info);
            EnableInClassList(UiClassNames.WindowToastToneSuccess, _state.Tone == WindowToastTone.Success);
            EnableInClassList(UiClassNames.WindowToastToneWarning, _state.Tone == WindowToastTone.Warning);
            EnableInClassList(UiClassNames.WindowToastToneError, _state.Tone == WindowToastTone.Error);

            RebuildActions();
        }

        private void RebuildActions()
        {
            _actionsRow.Clear();
            for (var i = 0; i < _state.Actions.Count; i++)
            {
                var action = _state.Actions[i];
                if (action == null || string.IsNullOrWhiteSpace(action.Label))
                {
                    continue;
                }

                var button = new Button(() => InvokeAction(action))
                {
                    text = action.Label
                };
                button.AddToClassList(UiClassNames.WindowToastActionButton);
                _actionsRow.Add(button);
            }

            _actionsRow.style.display = _actionsRow.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void InvokeAction(WindowToastAction action)
        {
            action.OnClick?.Invoke();
            if (action.ClosesToast)
            {
                RequestDismiss();
            }
        }

        private void RequestDismiss()
        {
            DismissRequested?.Invoke(this);
        }
    }

    internal sealed class WindowToastHost : VisualElement
    {
        private const double FadeOutDurationSeconds = 0.18d;
        private readonly List<Entry> _entries = new List<Entry>();

        public WindowToastHost()
        {
            name = WindowToastApi.HostElementName;
            AddToClassList(UiClassNames.WindowToastHost);
        }

        internal int ToastCount
        {
            get { return _entries.Count; }
        }

        public void Show(WindowToastRequest request)
        {
            Show(request, EditorApplication.timeSinceStartup);
        }

        internal void Show(WindowToastRequest request, double now)
        {
            if (request == null)
            {
                return;
            }

            var toast = new WindowToast(new WindowToastState(
                request.Tone,
                request.Title,
                request.Message,
                request.Dismissible,
                request.Actions));
            toast.DismissRequested += OnToastDismissRequested;

            _entries.Insert(0, new Entry(toast, ResolveExpiresAt(request, now)));
            Insert(0, toast);
        }

        public void ClearToasts()
        {
            ClearToasts(EditorApplication.timeSinceStartup);
        }

        internal void ClearToasts(double now)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                BeginDismiss(_entries[i], now);
            }
        }

        internal void ClearToastsImmediate()
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                _entries[i].Toast.DismissRequested -= OnToastDismissRequested;
            }

            _entries.Clear();
            Clear();
        }

        internal void Tick(double now)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.IsDismissing)
                {
                    if (entry.RemoveAt.HasValue && now >= entry.RemoveAt.Value)
                    {
                        RemoveEntryAt(i);
                    }

                    continue;
                }

                if (!entry.ExpiresAt.HasValue || now < entry.ExpiresAt.Value)
                {
                    continue;
                }

                BeginDismiss(entry, now);
            }
        }

        private static double? ResolveExpiresAt(WindowToastRequest request, double now)
        {
            if (request == null || request.Tone == WindowToastTone.Error)
            {
                return null;
            }

            if (request.DurationSeconds.HasValue)
            {
                if (request.DurationSeconds.Value <= 0d)
                {
                    return null;
                }

                return now + request.DurationSeconds.Value;
            }

            if (request.Actions != null && request.Actions.Count > 0)
            {
                return null;
            }

            switch (request.Tone)
            {
                case WindowToastTone.Success:
                    return now + 3d;
                case WindowToastTone.Warning:
                    return now + 5d;
                case WindowToastTone.Info:
                default:
                    return now + 4d;
            }
        }

        private void OnToastDismissRequested(WindowToast toast)
        {
            BeginDismiss(toast, EditorApplication.timeSinceStartup);
        }

        private void BeginDismiss(WindowToast toast, double now)
        {
            if (toast == null)
            {
                return;
            }

            for (var i = 0; i < _entries.Count; i++)
            {
                if (!ReferenceEquals(_entries[i].Toast, toast))
                {
                    continue;
                }

                BeginDismiss(_entries[i], now);
                return;
            }
        }

        private void BeginDismiss(Entry entry, double now)
        {
            if (entry == null || entry.IsDismissing)
            {
                return;
            }

            entry.IsDismissing = true;
            entry.RemoveAt = now + FadeOutDurationSeconds;
            entry.Toast.SetDismissing(true);
            entry.Toast.MarkDirtyRepaint();
        }

        private void RemoveEntryAt(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                return;
            }

            var entry = _entries[index];
            entry.Toast.DismissRequested -= OnToastDismissRequested;
            _entries.RemoveAt(index);
            Remove(entry.Toast);
        }

        private sealed class Entry
        {
            public Entry(WindowToast toast, double? expiresAt)
            {
                Toast = toast;
                ExpiresAt = expiresAt;
            }

            public WindowToast Toast { get; }

            public double? ExpiresAt { get; }

            public bool IsDismissing { get; set; }

            public double? RemoveAt { get; set; }
        }
    }

    internal static class WindowToastApi
    {
        internal const string HostElementName = "ee4v-window-toast-host";

        private static readonly Dictionary<int, HostRegistration> Hosts = new Dictionary<int, HostRegistration>();
        private static bool _isSubscribed;

        public static void EnsureHost(EditorWindow window)
        {
            if (window == null)
            {
                return;
            }

            var root = window.rootVisualElement;
            if (root == null)
            {
                return;
            }

            root.AddToClassList(UiClassNames.Root);
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/window-toast.uss");

            var host = root.Q<WindowToastHost>(HostElementName);
            if (host == null)
            {
                host = new WindowToastHost();
                root.Add(host);
            }

            Hosts[window.GetInstanceID()] = new HostRegistration(window, host);
            Subscribe();
        }

        public static void Show(EditorWindow window, WindowToastRequest request)
        {
            if (window == null || request == null)
            {
                return;
            }

            EnsureHost(window);

            if (Hosts.TryGetValue(window.GetInstanceID(), out var registration))
            {
                registration.Host.Show(request, EditorApplication.timeSinceStartup);
                window.Repaint();
            }
        }

        public static void Clear(EditorWindow window)
        {
            if (window == null)
            {
                return;
            }

            if (Hosts.TryGetValue(window.GetInstanceID(), out var registration))
            {
                registration.Host.ClearToasts(EditorApplication.timeSinceStartup);
                window.Repaint();
            }
        }

        internal static void ResetAllHosts()
        {
            foreach (var pair in Hosts)
            {
                pair.Value.Host.ClearToastsImmediate();
            }

            Hosts.Clear();
            if (_isSubscribed)
            {
                EditorApplication.update -= OnEditorUpdate;
                _isSubscribed = false;
            }
        }

        private static void Subscribe()
        {
            if (_isSubscribed)
            {
                return;
            }

            _isSubscribed = true;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (Hosts.Count == 0)
            {
                EditorApplication.update -= OnEditorUpdate;
                _isSubscribed = false;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            List<int> staleIds = null;
            foreach (var pair in Hosts)
            {
                var registration = pair.Value;
                var host = registration.Host;
                var window = registration.Window;
                if (window == null || host == null || host.panel == null || host.parent == null)
                {
                    if (staleIds == null)
                    {
                        staleIds = new List<int>();
                    }

                    staleIds.Add(pair.Key);
                    continue;
                }

                host.Tick(now);
                window.Repaint();
            }

            if (staleIds == null)
            {
                return;
            }

            for (var i = 0; i < staleIds.Count; i++)
            {
                Hosts.Remove(staleIds[i]);
            }
        }

        private sealed class HostRegistration
        {
            public HostRegistration(EditorWindow window, WindowToastHost host)
            {
                Window = window;
                Host = host;
            }

            public EditorWindow Window { get; }

            public WindowToastHost Host { get; }
        }
    }
}
