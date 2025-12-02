using System.IO;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.Wraps;
using _4OF.ee4v.ProjectExtension.Toolbar.Component;
using UnityEditor;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar {
    public static class ProjectToolbarInjector {
        private static bool _isInitialized;
        private static EditorWindow _projectWindow;
        private static string _currentFolderPath;
        private static bool _watcherEnabled;

        [InitializeOnLoadMethod]
        private static void Initialize() {
            if (!SettingSingleton.I.enableProjectExtension) return;

            EditorApplication.update -= InitializationCheck;
            EditorApplication.update += InitializationCheck;
        }

        private static void InitializationCheck() {
            var pbWrap = ProjectBrowserWrap.GetWindow();
            if (pbWrap == null) return;

            _projectWindow = pbWrap.Instance as EditorWindow;

            if (!SettingSingleton.I.compatLilEditorToolbox)
                InitializeContent();
            else
                CompatInjector();

            EditorApplication.update -= InitializationCheck;

            if (_isInitialized && SettingSingleton.I.enableProjectTab) EnableWatcher();
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

            if (_projectWindow == null) return;

            _isInitialized = true;

            if (!SettingSingleton.I.enableProjectTab) return;
            var projectToolBar = new ProjectToolBar();
            _projectWindow.rootVisualElement.Add(projectToolBar);
            TabManager.Initialize();
        }

        private static void ProjectToolbarWatcher() {
            if (_projectWindow == null) {
                DisableWatcher();
                return;
            }

            var pbWrap = new ProjectBrowserWrap(_projectWindow);
            var newPath = pbWrap.GetCurrentFolderPath();

            if (string.IsNullOrEmpty(newPath) || _currentFolderPath == newPath) return;
            UpdateCurrentPath(newPath);
            _currentFolderPath = newPath;
        }

        private static void CompatInjector() {
            if (_isInitialized) return;

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
            var tabContainer = new TabContainer();

            containerWrapper.Add(workspaceContainer);
            containerWrapper.Add(tabContainer);

            var target = _projectWindow.rootVisualElement[_projectWindow.rootVisualElement.childCount - 1];
            target.Insert(target.childCount, containerWrapper);
            TabManager.Initialize();
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