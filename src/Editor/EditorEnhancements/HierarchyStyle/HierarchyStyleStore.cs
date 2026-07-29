using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    [FilePath(
        "UserSettings/ee4v.hierarchy-styles.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class HierarchyStyleStore
        : ScriptableSingleton<HierarchyStyleStore>,
          IHierarchyStyleRepository
    {
        [SerializeField]
        private List<SerializedHierarchyStyle> _styles =
            new List<SerializedHierarchyStyle>();

        [SerializeField]
        private List<string> _recentIconGuids =
            new List<string>();

        public HierarchyStyleValue Get(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            for (var i = 0; i < _styles.Count; i++)
            {
                var style = _styles[i];
                if (style != null &&
                    string.Equals(
                        style.objectId,
                        objectId,
                        StringComparison.Ordinal))
                {
                    return new HierarchyStyleValue(
                        style.objectId,
                        style.hasBackgroundColor,
                        style.backgroundColor,
                        style.iconGuid);
                }
            }

            return null;
        }

        public void Put(HierarchyStyleValue style)
        {
            if (style == null ||
                string.IsNullOrEmpty(style.ObjectId))
            {
                return;
            }

            var index = FindIndex(style.ObjectId);
            if (style.IsEmpty)
            {
                if (index >= 0)
                {
                    _styles.RemoveAt(index);
                }

                return;
            }

            var serialized = new SerializedHierarchyStyle
            {
                objectId = style.ObjectId,
                hasBackgroundColor =
                    style.HasBackgroundColor,
                backgroundColor = style.BackgroundColor,
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

        public void Save()
        {
            Save(true);
        }

        private int FindIndex(string objectId)
        {
            for (var i = 0; i < _styles.Count; i++)
            {
                var style = _styles[i];
                if (style != null &&
                    string.Equals(
                        style.objectId,
                        objectId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        [Serializable]
        private sealed class SerializedHierarchyStyle
        {
            public string objectId;
            public bool hasBackgroundColor;
            public Color backgroundColor;
            public string iconGuid;
        }
    }
}
