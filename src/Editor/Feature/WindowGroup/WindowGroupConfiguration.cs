using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.WindowGroup
{
    [Serializable]
    internal sealed class WindowGroupDocument
    {
        [SerializeField]
        private List<WindowGroupDefinition> _groups =
            new List<WindowGroupDefinition>();

        internal List<WindowGroupDefinition> Groups
        {
            get
            {
                if (_groups == null)
                {
                    _groups = new List<WindowGroupDefinition>();
                }

                return _groups;
            }
        }
    }

    [Serializable]
    internal sealed class WindowGroupDefinition
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _name;

        [SerializeField]
        private List<string> _windowTypeIds = new List<string>();

        [SerializeField]
        private List<string> _followerWindowTypeIds =
            new List<string>();

        internal WindowGroupDefinition(string id, string name)
        {
            _id = id;
            _name = name;
        }

        internal string Id
        {
            get { return _id; }
            set { _id = value; }
        }

        internal string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        internal List<string> WindowTypeIds
        {
            get
            {
                if (_windowTypeIds == null)
                {
                    _windowTypeIds = new List<string>();
                }

                return _windowTypeIds;
            }
        }

        internal List<string> FollowerWindowTypeIds
        {
            get
            {
                if (_followerWindowTypeIds == null)
                {
                    _followerWindowTypeIds = new List<string>();
                }

                return _followerWindowTypeIds;
            }
        }
    }

    internal interface IWindowGroupStore
    {
        WindowGroupDocument Load();

        void Save(WindowGroupDocument document);
    }

    internal sealed class EditorPrefsWindowGroupStore
        : IWindowGroupStore
    {
        private const string Key =
            "dev.4of.ee4v.windowGroup.configuration";

        public WindowGroupDocument Load()
        {
            var json = EditorPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new WindowGroupDocument();
            }

            try
            {
                return JsonUtility.FromJson<WindowGroupDocument>(json) ??
                       new WindowGroupDocument();
            }
            catch (ArgumentException)
            {
                return new WindowGroupDocument();
            }
        }

        public void Save(WindowGroupDocument document)
        {
            EditorPrefs.SetString(Key, JsonUtility.ToJson(document));
        }
    }

    internal sealed class WindowGroupConfiguration
    {
        private readonly IWindowGroupStore _store;
        private readonly WindowGroupDocument _document;

        internal WindowGroupConfiguration(IWindowGroupStore store)
        {
            _store = store ??
                throw new ArgumentNullException(nameof(store));
            _document = store.Load() ?? new WindowGroupDocument();
            Normalize();
        }

        internal event Action Changed;

        internal IReadOnlyList<WindowGroupDefinition> Groups
        {
            get { return _document.Groups; }
        }

        internal WindowGroupDefinition GetGroup(string groupId)
        {
            return _document.Groups.Find(group =>
                string.Equals(
                    group.Id,
                    groupId,
                    StringComparison.Ordinal));
        }

        internal bool IsAssigned(
            string windowTypeId,
            string groupId)
        {
            var group = GetGroup(groupId);
            return group != null &&
                   group.WindowTypeIds.Contains(windowTypeId);
        }

        internal bool IsFollower(
            string windowTypeId,
            string groupId)
        {
            var group = GetGroup(groupId);
            return group != null &&
                   group.FollowerWindowTypeIds.Contains(windowTypeId);
        }

        internal bool HasRegularMembershipInOtherGroup(
            string windowTypeId,
            string groupId)
        {
            return _document.Groups.Exists(group =>
                !string.Equals(
                    group.Id,
                    groupId,
                    StringComparison.Ordinal) &&
                group.WindowTypeIds.Contains(windowTypeId) &&
                !group.FollowerWindowTypeIds.Contains(windowTypeId));
        }

        internal IReadOnlyList<WindowGroupMembership> GetMemberships(
            string windowTypeId)
        {
            var memberships = new List<WindowGroupMembership>();
            foreach (var group in _document.Groups)
            {
                if (group.WindowTypeIds.Contains(windowTypeId))
                {
                    memberships.Add(new WindowGroupMembership(
                        group.Id,
                        group.FollowerWindowTypeIds.Contains(
                            windowTypeId)));
                }
            }

            return memberships;
        }

        internal string CreateGroup(string baseName)
        {
            var name = CreateUniqueName(
                string.IsNullOrWhiteSpace(baseName)
                    ? "Group"
                    : baseName.Trim(),
                null);
            var group = new WindowGroupDefinition(
                Guid.NewGuid().ToString("N"),
                name);
            _document.Groups.Add(group);
            Persist();
            return group.Id;
        }

        internal bool RenameGroup(string groupId, string name)
        {
            var group = GetGroup(groupId);
            var normalizedName = name?.Trim();
            if (group == null ||
                string.IsNullOrWhiteSpace(normalizedName) ||
                HasName(normalizedName, groupId))
            {
                return false;
            }

            if (string.Equals(
                    group.Name,
                    normalizedName,
                    StringComparison.Ordinal))
            {
                return true;
            }

            group.Name = normalizedName;
            Persist();
            return true;
        }

        internal bool DeleteGroup(string groupId)
        {
            var removed = _document.Groups.RemoveAll(group =>
                string.Equals(
                    group.Id,
                    groupId,
                    StringComparison.Ordinal)) > 0;
            if (removed)
            {
                Persist();
            }

            return removed;
        }

        internal bool SetWindowTypeAssigned(
            string windowTypeId,
            string groupId,
            bool isAssigned)
        {
            if (string.IsNullOrWhiteSpace(windowTypeId))
            {
                throw new ArgumentException(
                    "A window type ID is required.",
                    nameof(windowTypeId));
            }

            var group = GetGroup(groupId);
            if (group == null)
            {
                throw new ArgumentException(
                    "The window group does not exist.",
                    nameof(groupId));
            }

            if (group.WindowTypeIds.Contains(windowTypeId) == isAssigned)
            {
                return false;
            }

            if (isAssigned)
            {
                group.WindowTypeIds.Add(windowTypeId);
                if (HasRegularMembershipInOtherGroup(
                        windowTypeId,
                        groupId))
                {
                    group.FollowerWindowTypeIds.Add(windowTypeId);
                }
            }
            else
            {
                group.WindowTypeIds.RemoveAll(typeId =>
                    string.Equals(
                        typeId,
                        windowTypeId,
                        StringComparison.Ordinal));
                group.FollowerWindowTypeIds.RemoveAll(typeId =>
                    string.Equals(
                        typeId,
                        windowTypeId,
                        StringComparison.Ordinal));
            }

            Persist();
            return true;
        }

        internal bool SetFollower(
            string windowTypeId,
            string groupId,
            bool isFollower)
        {
            var group = GetGroup(groupId);
            if (group == null ||
                !group.WindowTypeIds.Contains(windowTypeId) ||
                group.FollowerWindowTypeIds.Contains(windowTypeId) ==
                isFollower ||
                (!isFollower && HasRegularMembershipInOtherGroup(
                     windowTypeId,
                     groupId)))
            {
                return false;
            }

            if (isFollower)
            {
                group.FollowerWindowTypeIds.Add(windowTypeId);
            }
            else
            {
                group.FollowerWindowTypeIds.RemoveAll(typeId =>
                    string.Equals(
                        typeId,
                        windowTypeId,
                        StringComparison.Ordinal));
            }

            Persist();
            return true;
        }

        private void Normalize()
        {
            _document.Groups.RemoveAll(group => group == null);
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            var groupNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var group in _document.Groups)
            {
                if (string.IsNullOrWhiteSpace(group.Id) ||
                    !groupIds.Add(group.Id))
                {
                    group.Id = Guid.NewGuid().ToString("N");
                    groupIds.Add(group.Id);
                }

                var baseName = string.IsNullOrWhiteSpace(group.Name)
                    ? "Group"
                    : group.Name.Trim();
                var name = baseName;
                var suffix = 2;
                while (!groupNames.Add(name))
                {
                    name = baseName + " " + suffix++;
                }

                group.Name = name;
                var windowTypeIds = new HashSet<string>(
                    StringComparer.Ordinal);
                group.WindowTypeIds.RemoveAll(typeId =>
                    string.IsNullOrWhiteSpace(typeId) ||
                    !windowTypeIds.Add(typeId));
                var followerWindowTypeIds =
                    new HashSet<string>(StringComparer.Ordinal);
                group.FollowerWindowTypeIds.RemoveAll(typeId =>
                    !group.WindowTypeIds.Contains(typeId) ||
                    !followerWindowTypeIds.Add(typeId));
            }
        }

        private string CreateUniqueName(
            string baseName,
            string excludedGroupId)
        {
            var name = baseName;
            var suffix = 2;
            while (HasName(name, excludedGroupId))
            {
                name = baseName + " " + suffix++;
            }

            return name;
        }

        private bool HasName(string name, string excludedGroupId)
        {
            return _document.Groups.Exists(group =>
                !string.Equals(
                    group.Id,
                    excludedGroupId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    group.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void Persist()
        {
            _store.Save(_document);
            Changed?.Invoke();
        }
    }
}
