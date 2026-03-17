using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Injector
{
    [InitializeOnLoad]
    public static class InjectorApi
    {
        private const string HierarchyHeaderHostName = "ee4v-hierarchy-header-host";
        private const string ProjectToolbarHostName = "ee4v-project-toolbar-host";

        private static readonly Type HierarchyWindowType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        private static readonly Type ProjectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
        private static readonly List<InjectionRegistration> Registrations = new List<InjectionRegistration>();
        private static readonly Dictionary<int, int> HierarchyHostVersions = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> ProjectHostVersions = new Dictionary<int, int>();

        private static bool _hostsDirty = true;
        private static int _hostVersion;
        private static double _nextHostSyncAt;

        static InjectorApi()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyItemGui;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGui;

            EditorApplication.projectWindowItemOnGUI -= OnProjectItemGui;
            EditorApplication.projectWindowItemOnGUI += OnProjectItemGui;

            EditorApplication.update -= UpdateVisualHosts;
            EditorApplication.update += UpdateVisualHosts;
        }

        public static void Register(InjectionRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            var index = Registrations.FindIndex(existing => existing.Id == registration.Id && existing.Channel == registration.Channel);
            if (index >= 0)
            {
                Registrations[index] = registration;
            }
            else
            {
                Registrations.Add(registration);
            }

            Registrations.Sort(CompareRegistrations);
            MarkHostsDirty();
            Repaint(registration.Channel);
        }

        public static void Repaint(InjectionChannel channel)
        {
            if (channel == InjectionChannel.HierarchyHeader || channel == InjectionChannel.ProjectToolbar)
            {
                MarkHostsDirty();
            }

            if (channel == InjectionChannel.HierarchyItem || channel == InjectionChannel.HierarchyHeader)
            {
                EditorApplication.RepaintHierarchyWindow();
            }

            if (channel == InjectionChannel.ProjectItem || channel == InjectionChannel.ProjectToolbar)
            {
                EditorApplication.RepaintProjectWindow();
            }
        }

        private static void MarkHostsDirty()
        {
            _hostVersion++;
            _hostsDirty = true;
        }

        private static void OnHierarchyItemGui(int instanceId, Rect selectionRect)
        {
            var active = Registrations.OfType<ItemInjectionRegistration>()
                .Where(registration => registration.Channel == InjectionChannel.HierarchyItem && IsEnabled(registration))
                .ToArray();

            if (active.Length == 0)
            {
                return;
            }

            var context = new ItemInjectionContext(InjectionChannel.HierarchyItem, instanceId, null, selectionRect);
            for (var i = 0; i < active.Length; i++)
            {
                active[i].Draw(context);
            }
        }

        private static void OnProjectItemGui(string guid, Rect selectionRect)
        {
            var active = Registrations.OfType<ItemInjectionRegistration>()
                .Where(registration => registration.Channel == InjectionChannel.ProjectItem && IsEnabled(registration))
                .ToArray();

            if (active.Length == 0)
            {
                return;
            }

            var context = new ItemInjectionContext(InjectionChannel.ProjectItem, 0, guid, selectionRect);
            for (var i = 0; i < active.Length; i++)
            {
                active[i].Draw(context);
            }
        }

        private static void UpdateVisualHosts()
        {
            if (!_hostsDirty && EditorApplication.timeSinceStartup < _nextHostSyncAt)
            {
                return;
            }

            _hostsDirty = false;
            _nextHostSyncAt = EditorApplication.timeSinceStartup + 1d;

            SyncWindowHosts(HierarchyWindowType, HierarchyHeaderHostName, InjectionChannel.HierarchyHeader, HierarchyHostVersions);
            SyncWindowHosts(ProjectBrowserType, ProjectToolbarHostName, InjectionChannel.ProjectToolbar, ProjectHostVersions);
        }

        private static void SyncWindowHosts(
            Type windowType,
            string hostName,
            InjectionChannel channel,
            IDictionary<int, int> knownVersions)
        {
            if (windowType == null)
            {
                return;
            }

            var activeIds = new HashSet<int>();
            var windows = Resources.FindObjectsOfTypeAll(windowType);
            for (var i = 0; i < windows.Length; i++)
            {
                var window = windows[i] as EditorWindow;
                if (window == null)
                {
                    continue;
                }

                activeIds.Add(window.GetInstanceID());

                var root = window.rootVisualElement;
                var host = root.Q<VisualElement>(hostName);
                if (host == null)
                {
                    host = CreateHost(hostName, channel);
                    root.Insert(0, host);
                }

                int currentVersion;
                if (!knownVersions.TryGetValue(window.GetInstanceID(), out currentVersion) || currentVersion != _hostVersion)
                {
                    RebuildHost(host, window, channel);
                    knownVersions[window.GetInstanceID()] = _hostVersion;
                }
            }

            var staleIds = knownVersions.Keys.Where(id => !activeIds.Contains(id)).ToArray();
            for (var i = 0; i < staleIds.Length; i++)
            {
                knownVersions.Remove(staleIds[i]);
            }
        }

        private static VisualElement CreateHost(string hostName, InjectionChannel channel)
        {
            var host = new VisualElement
            {
                name = hostName
            };

            host.style.flexDirection = FlexDirection.Row;
            host.style.alignItems = Align.Center;
            host.style.paddingLeft = 6f;
            host.style.paddingRight = 6f;
            host.style.marginBottom = 2f;
            host.style.height = channel == InjectionChannel.HierarchyHeader ? 22f : 24f;
            host.style.backgroundColor = channel == InjectionChannel.HierarchyHeader
                ? new Color(0.16f, 0.18f, 0.22f, 0.95f)
                : new Color(0.12f, 0.13f, 0.15f, 0.95f);

            return host;
        }

        private static void RebuildHost(VisualElement host, EditorWindow window, InjectionChannel channel)
        {
            host.Clear();

            var active = Registrations.OfType<VisualElementInjectionRegistration>()
                .Where(registration => registration.Channel == channel && IsEnabled(registration))
                .ToArray();

            if (active.Length == 0)
            {
                host.style.display = DisplayStyle.None;
                return;
            }

            host.style.display = DisplayStyle.Flex;
            var context = new VisualHostContext(channel, window);
            for (var i = 0; i < active.Length; i++)
            {
                var element = active[i].CreateElement(context);
                if (element != null)
                {
                    host.Add(element);
                }
            }
        }

        private static bool IsEnabled(InjectionRegistration registration)
        {
            return registration.IsEnabled == null || registration.IsEnabled();
        }

        private static int CompareRegistrations(InjectionRegistration left, InjectionRegistration right)
        {
            var channelCompare = left.Channel.CompareTo(right.Channel);
            if (channelCompare != 0)
            {
                return channelCompare;
            }

            var priorityCompare = left.Priority.CompareTo(right.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }
    }
}
