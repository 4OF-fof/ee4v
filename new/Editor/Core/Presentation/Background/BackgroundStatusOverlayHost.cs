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
