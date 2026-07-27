using System;
using System.Collections.Generic;
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
            return (_recentIconGuids ??
                    new List<string>())
                .ToArray();
        }

        public void RecordRecentIcon(
            string iconGuid,
            int maximumCount)
        {
            if (string.IsNullOrEmpty(iconGuid) ||
                maximumCount <= 0)
            {
                return;
            }

            if (_recentIconGuids == null)
            {
                _recentIconGuids =
                    new List<string>();
            }

            _recentIconGuids.RemoveAll(
                guid => string.Equals(
                    guid,
                    iconGuid,
                    StringComparison.Ordinal));
            _recentIconGuids.Insert(0, iconGuid);
            if (_recentIconGuids.Count > maximumCount)
            {
                _recentIconGuids.RemoveRange(
                    maximumCount,
                    _recentIconGuids.Count -
                    maximumCount);
            }
        }

        public bool RemoveRecentIcon(string iconGuid)
        {
            if (string.IsNullOrEmpty(iconGuid) ||
                _recentIconGuids == null)
            {
                return false;
            }

            return _recentIconGuids.RemoveAll(
                guid => string.Equals(
                    guid,
                    iconGuid,
                    StringComparison.Ordinal)) > 0;
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
