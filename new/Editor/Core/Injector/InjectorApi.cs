using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Injector
{
    [InitializeOnLoad]
    public static class InjectorApi
    {
        private const string ProjectToolbarHostName = "ee4v-project-toolbar-host";

        private static readonly Type ProjectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
        private static readonly List<InjectionRegistration> Registrations = new List<InjectionRegistration>();
        private static readonly Dictionary<int, int> ProjectHostVersions = new Dictionary<int, int>();
        private static ItemInjectionRegistration[] _hierarchyItemRegistrations = Array.Empty<ItemInjectionRegistration>();
        private static ItemInjectionRegistration[] _projectItemRegistrations = Array.Empty<ItemInjectionRegistration>();
        private static VisualElementInjectionRegistration[] _projectToolbarRegistrations = Array.Empty<VisualElementInjectionRegistration>();

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
            RefreshRegistrationCaches();
            MarkHostsDirty();
            Repaint(registration.Channel);
        }

        public static void Repaint(InjectionChannel channel)
        {
            if (channel == InjectionChannel.ProjectToolbar)
            {
                MarkHostsDirty();
            }

            if (channel == InjectionChannel.HierarchyItem)
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
            if (_hierarchyItemRegistrations.Length == 0)
            {
                return;
            }

            ItemInjectionContext context = null;
            for (var i = 0; i < _hierarchyItemRegistrations.Length; i++)
            {
                var registration = _hierarchyItemRegistrations[i];
                if (!IsEnabled(registration))
                {
                    continue;
                }

                if (context == null)
                {
                    context = new ItemInjectionContext(InjectionChannel.HierarchyItem, instanceId, null, selectionRect);
                }

                registration.Draw(context);
            }
        }

        private static void OnProjectItemGui(string guid, Rect selectionRect)
        {
            if (_projectItemRegistrations.Length == 0)
            {
                return;
            }

            ItemInjectionContext context = null;
            for (var i = 0; i < _projectItemRegistrations.Length; i++)
            {
                var registration = _projectItemRegistrations[i];
                if (!IsEnabled(registration))
                {
                    continue;
                }

                if (context == null)
                {
                    context = new ItemInjectionContext(InjectionChannel.ProjectItem, 0, guid, selectionRect);
                }

                registration.Draw(context);
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
                    host = CreateHost(hostName);
                    root.Add(host);
                }

                int currentVersion;
                if (!knownVersions.TryGetValue(window.GetInstanceID(), out currentVersion) || currentVersion != _hostVersion)
                {
                    RebuildHost(host, window, channel);
                    knownVersions[window.GetInstanceID()] = _hostVersion;
                }
            }

            List<int> staleIds = null;
            foreach (var id in knownVersions.Keys)
            {
                if (activeIds.Contains(id))
                {
                    continue;
                }

                if (staleIds == null)
                {
                    staleIds = new List<int>();
                }

                staleIds.Add(id);
            }

            if (staleIds == null)
            {
                return;
            }

            for (var i = 0; i < staleIds.Count; i++)
            {
                knownVersions.Remove(staleIds[i]);
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

        private static void RebuildHost(VisualElement host, EditorWindow window, InjectionChannel channel)
        {
            host.Clear();

            var registrations = GetVisualRegistrations(channel);
            VisualHostContext context = null;
            var hasEnabledRegistration = false;

            for (var i = 0; i < registrations.Length; i++)
            {
                var registration = registrations[i];
                if (!IsEnabled(registration))
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

            host.style.display = hasEnabledRegistration ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsEnabled(InjectionRegistration registration)
        {
            return registration.IsEnabled == null || registration.IsEnabled();
        }

        private static void RefreshRegistrationCaches()
        {
            var hierarchyItems = new List<ItemInjectionRegistration>();
            var projectItems = new List<ItemInjectionRegistration>();
            var projectToolbars = new List<VisualElementInjectionRegistration>();

            for (var i = 0; i < Registrations.Count; i++)
            {
                var registration = Registrations[i];
                if (registration is ItemInjectionRegistration itemRegistration)
                {
                    if (itemRegistration.Channel == InjectionChannel.HierarchyItem)
                    {
                        hierarchyItems.Add(itemRegistration);
                    }
                    else if (itemRegistration.Channel == InjectionChannel.ProjectItem)
                    {
                        projectItems.Add(itemRegistration);
                    }

                    continue;
                }

                var visualRegistration = registration as VisualElementInjectionRegistration;
                if (visualRegistration == null)
                {
                    continue;
                }

                if (visualRegistration.Channel == InjectionChannel.ProjectToolbar)
                {
                    projectToolbars.Add(visualRegistration);
                }
            }

            _hierarchyItemRegistrations = hierarchyItems.ToArray();
            _projectItemRegistrations = projectItems.ToArray();
            _projectToolbarRegistrations = projectToolbars.ToArray();
        }

        private static VisualElementInjectionRegistration[] GetVisualRegistrations(InjectionChannel channel)
        {
            return channel == InjectionChannel.ProjectToolbar
                ? _projectToolbarRegistrations
                : Array.Empty<VisualElementInjectionRegistration>();
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
