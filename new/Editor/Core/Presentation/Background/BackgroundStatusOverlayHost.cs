using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.Core.Background
{
    internal sealed class BackgroundStatusOverlayHost : VisualElement
    {
        private const string HostClassName = "ee4v-ui-status-overlay-host";
        private readonly StatusOverlay _overlay;

        public BackgroundStatusOverlayHost()
        {
            name = BackgroundStatusOverlayApi.HostElementName;
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
            _overlay.SetState(new StatusOverlayState(
                activity.IsActive,
                activity.Message));
        }
    }

    internal static class BackgroundStatusOverlayApi
    {
        internal const string HostElementName = "ee4v-status-overlay-host";
        private const string RootClassName = "ee4v-ui";
        private static readonly Dictionary<int, HostRegistration> Hosts =
            new Dictionary<int, HostRegistration>();

        internal static int HostCount => Hosts.Count;

        public static void EnsureHost(EditorWindow window)
        {
            if (window == null || window.rootVisualElement == null)
            {
                return;
            }

            var root = window.rootVisualElement;
            root.AddToClassList(RootClassName);
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Overlays/StatusOverlay/status-overlay.uss");

            var host = root.Q<BackgroundStatusOverlayHost>(HostElementName);
            if (host == null)
            {
                host = new BackgroundStatusOverlayHost();
                var windowId = window.GetInstanceID();
                var registeredHost = host;
                host.RegisterCallback<DetachFromPanelEvent>(
                    _ => RemoveHost(windowId, registeredHost));
                root.Add(host);
            }

            Hosts[window.GetInstanceID()] = new HostRegistration(window, host);
            host.Refresh();
        }

        internal static void ReleaseHost(EditorWindow window)
        {
            if (window == null)
            {
                return;
            }

            var windowId = window.GetInstanceID();
            if (!Hosts.TryGetValue(windowId, out var registration))
            {
                return;
            }

            Hosts.Remove(windowId);
            var host = registration.Host;
            if (host != null && host.parent != null)
            {
                host.RemoveFromHierarchy();
            }
        }

        internal static void ResetAllHosts()
        {
            var registrations = new List<HostRegistration>(Hosts.Values);
            Hosts.Clear();
            for (var i = 0; i < registrations.Count; i++)
            {
                var host = registrations[i].Host;
                if (host != null && host.parent != null)
                {
                    host.RemoveFromHierarchy();
                }
            }
        }

        private static void RemoveHost(
            int windowId,
            BackgroundStatusOverlayHost host)
        {
            if (Hosts.TryGetValue(windowId, out var registration) &&
                ReferenceEquals(registration.Host, host))
            {
                Hosts.Remove(windowId);
            }
        }

        private sealed class HostRegistration
        {
            public HostRegistration(
                EditorWindow window,
                BackgroundStatusOverlayHost host)
            {
                Window = window;
                Host = host;
            }

            public EditorWindow Window { get; }

            public BackgroundStatusOverlayHost Host { get; }
        }
    }
}
