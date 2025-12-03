using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.Wraps;
using _4OF.ee4v.ProjectExtension.Toolbar;
using UnityEditor;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension {
    [InitializeOnLoad]
    public static class ProjectToolbarManager {
        private static List<IProjectToolbarComponent> _components;
        private static bool _isInitialized;
        private static EditorWindow _projectWindow;
        private static string _currentFolderPath;
        private static bool _watcherEnabled;

        static ProjectToolbarManager() {
            Initialize();
        }

        private static void Initialize() {
            if (!SettingSingleton.I.enableProjectExtension) return;

            EditorApplication.update -= InitializationCheck;
            EditorApplication.update += InitializationCheck;
        }

        private static void Resolve() {
            if (_components != null) return;

            _components = new List<IProjectToolbarComponent>();

            var types = TypeCache.GetTypesDerivedFrom<IProjectToolbarComponent>()
                .Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in types)
                if (type.GetConstructor(Type.EmptyTypes) != null &&
                    Activator.CreateInstance(type) is IProjectToolbarComponent component)
                    _components.Add(component);

            _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        private static void InitializeToolbar(VisualElement root) {
            Resolve();
            root.Clear();

            var leftContainer = new VisualElement {
                name = "ee4v-toolbar-left",
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexShrink = 1,
                    flexGrow = 1,
                    height = Length.Percent(100),
                    overflow = Overflow.Hidden
                }
            };

            var rightContainer = new VisualElement {
                name = "ee4v-toolbar-right",
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = Length.Auto(),
                    flexShrink = 0,
                    height = Length.Percent(100)
                }
            };

            root.Add(leftContainer);
            root.Add(rightContainer);

            foreach (var component in _components.Where(c => c.Position == ToolbarPosition.Left)) {
                var element = component.CreateElement();
                if (element != null) leftContainer.Add(element);
            }

            foreach (var component in _components.Where(c => c.Position == ToolbarPosition.Right)) {
                var element = component.CreateElement();
                if (element != null) rightContainer.Add(element);
            }
        }

        private static VisualElement CreateBaseToolbar() {
            var toolbar = new VisualElement {
                name = "ee4v-project-toolbar",
                style = {
                    flexDirection = FlexDirection.Row,
                    height = 20,
                    overflow = Overflow.Hidden
                }
            };
            return toolbar;
        }

        private static void InitializationCheck() {
            var pbWrap = ProjectBrowserWrap.GetWindow();
            if (pbWrap == null) return;

            _projectWindow = pbWrap.Instance as EditorWindow;

            if (!SettingSingleton.I.compatLilEditorToolbox)
                InjectNormal();
            else
                InjectCompat();

            EditorApplication.update -= InitializationCheck;

            if (_isInitialized && SettingSingleton.I.enableProjectTab) EnableWatcher();
        }

        private static void InjectNormal() {
            if (_isInitialized) return;
            if (_projectWindow == null) return;
            _isInitialized = true;
            if (!SettingSingleton.I.enableProjectTab) return;

            var projectToolBar = CreateBaseToolbar();
            projectToolBar.style.marginLeft = 36;
            projectToolBar.style.marginRight = 470;

            InitializeToolbar(projectToolBar);
            _projectWindow.rootVisualElement.Add(projectToolBar);
            TabManager.Initialize();
        }

        private static void InjectCompat() {
            if (_isInitialized) return;
            if (_projectWindow == null || _projectWindow.rootVisualElement.childCount <= 0) return;

            _isInitialized = true;

            var projectToolBar = CreateBaseToolbar();
            projectToolBar.style.marginLeft = 0;
            projectToolBar.style.marginRight = 0;
            projectToolBar.style.flexGrow = 1;

            InitializeToolbar(projectToolBar);

            var target = _projectWindow.rootVisualElement[_projectWindow.rootVisualElement.childCount - 1];
            target.Insert(target.childCount, projectToolBar);

            TabManager.Initialize();
        }


        private static void EnableWatcher() {
            if (_watcherEnabled) return;
            _watcherEnabled = true;
            EditorApplication.update -= ProjectToolbarWatcher;
            EditorApplication.update += ProjectToolbarWatcher;
        }

        private static void ProjectToolbarWatcher() {
            if (_projectWindow == null) {
                _watcherEnabled = false;
                EditorApplication.update -= ProjectToolbarWatcher;
                return;
            }

            var pbWrap = new ProjectBrowserWrap(_projectWindow);
            var newPath = pbWrap.GetCurrentFolderPath();
            if (string.IsNullOrEmpty(newPath) || _currentFolderPath == newPath) return;
            UpdateCurrentPath(newPath);
            _currentFolderPath = newPath;
        }

        private static void UpdateCurrentPath(string path) {
            var tabContainer = _projectWindow.rootVisualElement?.Q<VisualElement>("ee4v-project-toolbar-tabContainer");
            if (tabContainer == null) return;

            var currentTab = TabManager.CurrentTab;
            if (currentTab == null || currentTab.parent != tabContainer) return;
            if (currentTab.name == "ee4v-project-toolbar-workspaceContainer-tab") return;

            TabManager.UpdateTab(currentTab, path, Path.GetFileName(path));
        }
    }
}