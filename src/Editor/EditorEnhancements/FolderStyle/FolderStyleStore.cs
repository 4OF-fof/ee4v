using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderStyle
{
    [FilePath(
        "UserSettings/ee4v.folder-styles.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class FolderStyleStore
        : ScriptableSingleton<FolderStyleStore>,
          IFolderStyleRepository
    {
        [SerializeField]
        private List<SerializedFolderStyle> _styles =
            new List<SerializedFolderStyle>();

        [SerializeField]
        private List<string> _recentIconGuids =
            new List<string>();

        public FolderStyleValue Get(string folderGuid)
        {
            if (string.IsNullOrEmpty(folderGuid))
            {
                return null;
            }

            for (var i = 0; i < _styles.Count; i++)
            {
                var style = _styles[i];
                if (style != null &&
                    string.Equals(
                        style.folderGuid,
                        folderGuid,
                        StringComparison.Ordinal))
                {
                    return new FolderStyleValue(
                        style.folderGuid,
                        style.hasColor,
                        style.color,
                        style.iconGuid);
                }
            }

            return null;
        }

        public void Put(FolderStyleValue style)
        {
            if (style == null ||
                string.IsNullOrEmpty(style.FolderGuid))
            {
                return;
            }

            var index = FindIndex(style.FolderGuid);
            if (style.IsEmpty)
            {
                if (index >= 0)
                {
                    _styles.RemoveAt(index);
                }

                return;
            }

            var serialized = new SerializedFolderStyle
            {
                folderGuid = style.FolderGuid,
                hasColor = style.HasColor,
                color = style.Color,
                iconGuid = style.IconGuid
            };
            if (index >= 0)
            {
                _styles[index] = serialized;
            }
            else
            {
                _styles.Add(serialized);
            }
        }

        public void Save()
        {
            Save(true);
        }

        public IReadOnlyList<string> GetRecentIconGuids()
        {
            return DecorationRecentIconHistory.Snapshot(
                _recentIconGuids);
        }

        public void RecordRecentIcon(
            string iconGuid,
            int maximumCount)
        {
            if (_recentIconGuids == null)
            {
                _recentIconGuids =
                    new List<string>();
            }

            DecorationRecentIconHistory.Record(
                _recentIconGuids,
                iconGuid,
                maximumCount);
        }

        public bool RemoveRecentIcon(string iconGuid)
        {
            return DecorationRecentIconHistory.Remove(
                _recentIconGuids,
                iconGuid);
        }

        private int FindIndex(string folderGuid)
        {
            for (var i = 0; i < _styles.Count; i++)
            {
                var style = _styles[i];
                if (style != null &&
                    string.Equals(
                        style.folderGuid,
                        folderGuid,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        [Serializable]
        private sealed class SerializedFolderStyle
        {
            public string folderGuid;
            public bool hasColor;
            public Color color;
            public string iconGuid;
        }
    }
}
