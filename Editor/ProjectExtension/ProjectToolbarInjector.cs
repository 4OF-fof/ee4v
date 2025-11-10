using System.IO;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.ProjectExtension.Data;
using _4OF.ee4v.ProjectExtension.Service;
using _4OF.ee4v.ProjectExtension.UI.ToolBar;
using UnityEditor;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension {
    public static class ProjectToolbarInjector {
        private static bool _isInitialized;
        private static EditorWindow _projectWindow;
        private static string _currentFolderPath;
        private static bool _watcherEnabled;

        [InitializeOnLoadMethod]
        private static void Initialize() {
            if (!EditorPrefsManager.EnableProjectExtension) return;

            EditorApplication.update -= InitializationCheck;
            EditorApplication.update += InitializationCheck;
        }

        private static void InitializationCheck() {
            var projectWindow = ReflectionWrapper.ProjectBrowserWindow;
            if (projectWindow == null) return;

            if (!EditorPrefsManager.CompatLilEditorToolbox)
                InitializeContent();
            else
                CompatInjector();

            EditorApplication.update -= InitializationCheck;

            if (_isInitialized && EditorPrefsManager.EnableProjectTab) EnableWatcher();
        }

        private static void EnableWatcher() {
            if (_watcherEnabled) return;
            _watcherEnabled = true;
            EditorApplication.update -= ProjectToolbarWatcher;
            EditorApplication.update += ProjectToolbarWatcher;
        }

        private static void DisableWatcher() {
            if (!_watcherEnabled) return;
            _watcherEnabled = false;
            EditorApplication.update -= ProjectToolbarWatcher;
        }

        private static void InitializeContent() {
            if (_isInitialized) return;

            _projectWindow = ReflectionWrapper.ProjectBrowserWindow;
            if (_projectWindow == null) return;

            _isInitialized = true;

            if (!EditorPrefsManager.EnableProjectTab) return;
            var projectToolBar = ProjectToolBar.Element();
            _projectWindow.rootVisualElement.Add(projectToolBar);
            TabListController.Initialize();
        }

        private static void ProjectToolbarWatcher() {
            if (_projectWindow == null) {
                DisableWatcher();
                return;
            }

            var newPath = ReflectionWrapper.GetProjectWindowCurrentPath(_projectWindow);
            if (string.IsNullOrEmpty(newPath) || _currentFolderPath == newPath) return;
            UpdateCurrentPath(newPath);
            _currentFolderPath = newPath;
        }

        private static void CompatInjector() {
            if (_isInitialized) return;

            _projectWindow = ReflectionWrapper.ProjectBrowserWindow;
            if (_projectWindow == null || _projectWindow.rootVisualElement.childCount <= 0) return;

            _isInitialized = true;

            var containerWrapper = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    alignItems = Align.Center
                }
            };

            var workspaceContainer = WorkspaceContainer.Element();
            var tabContainer = TabContainer.Element();

            containerWrapper.Add(workspaceContainer);
            containerWrapper.Add(tabContainer);

            var target = _projectWindow.rootVisualElement[_projectWindow.rootVisualElement.childCount - 1];
            target.Insert(target.childCount, containerWrapper);
            TabListController.Initialize();
        }

        private static void UpdateCurrentPath(string path) {
            var tabContainer = _projectWindow.rootVisualElement?.Q<VisualElement>("ee4v-project-toolbar-tabContainer");
            if (tabContainer == null) return;

            var currentTab = TabListController.CurrentTab();
            if (currentTab == null || currentTab.parent != tabContainer) return;

            if (currentTab.name == "ee4v-project-toolbar-workspaceContainer-tab") return;

            TabListController.UpdateTab(currentTab, path, Path.GetFileName(path));
        }
    }
}