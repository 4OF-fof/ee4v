using System;
using System.Linq;
using _4OF.ee4v.ProjectExtension.Service;
using _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Data {
    public static class TabListController {
        private static TabListObject _asset;
        private static VisualElement _tabContainer;
        private static VisualElement _workspaceContainer;
        private static VisualElement _currentTab;

        public static VisualElement CurrentTab() {
            return _currentTab;
        }

        public static void Initialize() {
            if (_asset == null) _asset = TabListObject.LoadOrCreate();

            if (_tabContainer != null) return;
            var projectWindow = ReflectionWrapper.ProjectBrowserWindow;
            if (projectWindow != null) {
                _tabContainer = projectWindow.rootVisualElement?.Q<VisualElement>("ee4v-project-toolbar-tabContainer");
                _workspaceContainer = projectWindow.rootVisualElement?.Q<VisualElement>("ee4v-project-toolbar-workspaceContainer");
            }
            Sync();
            KeepOneTab();
        }

        private static void Add(VisualElement tab) {
            Initialize();
            var path = tab.tooltip;
            var name = tab.Q<Label>().text;
            _asset.Add(path, name);
            _tabContainer.Insert(_tabContainer.childCount - 1, tab);
            EditorUtility.SetDirty(_asset);
        }

        public static void AddWorkspaceTab(VisualElement workspaceTab) {
            Initialize();
            if (_workspaceContainer == null) return;
            
            var path = workspaceTab.tooltip;
            var name = workspaceTab.Q<Label>().text;
            _asset.Add(path, name, true);
            _workspaceContainer.Add(workspaceTab);
            EditorUtility.SetDirty(_asset);
        }

        public static void Insert(int index, VisualElement tab) {
            Initialize();
            var path = tab.tooltip;
            var name = tab.Q<Label>().text;
            _asset.Insert(index, path, name);
            _tabContainer.Insert(index, tab);
            EditorUtility.SetDirty(_asset);
        }

        public static void Remove(VisualElement tab) {
            Initialize();
            
            var isWorkspaceTab = tab.name == "ee4v-project-toolbar-workspaceContainer-tab";
            var isCurrentTab = tab == _currentTab;
            
            if (isWorkspaceTab) {
                if (_workspaceContainer == null) return;
                var index = _workspaceContainer.IndexOf(tab);
                _asset.Remove(index);
                _workspaceContainer.Remove(tab);
                
                if (isCurrentTab) {
                    _currentTab = null;
                    var lastTab = _tabContainer?.Children()
                        .LastOrDefault(e => e.name == "ee4v-project-toolbar-tabContainer-tab");
                    if (lastTab != null) {
                        SelectTab(lastTab);
                    }
                }
            } else {
                var index = _tabContainer.IndexOf(tab);
                _asset.Remove(index);
                _tabContainer.Remove(tab);
                
                if (isCurrentTab) {
                    var regularTabs = _tabContainer.Children()
                        .Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab").ToList();
                    
                    if (regularTabs.Count == 0) {
                        _currentTab = null;
                        var currentIsWorkspace = _currentTab != null && _currentTab.name == "ee4v-project-toolbar-workspaceContainer-tab";
                        if (!currentIsWorkspace) {
                            KeepOneTab();
                        }
                    } else {
                        SelectTab(index == 0 ? regularTabs[0] : regularTabs[Math.Max(0, index - 1)]);
                    }
                } else {
                    KeepOneTab();
                }
            }
            
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

            _asset.Remove(fromIndex);
            _asset.Insert(toIndex, path, name);
            _tabContainer.Remove(tab);
            _tabContainer.Insert(Mathf.Clamp(toIndex, 0, _tabContainer.childCount - 1), tab);

            if (_currentTab == tab) Tab.SetState(tab, Tab.State.Selected);
            EditorUtility.SetDirty(_asset);
        }

        public static void UpdateTab(VisualElement tab, string path, string name) {
            Initialize();
            var index = _tabContainer.IndexOf(tab);
            _asset.UpdateTab(index, path, name);
            tab.tooltip = path;
            tab.Q<Label>().text = name;
            EditorUtility.SetDirty(_asset);
        }

        public static void SelectTab(VisualElement tabElement) {
            if (tabElement == null) return;
            if (tabElement == _currentTab) return;
            
            var isWorkspaceTab = tabElement.name == "ee4v-project-toolbar-workspaceContainer-tab";
            var currentIsWorkspaceTab = _currentTab != null && _currentTab.name == "ee4v-project-toolbar-workspaceContainer-tab";
            
            if (_currentTab != null) {
                if (currentIsWorkspaceTab) {
                    WorkspaceTab.SetState(_currentTab, WorkspaceTab.State.Default);
                } else {
                    Tab.SetState(_currentTab, Tab.State.Default);
                }
            }
            
            if (isWorkspaceTab) {
                WorkspaceTab.SetState(tabElement, WorkspaceTab.State.Selected);
            } else {
                Tab.SetState(tabElement, Tab.State.Selected);
            }
            
            _currentTab = tabElement;
            ProjectWindowOpener.OpenFolderInProject(tabElement.tooltip);
            EditorUtility.SetDirty(_asset);
        }

        private static void KeepOneTab() {
            if (_tabContainer is not { childCount: <= 1 }) return;
            var newTab = Tab.Element("Assets", "Assets");
            Add(newTab);
            SelectTab(newTab);
        }

        private static void Sync() {
            if (_tabContainer == null || _asset == null) return;

            var existingTabs = _tabContainer.Children().Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab")
                .ToList();
            foreach (var t in existingTabs) _tabContainer.Remove(t);
            
            if (_workspaceContainer != null) {
                var existingWorkspaceTabs = _workspaceContainer.Children()
                    .Where(e => e.name == "ee4v-project-toolbar-workspaceContainer-tab").ToList();
                foreach (var t in existingWorkspaceTabs) _workspaceContainer.Remove(t);
            }

            var objectTabList = _asset.TabList;
            var tabInsertIndex = 0;
            VisualElement firstRegularTab = null;
            
            foreach (var objectTab in objectTabList) {
                if (objectTab.isWorkspace) {
                    if (_workspaceContainer == null) continue;
                    var newWorkspaceTab = WorkspaceTab.Element(objectTab.path, objectTab.tabName);
                    if (newWorkspaceTab == null) continue;
                    _workspaceContainer.Add(newWorkspaceTab);
                } else {
                    var newTab = Tab.Element(objectTab.path, objectTab.tabName);
                    if (newTab == null) continue;
                    _tabContainer.Insert(Math.Min(tabInsertIndex, _tabContainer.childCount - 1), newTab);
                    if (firstRegularTab == null) firstRegularTab = newTab;
                    tabInsertIndex++;
                }
            }

            if (firstRegularTab != null) {
                Tab.SetState(firstRegularTab, Tab.State.Selected);
                SelectTab(firstRegularTab);
                var others = _tabContainer.Children()
                    .Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab" && e != firstRegularTab).ToList();
                foreach (var o in others) Tab.SetState(o, Tab.State.Default);
            }
        }
    }
}