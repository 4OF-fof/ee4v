using System;
using System.Linq;
using _4OF.ee4v.ProjectExtension.Data;
using _4OF.ee4v.ProjectExtension.Service;
using _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.UI.ToolBar {
    public static class TabManager {
        private static VisualElement _tabContainer;
        private static VisualElement _workspaceContainer;

        public static VisualElement CurrentTab { get; private set; }

        public static void Initialize() {
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

        private static void Add(VisualElement tab) {
            Initialize();
            var path = tab.tooltip;
            var name = tab.Q<Label>().text;
            TabList.instance.Add(path, name, false);
            _tabContainer.Insert(_tabContainer.childCount - 1, tab);
            EditorUtility.SetDirty(TabList.instance);
        }

        public static void AddWorkspaceTab(VisualElement workspaceTab) {
            Initialize();
            if (_workspaceContainer == null) return;

            var path = workspaceTab.tooltip;
            var name = workspaceTab.Q<Label>().text;
            TabList.instance.Add(path, name, true);
            _workspaceContainer.Add(workspaceTab);
            EditorUtility.SetDirty(TabList.instance);
        }

        public static void Insert(int index, VisualElement tab) {
            Initialize();
            var path = tab.tooltip;
            var name = tab.Q<Label>().text;
            TabList.instance.Insert(index, path, name, false);
            _tabContainer.Insert(index, tab);
            EditorUtility.SetDirty(TabList.instance);
        }

        public static void Remove(VisualElement tab) {
            Initialize();

            var isWorkspaceTab = tab.name == "ee4v-project-toolbar-workspaceContainer-tab";
            var isCurrentTab = tab == CurrentTab;

            if (isWorkspaceTab) {
                if (_workspaceContainer == null) return;

                var path = tab.tooltip;
                var name = tab.Q<Label>()?.text;
                var assetIndex = TabList.instance.Contents.ToList()
                    .FindIndex(t => t.isWorkspace && t.path == path && t.tabName == name);

                if (assetIndex >= 0) TabList.instance.Remove(assetIndex);
                _workspaceContainer.Remove(tab);

                TabListService.RemoveWorkspaceLabels(name);
                TabListService.SetCurrentWorkspace(null);

                if (isCurrentTab) {
                    CurrentTab = null;
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
                TabList.instance.Remove(index);
                _tabContainer.Remove(tab);

                if (isCurrentTab) {
                    CurrentTab = null;

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
            EditorUtility.SetDirty(TabList.instance);
        }

        public static void Move(int fromIndex, int toIndex) {
            Initialize();
            if (_tabContainer == null) return;
            if (fromIndex == toIndex) return;

            fromIndex = Mathf.Clamp(fromIndex, 0, _tabContainer.childCount - 1);
            toIndex = Mathf.Clamp(toIndex, 0, _tabContainer.childCount - 1);

            var tab = _tabContainer.ElementAt(fromIndex);
            if (tab == null) return;

            if (toIndex > fromIndex) toIndex--;

            TabList.instance.Move(fromIndex, toIndex);

            _tabContainer.Remove(tab);
            _tabContainer.Insert(Mathf.Clamp(toIndex, 0, _tabContainer.childCount - 1), tab);

            if (CurrentTab == tab) Tab.SetState(tab, Tab.State.Selected);
            EditorUtility.SetDirty(TabList.instance);
        }

        public static void UpdateTab(VisualElement tab, string path, string name) {
            Initialize();
            var index = _tabContainer.IndexOf(tab);
            TabList.instance.Update(index, path, name);
            tab.tooltip = path;
            tab.Q<Label>().text = name;
            EditorUtility.SetDirty(TabList.instance);
        }

        public static void SelectTab(VisualElement tabElement) {
            if (tabElement == null) return;

            var isWorkspaceTab = tabElement.name == "ee4v-project-toolbar-workspaceContainer-tab";
            if (tabElement == CurrentTab && !isWorkspaceTab) return;

            var currentIsWorkspaceTab = CurrentTab is { name: "ee4v-project-toolbar-workspaceContainer-tab" };

            if (CurrentTab != null) {
                if (currentIsWorkspaceTab)
                    WorkspaceTab.SetState(CurrentTab, WorkspaceTab.State.Default);
                else
                    Tab.SetState(CurrentTab, Tab.State.Default);
            }

            if (isWorkspaceTab)
                WorkspaceTab.SetState(tabElement, WorkspaceTab.State.Selected);
            else
                Tab.SetState(tabElement, Tab.State.Selected);

            CurrentTab = tabElement;

            if (isWorkspaceTab) {
                var labelName = tabElement.Q<Label>()?.text;
                if (!string.IsNullOrEmpty(labelName)) {
                    ReflectionWrapper.SetSearchFilter($"l=Ee4v.ws.{labelName}");
                    TabListService.SetCurrentWorkspace(labelName);
                }
            }
            else {
                ReflectionWrapper.ClearSearchFilter();
                var folderObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(tabElement.tooltip);
                if (folderObject == null) return;

                ReflectionWrapper.ShowFolderContents(folderObject.GetInstanceID());
                Selection.activeObject = null;
                GUI.FocusControl(null);
                TabListService.SetCurrentWorkspace(null);
            }

            EditorUtility.SetDirty(TabList.instance);
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
            if (_tabContainer == null) return;

            var existingTabs = _tabContainer.Children().Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab")
                .ToList();
            foreach (var t in existingTabs) _tabContainer.Remove(t);

            if (_workspaceContainer != null) {
                var existingWorkspaceTabs = _workspaceContainer.Children()
                    .Where(e => e.name == "ee4v-project-toolbar-workspaceContainer-tab").ToList();
                foreach (var t in existingWorkspaceTabs) _workspaceContainer.Remove(t);
            }

            var objectTabList = TabList.instance.Contents;
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
    }
}