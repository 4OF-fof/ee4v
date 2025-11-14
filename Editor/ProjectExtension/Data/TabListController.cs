using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.ProjectExtension.Data.Schema;
using _4OF.ee4v.ProjectExtension.Service;
using _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Data {
    public static class TabListController {
        private static TabList _asset;
        private static VisualElement _tabContainer;
        private static VisualElement _workspaceContainer;
        private static VisualElement _currentTab;
        private const string AssetPath = "Assets/4OF/ee4v/UserData/TabList.asset";

        public static VisualElement CurrentTab() {
            return _currentTab;
        }

        public static TabList GetInstance() {
            if (_asset == null) _asset = LoadOrCreate();
            return _asset;
        }

        private static TabList LoadOrCreate() {
            var tabListObject = AssetDatabase.LoadAssetAtPath<TabList>(AssetPath);
            if (tabListObject != null) {
                _asset = tabListObject;
                return tabListObject;
            }

            var dir = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            tabListObject = ScriptableObject.CreateInstance<TabList>();
            tabListObject.contents = new List<TabList.Tab>();
            AssetDatabase.CreateAsset(tabListObject, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning(I18N.Get("Debug.ProjectExtension.NotFoundTabListObject", AssetPath));
            _asset = tabListObject;
            return tabListObject;
        }

        public static void Initialize() {
            if (!_asset) _asset = LoadOrCreate();

            if (_tabContainer != null) return;
            var projectWindow = ReflectionWrapper.ProjectBrowserWindow;
            if (projectWindow) {
                _tabContainer = projectWindow.rootVisualElement?.Q<VisualElement>("ee4v-project-toolbar-tabContainer");
                _workspaceContainer =
                    projectWindow.rootVisualElement?.Q<VisualElement>("ee4v-project-toolbar-workspaceContainer");
            }

            Sync();
            KeepOneTab();
        }

        private static void AddToAsset(string path, string tabName, bool isWorkspace = false) {
            var entry = new TabList.Tab { path = path, tabName = tabName, isWorkspace = isWorkspace };
            _asset.contents.Add(entry);
        }

        private static void InsertToAsset(int index, string path, string tabName, bool isWorkspace = false) {
            var entry = new TabList.Tab { path = path, tabName = tabName, isWorkspace = isWorkspace };
            index = Mathf.Clamp(index, 0, _asset.contents.Count);
            _asset.contents.Insert(index, entry);
        }

        private static void RemoveFromAsset(int index) {
            if (index < 0 || index >= _asset.contents.Count) return;
            _asset.contents.RemoveAt(index);
        }

        private static void UpdateTabInAsset(int index, string path, string tabName) {
            if (index < 0 || index >= _asset.contents.Count) return;
            _asset.contents[index].path = path;
            _asset.contents[index].tabName = tabName;
        }

        private static void Add(VisualElement tab) {
            Initialize();
            var path = tab.tooltip;
            var name = tab.Q<Label>().text;
            AddToAsset(path, name);
            _tabContainer.Insert(_tabContainer.childCount - 1, tab);
            EditorUtility.SetDirty(_asset);
        }

        public static void AddWorkspaceTab(VisualElement workspaceTab) {
            Initialize();
            if (_workspaceContainer == null) return;

            var path = workspaceTab.tooltip;
            var name = workspaceTab.Q<Label>().text;
            AddToAsset(path, name, true);
            _workspaceContainer.Add(workspaceTab);
            EditorUtility.SetDirty(_asset);
        }

        public static void Insert(int index, VisualElement tab) {
            Initialize();
            var path = tab.tooltip;
            var name = tab.Q<Label>().text;
            InsertToAsset(index, path, name);
            _tabContainer.Insert(index, tab);
            EditorUtility.SetDirty(_asset);
        }

        public static void Remove(VisualElement tab) {
            Initialize();

            var isWorkspaceTab = tab.name == "ee4v-project-toolbar-workspaceContainer-tab";
            var isCurrentTab = tab == _currentTab;

            if (isWorkspaceTab) {
                if (_workspaceContainer == null) return;

                var path = tab.tooltip;
                var name = tab.Q<Label>()?.text;
                var assetIndex = _asset.Contents.ToList()
                    .FindIndex(t => t.isWorkspace && t.path == path && t.tabName == name);

                if (assetIndex >= 0) RemoveFromAsset(assetIndex);
                _workspaceContainer.Remove(tab);

                RemoveWorkspaceLabels(name);

                if (isCurrentTab) {
                    _currentTab = null;
                    var lastTab = _tabContainer?.Children()
                        .LastOrDefault(e => e.name == "ee4v-project-toolbar-tabContainer-tab");
                    if (lastTab != null)
                        SelectTab(lastTab);
                    else
                        KeepOneTab();
                }
            }
            else {
                var index = _tabContainer.IndexOf(tab);
                RemoveFromAsset(index);
                _tabContainer.Remove(tab);

                if (isCurrentTab) {
                    _currentTab = null;

                    var regularTabs = _tabContainer.Children()
                        .Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab").ToList();
                    var workspaceTabs = _workspaceContainer?.Children()
                        .Where(e => e.name == "ee4v-project-toolbar-workspaceContainer-tab").ToList();

                    if (regularTabs.Count > 0)
                        SelectTab(index == 0 ? regularTabs[0] : regularTabs[Math.Max(0, index - 1)]);
                    else if (workspaceTabs is { Count: > 0 })
                        SelectTab(workspaceTabs.Last());
                    else
                        KeepOneTab();
                }
            }

            KeepOneTab();
            EditorUtility.SetDirty(_asset);
        }

        public static void Move(int fromIndex, int toIndex) {
            Initialize();
            if (_tabContainer == null || _asset == null) return;
            if (fromIndex == toIndex) return;

            fromIndex = Mathf.Clamp(fromIndex, 0, _tabContainer.childCount - 1);
            toIndex = Mathf.Clamp(toIndex, 0, _tabContainer.childCount - 1);

            var tab = _tabContainer.ElementAt(fromIndex);
            if (tab == null) return;

            if (toIndex > fromIndex) toIndex--;

            var path = tab.tooltip;
            var name = tab.Q<Label>()?.text;

            RemoveFromAsset(fromIndex);
            InsertToAsset(toIndex, path, name);
            _tabContainer.Remove(tab);
            _tabContainer.Insert(Mathf.Clamp(toIndex, 0, _tabContainer.childCount - 1), tab);

            if (_currentTab == tab) Tab.SetState(tab, Tab.State.Selected);
            EditorUtility.SetDirty(_asset);
        }

        public static void UpdateTab(VisualElement tab, string path, string name) {
            Initialize();
            var index = _tabContainer.IndexOf(tab);
            UpdateTabInAsset(index, path, name);
            tab.tooltip = path;
            tab.Q<Label>().text = name;
            EditorUtility.SetDirty(_asset);
        }

        public static void SelectTab(VisualElement tabElement) {
            if (tabElement == null) return;

            var isWorkspaceTab = tabElement.name == "ee4v-project-toolbar-workspaceContainer-tab";
            if (tabElement == _currentTab && !isWorkspaceTab) return;

            var currentIsWorkspaceTab = _currentTab is { name: "ee4v-project-toolbar-workspaceContainer-tab" };

            if (_currentTab != null) {
                if (currentIsWorkspaceTab)
                    WorkspaceTab.SetState(_currentTab, WorkspaceTab.State.Default);
                else
                    Tab.SetState(_currentTab, Tab.State.Default);
            }

            if (isWorkspaceTab)
                WorkspaceTab.SetState(tabElement, WorkspaceTab.State.Selected);
            else
                Tab.SetState(tabElement, Tab.State.Selected);

            _currentTab = tabElement;

            if (isWorkspaceTab) {
                var labelName = tabElement.Q<Label>()?.text;
                if (!string.IsNullOrEmpty(labelName)) ReflectionWrapper.SetSearchFilter($"l=Ee4v.ws.{labelName}");
            }
            else {
                ReflectionWrapper.ClearSearchFilter();
                ProjectWindowOpener.OpenFolderInProject(tabElement.tooltip);
            }

            EditorUtility.SetDirty(_asset);
        }

        private static void KeepOneTab() {
            if (_tabContainer == null) return;

            var regularTabCount = _tabContainer.Children()
                .Count(e => e.name == "ee4v-project-toolbar-tabContainer-tab");
            var workspaceTabCount = _workspaceContainer?.Children()
                .Count(e => e.name == "ee4v-project-toolbar-workspaceContainer-tab") ?? 0;
            var totalTabCount = regularTabCount + workspaceTabCount;

            if (totalTabCount > 0) return;

            var newTab = Tab.Element("Assets", "Assets");
            Add(newTab);
            SelectTab(newTab);
        }

        private static void Sync() {
            if (_tabContainer == null || !_asset) return;

            var existingTabs = _tabContainer.Children().Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab")
                .ToList();
            foreach (var t in existingTabs) _tabContainer.Remove(t);

            if (_workspaceContainer != null) {
                var existingWorkspaceTabs = _workspaceContainer.Children()
                    .Where(e => e.name == "ee4v-project-toolbar-workspaceContainer-tab").ToList();
                foreach (var t in existingWorkspaceTabs) _workspaceContainer.Remove(t);
            }

            var objectTabList = _asset.Contents;
            var tabInsertIndex = 0;
            VisualElement firstRegularTab = null;

            foreach (var objectTab in objectTabList)
                if (objectTab.isWorkspace) {
                    if (_workspaceContainer == null) continue;
                    var newWorkspaceTab = WorkspaceTab.Element(objectTab.path, objectTab.tabName);
                    if (newWorkspaceTab == null) continue;
                    _workspaceContainer.Add(newWorkspaceTab);
                }
                else {
                    var newTab = Tab.Element(objectTab.path, objectTab.tabName);
                    if (newTab == null) continue;
                    _tabContainer.Insert(Math.Min(tabInsertIndex, _tabContainer.childCount - 1), newTab);
                    firstRegularTab ??= newTab;
                    tabInsertIndex++;
                }

            if (firstRegularTab == null) return;
            Tab.SetState(firstRegularTab, Tab.State.Selected);
            SelectTab(firstRegularTab);
            var others = _tabContainer.Children()
                .Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab" && e != firstRegularTab).ToList();
            foreach (var o in others) Tab.SetState(o, Tab.State.Default);
        }

        private static void RemoveWorkspaceLabels(string workspaceName) {
            if (string.IsNullOrEmpty(workspaceName)) return;

            var labelName = $"Ee4v.ws.{workspaceName}";
            
            var guids = AssetDatabase.FindAssets($"l:{labelName}");
            if (guids.Length == 0) return;

            var removedCount = 0;
            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null) continue;

                var labels = AssetDatabase.GetLabels(asset).ToList();
                if (!labels.Remove(labelName)) continue;
                AssetDatabase.SetLabels(asset, labels.ToArray());
                removedCount++;
            }

            if (removedCount <= 0) return;
            AssetDatabase.SaveAssets();
            Debug.Log(I18N.Get("Debug.ProjectExtension.RemovedWorkspaceLabels", labelName, removedCount));
        }

        public static string GetCurrentWorkspace() {
            Initialize();
            
            if (_currentTab == null) return null;
            if (_currentTab.name != "ee4v-project-toolbar-workspaceContainer-tab") return null;
            
            var label = _currentTab.Q<Label>();
            return label?.text;
        }
    }
}