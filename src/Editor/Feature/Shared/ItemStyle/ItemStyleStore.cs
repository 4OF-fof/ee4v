using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.ItemStyle
{
    internal interface IItemStyleRepository
    {
        ItemStyleValue Get(string scope, string identity);
        void Put(string scope, ItemStyleValue style);
        IReadOnlyList<string> GetRecentIconGuids(string scope);
        void RecordRecentIcon(
            string scope,
            string iconGuid,
            int maximumCount);
        bool RemoveRecentIcon(string scope, string iconGuid);
        void Save();
    }

    [FilePath(
        "UserSettings/ee4v.item-styles.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ItemStyleStore
        : ScriptableSingleton<ItemStyleStore>,
          IItemStyleRepository
    {
        [SerializeField]
        private List<SerializedStyle> _styles =
            new List<SerializedStyle>();

        [SerializeField]
        private List<RecentIcons> _recentIcons =
            new List<RecentIcons>();

        public ItemStyleValue Get(string scope, string identity)
        {
            var index = FindStyle(scope, identity);
            if (index < 0)
            {
                return null;
            }

            var style = _styles[index];
            return new ItemStyleValue(
                style.identity,
                style.hasColor,
                style.color,
                style.iconGuid);
        }

        public void Put(string scope, ItemStyleValue style)
        {
            if (style == null ||
                string.IsNullOrEmpty(scope) ||
                string.IsNullOrEmpty(style.Identity))
            {
                return;
            }

            var index = FindStyle(scope, style.Identity);
            if (style.IsEmpty)
            {
                if (index >= 0)
                {
                    _styles.RemoveAt(index);
                }

                return;
            }

            var serialized = new SerializedStyle
            {
                scope = scope,
                identity = style.Identity,
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

        public IReadOnlyList<string> GetRecentIconGuids(string scope)
        {
            var group = FindRecentIcons(scope);
            return group == null
                ? Array.Empty<string>()
                : group.iconGuids.ToArray();
        }

        public void RecordRecentIcon(
            string scope,
            string iconGuid,
            int maximumCount)
        {
            if (string.IsNullOrEmpty(iconGuid) || maximumCount <= 0)
            {
                return;
            }

            var group = GetOrCreateRecentIcons(scope);
            group.iconGuids.RemoveAll(value =>
                string.Equals(value, iconGuid, StringComparison.Ordinal));
            group.iconGuids.Insert(0, iconGuid);
            if (group.iconGuids.Count > maximumCount)
            {
                group.iconGuids.RemoveRange(
                    maximumCount,
                    group.iconGuids.Count - maximumCount);
            }
        }

        public bool RemoveRecentIcon(string scope, string iconGuid)
        {
            var group = FindRecentIcons(scope);
            return group != null && group.iconGuids.Remove(iconGuid);
        }

        public void Save()
        {
            Save(true);
        }

        private int FindStyle(string scope, string identity)
        {
            if (string.IsNullOrEmpty(scope) ||
                string.IsNullOrEmpty(identity))
            {
                return -1;
            }

            for (var i = 0; i < _styles.Count; i++)
            {
                var style = _styles[i];
                if (style != null &&
                    string.Equals(style.scope, scope, StringComparison.Ordinal) &&
                    string.Equals(
                        style.identity,
                        identity,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private RecentIcons GetOrCreateRecentIcons(string scope)
        {
            var group = FindRecentIcons(scope);
            if (group != null)
            {
                return group;
            }

            group = new RecentIcons { scope = scope };
            _recentIcons.Add(group);
            return group;
        }

        private RecentIcons FindRecentIcons(string scope)
        {
            for (var i = 0; i < _recentIcons.Count; i++)
            {
                var group = _recentIcons[i];
                if (group != null &&
                    string.Equals(group.scope, scope, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            return null;
        }

        [Serializable]
        private sealed class SerializedStyle
        {
            public string scope;
            public string identity;
            public bool hasColor;
            public Color color;
            public string iconGuid;
        }

        [Serializable]
        private sealed class RecentIcons
        {
            public string scope;
            public List<string> iconGuids = new List<string>();
        }
    }
}
