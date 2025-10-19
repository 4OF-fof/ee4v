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
        private static VisualElement _currentTab;

        public static VisualElement CurrentTab() {
            return _currentTab;
        }

        public static void Initialize() {
            if (_asset == null) _asset = TabListObject.LoadOrCreate();

            if (_tabContainer != null) return;
            var projectWindow = ReflectionWrapper.ProjectBrowserWindow;
            if (projectWindow != null)
                _tabContainer = projectWindow.rootVisualElement?.Q<VisualElement>("ee4v-project-toolbar-tabContainer");
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
            var index = _tabContainer.IndexOf(tab);
            _asset.Remove(index);
            _tabContainer.Remove(tab);
            if (tab == _currentTab)
                SelectTab(index == 0 ? _tabContainer.ElementAt(0) : _tabContainer.ElementAt(index - 1));
            KeepOneTab();
            EditorUtility.SetDirty(_asset);
        }

        public static void Move(int fromIndex, int toIndex) {
            Initialize();
            if (_tabContainer == null || _asset == null) return;
            if (fromIndex == toIndex) return;

            fromIndex = Mathf.Clamp(fromIndex, 0, _tabContainer.childCount - 1);
            toIndex = Mathf.Clamp(toIndex, 0, _tabContainer.childCount     - 1);

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
            if (tabElement == _currentTab) return;
            Tab.SetState(tabElement, Tab.State.Selected);
            if (_currentTab != null) Tab.SetState(_currentTab, Tab.State.Default);
            _currentTab = tabElement;
            ProjectWindowOpener.OpenFolderInProject(_currentTab.tooltip);
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

            var objectTabList = _asset.TabList;
            var insertIndex = 0;
            foreach (var objectTab in objectTabList) {
                var newTab = Tab.Element(objectTab.path, objectTab.tabName);
                if (newTab == null) continue;
                _tabContainer.Insert(Math.Min(insertIndex, _tabContainer.childCount - 1), newTab);
                insertIndex++;
            }

            if (objectTabList.Count <= 0) return;
            var firstPath = objectTabList[0].path;
            var firstElem = _tabContainer.Children().FirstOrDefault(e =>
                e.name == "ee4v-project-toolbar-tabContainer-tab" && e.tooltip == firstPath);
            if (firstElem == null) return;
            Tab.SetState(firstElem, Tab.State.Selected);
            SelectTab(firstElem);
            var others = _tabContainer.Children()
                .Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab" && e.tooltip != firstPath).ToList();
            foreach (var o in others) Tab.SetState(o, Tab.State.Default);
        }
    }
}