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

        [InitializeOnLoadMethod]
        private static void Initialize() {
            if (!EditorPrefsManager.EnableProjectExtension) return;
            EditorApplication.update -= Injector;
            EditorApplication.update += Injector;
        }

        private static void Injector() {
            if (!_isInitialized && !EditorPrefsManager.CompatLilEditorToolbox) InitializeContent();
            if (!_isInitialized && EditorPrefsManager.CompatLilEditorToolbox) CompatInjector();
            ProjectToolbarWatcher();
        }

        private static void InitializeContent() {
            _isInitialized = true;
            _projectWindow = ReflectionWrapper.ProjectBrowserWindow;
            if (!EditorPrefsManager.EnableProjectTab) return;
            var projectToolBar = ProjectToolBar.Element();
            _projectWindow.rootVisualElement.Add(projectToolBar);
            TabListController.Initialize();
        }

        private static void ProjectToolbarWatcher() {
            var newPath = GetCurrentPath();
            if (string.IsNullOrEmpty(newPath) || _currentFolderPath == newPath) return;
            UpdateCurrentPath(newPath);
            _currentFolderPath = newPath;
        }

        private static void CompatInjector() {
            _projectWindow = ReflectionWrapper.ProjectBrowserWindow;
            if (_projectWindow.rootVisualElement.childCount <= 0) return;
            _isInitialized = true;
            var tabContainer = TabContainer.Element();
            var target = _projectWindow.rootVisualElement[_projectWindow.rootVisualElement.childCount - 1];
            target.Insert(target.childCount, tabContainer);
            TabListController.Initialize();
        }

        private static string GetCurrentPath() {
            if (_projectWindow == null) return null;
            var so = new SerializedObject(_projectWindow);

            var folders = so.FindProperty("m_SearchFilter.m_Folders");
            if (folders is not { arraySize: > 0 }) folders = so.FindProperty("m_LastFolders");
            if (folders is not { arraySize: > 0 }) return null;
            var folderPath = folders.GetArrayElementAtIndex(0).stringValue;
            return AssetDatabase.IsValidFolder(folderPath) ? folderPath : null;
        }

        private static void UpdateCurrentPath(string path) {
            var tabContainer = _projectWindow.rootVisualElement?.Q<VisualElement>("ee4v-project-toolbar-tabContainer");
            if (tabContainer == null) return;

            var currentTab = TabListController.CurrentTab();
            if (currentTab == null || currentTab.parent != tabContainer) return;

            TabListController.UpdateTab(currentTab, path, Path.GetFileName(path));
        }
    }
}