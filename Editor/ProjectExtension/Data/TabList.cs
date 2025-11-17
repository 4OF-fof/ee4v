using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    [FilePath("ee4v/UserData/TabList.asset", FilePathAttribute.Location.ProjectFolder)]
    public class TabList : ScriptableSingleton<TabList> {
        [SerializeField] private List<Tab> contents = new();
        public IReadOnlyList<Tab> Contents => contents;

        public void AddTab(string path, string tabName, bool isWorkspace) {
            contents.Add(new Tab {
                path = path,
                tabName = tabName,
                isWorkspace = isWorkspace
            });
            Save(true);
        }

        public void InsertTab(int index, string path, string tabName, bool isWorkspace) {
            index = Mathf.Clamp(index, 0, contents.Count);
            contents.Insert(index, new Tab {
                path = path,
                tabName = tabName,
                isWorkspace = isWorkspace
            });
            Save(true);
        }

        public void MoveTab(int fromIndex, int toIndex) {
            if (fromIndex < 0 || fromIndex >= contents.Count) return;
            if (toIndex < 0 || toIndex >= contents.Count) return;
            if (fromIndex == toIndex) return;
            var item = contents[fromIndex];
            contents.RemoveAt(fromIndex);
            if (fromIndex < toIndex) toIndex--;
            contents.Insert(toIndex, item);
            Save(true);
        }

        public void RemoveTab(int index) {
            if (index < 0 || index >= contents.Count) return;
            contents.RemoveAt(index);
            Save(true);
        }

        public void UpdateTab(int index, string path = null, string tabName = null, bool? isWorkspace = null) {
            if (index < 0 || index >= contents.Count) return;
            if (path != null) contents[index].path = path;
            if (tabName != null) contents[index].tabName = tabName;
            if (isWorkspace.HasValue) contents[index].isWorkspace = isWorkspace.Value;
            Save(true);
        }

        [Serializable]
        public class Tab {
            public string path;
            public string tabName;
            public bool isWorkspace;
        }
    }
}