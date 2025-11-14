using System;
using System.Collections.Generic;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data.Schema {
    [CreateAssetMenu(fileName = "TabList")]
    public class TabListObject : ScriptableObject {
        [SerializeField] internal List<Tab> tabList = new();

        public IReadOnlyList<Tab> TabList => tabList;

        [Serializable]
        public class Tab {
            public string path;
            public string tabName;
            public bool isWorkspace;
        }
    }
}