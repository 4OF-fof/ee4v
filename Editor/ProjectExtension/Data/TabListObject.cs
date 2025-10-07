using System;
using System.Collections.Generic;
using System.IO;
using _4OF.ee4v.Core.i18n;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    [CreateAssetMenu(fileName = "TabList")]
    public class TabListObject : ScriptableObject {
        private static TabListObject _instance;

        [SerializeField] private List<Tab> tabList = new();

        public IReadOnlyList<Tab> TabList => tabList;

        public void Add(string path, string tabName) {
            var entry = new Tab { path = path, tabName = tabName };
            tabList.Add(entry);
        }

        public void Insert(int index, string path, string tabName) {
            var entry = new Tab { path = path, tabName = tabName };
            index = Mathf.Clamp(index, 0, tabList.Count);
            tabList.Insert(index, entry);
        }

        public void Remove(int index) {
            if (index < 0 || index >= tabList.Count) return;
            tabList.RemoveAt(index);
        }

        public void UpdateTab(int index, string path, string tabName) {
            if (index < 0 || index >= tabList.Count) return;
            tabList[index].path = path;
            tabList[index].tabName = tabName;
        }

        public static TabListObject GetInstance() {
            if (_instance == null) _instance = LoadOrCreate();
            return _instance;
        }

        public static TabListObject LoadOrCreate() {
            const string path = "Assets/4OF/ee4v/UserData/TabList.asset";;
            var tabListObject = AssetDatabase.LoadAssetAtPath<TabListObject>(path);
            if (tabListObject != null) {
                _instance = tabListObject;
                return tabListObject;
            }

            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            tabListObject = CreateInstance<TabListObject>();
            tabListObject.tabList = new List<Tab>();
            AssetDatabase.CreateAsset(tabListObject, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning(I18N.Get("Debug.ProjectExtension.NotFoundTabListObject", path));
            _instance = tabListObject;
            return tabListObject;
        }

        [Serializable]
        public class Tab {
            public string path;
            public string tabName;
        }
    }
}