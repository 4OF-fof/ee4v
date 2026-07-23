using System.Collections.Generic;
using Ee4v.Core.Background;
using UnityEditor;
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

    internal sealed class StatusOverlayHost : VisualElement
    {
        private const string HostClassName = "ee4v-ui-status-overlay-host";
        private readonly StatusOverlay _overlay;

        public StatusOverlayHost()
        {
            name = StatusOverlayApi.HostElementName;
            AddToClassList(HostClassName);
            pickingMode = PickingMode.Ignore;
            _overlay = new StatusOverlay();
            Add(_overlay);
            schedule.Execute(Refresh).Every(100);
            Refresh();
        }

        internal void Refresh()
        {
            var activity = CoreBackgroundActivities.Current.GetState();
            _overlay.SetState(new StatusOverlayState(activity.IsActive, activity.Message));
        }
    }

    internal static class StatusOverlayApi
    {
        internal const string HostElementName = "ee4v-status-overlay-host";
        private const string RootClassName = "ee4v-ui";
        private static readonly Dictionary<int, HostRegistration> Hosts = new Dictionary<int, HostRegistration>();

        public static void EnsureHost(EditorWindow window)
        {
            if (window == null || window.rootVisualElement == null)
            {
                return;
            }

            var root = window.rootVisualElement;
            root.AddToClassList(RootClassName);
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Overlays/StatusOverlay/status-overlay.uss");

            var host = root.Q<StatusOverlayHost>(HostElementName);
            if (host == null)
            {
                host = new StatusOverlayHost();
                root.Add(host);
            }

            Hosts[window.GetInstanceID()] = new HostRegistration(window, host);
            host.Refresh();
        }

        internal static void ResetAllHosts()
        {
            foreach (var pair in Hosts)
            {
                var host = pair.Value.Host;
                if (host != null && host.parent != null)
                {
                    host.RemoveFromHierarchy();
                }
            }

            Hosts.Clear();
        }

        private sealed class HostRegistration
        {
            public HostRegistration(EditorWindow window, StatusOverlayHost host)
            {
                Window = window;
                Host = host;
            }

            public EditorWindow Window { get; }

            public StatusOverlayHost Host { get; }
        }
    }
}
