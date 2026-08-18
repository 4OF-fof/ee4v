using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.SceneSwitcher
{
    [FilePath(
        "ee4v/UserData/SceneList.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class SceneSwitcherStateStore
        : ScriptableSingleton<SceneSwitcherStateStore>,
          ISceneSwitcherRepository
    {
        [SerializeField]
        private List<SerializedRecord> contents =
            new List<SerializedRecord>();

        public IReadOnlyList<SceneSwitcherRecord> Load()
        {
            return (contents ?? new List<SerializedRecord>())
                .Where(record => record != null)
                .Select(record => new SceneSwitcherRecord(
                    record.path,
                    record.isIgnored,
                    record.isFavorite))
                .ToArray();
        }

        public void Save(
            IReadOnlyList<SceneSwitcherRecord> records)
        {
            contents = (records ??
                        Array.Empty<SceneSwitcherRecord>())
                .Where(record => record != null)
                .Select(record => new SerializedRecord
                {
                    path = record.Path,
                    isIgnored = record.IsIgnored,
                    isFavorite = record.IsFavorite
                })
                .ToList();
            Save(true);
        }

        [Serializable]
        private sealed class SerializedRecord
        {
            public string path;
            public bool isIgnored;
            public bool isFavorite;
        }
    }
}
