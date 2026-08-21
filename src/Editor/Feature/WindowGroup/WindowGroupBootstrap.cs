using System;
using UnityEditor;

namespace Ee4v.WindowGroup
{
    [InitializeOnLoad]
    internal static class WindowGroupBootstrap
    {
        private static readonly WindowGroupRegistry<EditorWindow> Registry =
            new WindowGroupRegistry<EditorWindow>();

        private static readonly WindowGroupConfiguration Configuration =
            new WindowGroupConfiguration(
                new EditorPrefsWindowGroupStore());

        private static readonly WindowGroupCoordinator<EditorWindow>
            Coordinator = new WindowGroupCoordinator<EditorWindow>(
                Registry,
                Focus);

        private static EditorWindow _lastConfiguredFocus;

        static WindowGroupBootstrap()
        {
            Configuration.Changed += OnConfigurationChanged;
            ApplyConfiguredMemberships();
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        internal static WindowGroupConfiguration Settings
        {
            get { return Configuration; }
        }

        internal static IDisposable Register(
            EditorWindow window,
            string groupId,
            bool isFollower)
        {
            var registration = Registry.Register(
                window,
                groupId,
                isFollower);
            Coordinator.Reset();
            return registration;
        }

        private static void Update()
        {
            Registry.RemoveWhere(window => window == null);
            var focusedWindow = EditorWindow.focusedWindow;
            if (!ReferenceEquals(
                    focusedWindow,
                    _lastConfiguredFocus))
            {
                _lastConfiguredFocus = focusedWindow;
                ApplyConfiguredMemberships();
            }

            Coordinator.ProcessFocus(focusedWindow);
        }

        private static void Focus(EditorWindow window)
        {
            if (window != null)
            {
                window.Focus();
            }
        }

        private static void OnConfigurationChanged()
        {
            ApplyConfiguredMemberships();
            Coordinator.Reset();
        }

        private static void ApplyConfiguredMemberships()
        {
            var windows = UnityWindowCatalog.GetOpenWindows();
            for (var i = 0; i < windows.Count; i++)
            {
                var window = windows[i];
                var typeId = WindowTypeIdentity.GetId(
                    window.GetType());
                Registry.SetManaged(
                    window,
                    Configuration.GetMemberships(typeId));
            }
        }
    }
}
