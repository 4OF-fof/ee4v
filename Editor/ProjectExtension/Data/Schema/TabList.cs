using System;
using System.Collections.Generic;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data.Schema {
    [CreateAssetMenu(fileName = "TabList")]
    public class TabList : ScriptableObject {
        [SerializeField] internal List<Tab> contents = new();

        public IReadOnlyList<Tab> Contents => contents;

        [Serializable]
        public class Tab {
            public string path;
            public string tabName;
            public bool isWorkspace;
        }
    }
}