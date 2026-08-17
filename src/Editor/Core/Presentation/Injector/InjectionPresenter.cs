using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Internal.EditorAPI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Injector
{
    internal sealed class InjectionPresenter
    {
        private const string ProjectToolbarHostName =
            "ee4v-project-toolbar-host";

        private readonly IInjectionRegistry _registry;
        private readonly Dictionary<int, int> _projectHostVersions =
            new Dictionary<int, int>();

        private ItemInjectionRegistration[] _hierarchyItemRegistrations =
            Array.Empty<ItemInjectionRegistration>();
        private ItemInjectionRegistration[] _projectItemRegistrations =
            Array.Empty<ItemInjectionRegistration>();
        private VisualElementInjectionRegistration[]
            _projectToolbarRegistrations =
                Array.Empty<VisualElementInjectionRegistration>();
        private bool _hostsDirty = true;
        private int _hostVersion;
        private double _nextHostSyncAt;

        public InjectionPresenter(IInjectionRegistry registry)
        {
            _registry = registry ??
                throw new ArgumentNullException(nameof(registry));
            _registry.Changed += OnRegistryChanged;
            RefreshRegistrationCaches();
        }

        public void Repaint(InjectionChannel channel)
        {
            if (channel == InjectionChannel.ProjectToolbar)
            {
                MarkHostsDirty();
            }

            if (channel == InjectionChannel.HierarchyItem)
            {
                EditorApplication.RepaintHierarchyWindow();
            }

            if (channel == InjectionChannel.ProjectItem ||
                channel == InjectionChannel.ProjectToolbar)
            {
                EditorApplication.RepaintProjectWindow();
            }
        }

        public void DrawHierarchyItem(int instanceId, Rect selectionRect)
        {
            ItemInjectionContext context = null;
            for (var i = 0; i < _hierarchyItemRegistrations.Length; i++)
            {
                var registration = _hierarchyItemRegistrations[i];
                if (!registration.IsEnabled())
                {
                    continue;
                }

                if (context == null)
                {
                    context = new ItemInjectionContext(
                        InjectionChannel.HierarchyItem,
                        instanceId,
                        null,
                        selectionRect);
                }

                registration.Draw(context);
            }
        }

        public void DrawProjectItem(string guid, Rect selectionRect)
        {
            ItemInjectionContext context = null;
            for (var i = 0; i < _projectItemRegistrations.Length; i++)
            {
                var registration = _projectItemRegistrations[i];
                if (!registration.IsEnabled())
                {
                    continue;
                }

                if (context == null)
                {
                    context = new ItemInjectionContext(
                        InjectionChannel.ProjectItem,
                        0,
                        guid,
                        selectionRect);
                }

                registration.Draw(context);
            }
        }

        public void UpdateVisualHosts()
        {
            if (!_hostsDirty &&
                EditorApplication.timeSinceStartup < _nextHostSyncAt)
            {
                return;
            }

            _hostsDirty = false;
            _nextHostSyncAt = EditorApplication.timeSinceStartup + 1d;
            SyncProjectToolbarHosts();
        }

        public void ResetState()
        {
            _projectHostVersions.Clear();
            _hostsDirty = true;
            _hostVersion = 0;
            _nextHostSyncAt = 0d;
            RefreshRegistrationCaches();
        }

        private void SyncProjectToolbarHosts()
        {
            if (!ProjectBrowser.TryGetOpenWindows(out var windows))
            {
                return;
            }

            var activeIds = new HashSet<int>();
            for (var i = 0; i < windows.Count; i++)
            {
                var window = windows[i];
                if (window == null)
                {
                    continue;
                }

                var windowId = window.GetInstanceID();
                activeIds.Add(windowId);

                var root = window.rootVisualElement;
                var host = root.Q<VisualElement>(
                    ProjectToolbarHostName);
                if (host == null)
                {
                    host = CreateHost(ProjectToolbarHostName);
                    root.Add(host);
                }

                if (!_projectHostVersions.TryGetValue(
                        windowId,
                        out var currentVersion) ||
                    currentVersion != _hostVersion)
                {
                    RebuildHost(
                        host,
                        window,
                        InjectionChannel.ProjectToolbar);
                    _projectHostVersions[windowId] = _hostVersion;
                }
            }

            var staleIds = _projectHostVersions.Keys
                .Where(id => !activeIds.Contains(id))
                .ToArray();
            for (var i = 0; i < staleIds.Length; i++)
            {
                _projectHostVersions.Remove(staleIds[i]);
            }
        }

        private static VisualElement CreateHost(string hostName)
        {
            var host = new VisualElement
            {
                name = hostName
            };
            host.style.flexDirection = FlexDirection.Row;
            host.style.height = 20f;
            host.style.marginLeft = 36f;
            host.style.marginRight = 470f;
            host.style.overflow = Overflow.Hidden;
            return host;
        }

        private void RebuildHost(
            VisualElement host,
            EditorWindow window,
            InjectionChannel channel)
        {
            host.Clear();

            var registrations = channel == InjectionChannel.ProjectToolbar
                ? _projectToolbarRegistrations
                : Array.Empty<VisualElementInjectionRegistration>();
            VisualHostContext context = null;
            var hasEnabledRegistration = false;

            for (var i = 0; i < registrations.Length; i++)
            {
                var registration = registrations[i];
                if (!registration.IsEnabled())
                {
                    continue;
                }

                if (context == null)
                {
                    context = new VisualHostContext(channel, window);
                }

                hasEnabledRegistration = true;
                var element = registration.CreateElement(context);
                if (element != null)
                {
                    host.Add(element);
                }
            }

            host.style.display = hasEnabledRegistration
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void OnRegistryChanged(
            object sender,
            InjectionRegistryChangedEventArgs args)
        {
            RefreshRegistrationCaches();
            Repaint(args.Channel);
        }

        private void MarkHostsDirty()
        {
            _hostVersion++;
            _hostsDirty = true;
        }

        private void RefreshRegistrationCaches()
        {
            _hierarchyItemRegistrations = GetRegistrations<
                ItemInjectionRegistration>(
                InjectionChannel.HierarchyItem);
            _projectItemRegistrations = GetRegistrations<
                ItemInjectionRegistration>(
                InjectionChannel.ProjectItem);
            _projectToolbarRegistrations = GetRegistrations<
                VisualElementInjectionRegistration>(
                InjectionChannel.ProjectToolbar);
        }

        private TRegistration[] GetRegistrations<TRegistration>(
            InjectionChannel channel)
            where TRegistration : class, IInjectionRegistration
        {
            return _registry.GetRegistrations(channel)
                .OfType<TRegistration>()
                .ToArray();
        }
    }
}
