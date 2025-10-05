using UnityEditor;
using UnityEngine;

using System.Collections.Generic;

namespace _4OF.ee4v.ProjectExtension.Data {
    [CreateAssetMenu(fileName = "TabList")]
    public class TabListObject : ScriptableObject {
        [SerializeField] private List<Tab> tabList = new();

        public IReadOnlyList<Tab> TabList => tabList;

        [System.Serializable]
        public class Tab {
            public string path;
            public string tabName;
        }
        
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

        public static TabListObject LoadOrCreate() {
            var temp = CreateInstance<TabListObject>();
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(temp));
            DestroyImmediate(temp);
            var path = scriptPath.Replace("ProjectExtension/Data/TabListObject.cs", "UserData/TabList.asset");
            var tabListObject = AssetDatabase.LoadAssetAtPath<TabListObject>(path);
            if (tabListObject != null) return tabListObject;
            
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            tabListObject = CreateInstance<TabListObject>();
            tabListObject.tabList = new List<Tab>();
            AssetDatabase.CreateAsset(tabListObject, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning($"TabListObject not found at {path}. Creating new one.");
            return tabListObject;
        }
    }
}