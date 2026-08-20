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

        internal string GetGroupId(string windowTypeId)
        {
            if (string.IsNullOrWhiteSpace(windowTypeId))
            {
                return null;
            }

            foreach (var group in _document.Groups)
            {
                if (group.WindowTypeIds.Contains(windowTypeId))
                {
                    return group.Id;
                }
            }

            return null;
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

        internal bool AssignWindowType(
            string windowTypeId,
            string groupId)
        {
            if (string.IsNullOrWhiteSpace(windowTypeId))
            {
                throw new ArgumentException(
                    "A window type ID is required.",
                    nameof(windowTypeId));
            }

            var target = string.IsNullOrWhiteSpace(groupId)
                ? null
                : GetGroup(groupId);
            if (!string.IsNullOrWhiteSpace(groupId) && target == null)
            {
                throw new ArgumentException(
                    "The window group does not exist.",
                    nameof(groupId));
            }

            var currentGroupId = GetGroupId(windowTypeId);
            if (string.Equals(
                    currentGroupId,
                    groupId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var group in _document.Groups)
            {
                group.WindowTypeIds.RemoveAll(typeId =>
                    string.Equals(
                        typeId,
                        windowTypeId,
                        StringComparison.Ordinal));
            }

            target?.WindowTypeIds.Add(windowTypeId);
            Persist();
            return true;
        }

        private void Normalize()
        {
            _document.Groups.RemoveAll(group => group == null);
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            var groupNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var windowTypeIds = new HashSet<string>(
                StringComparer.Ordinal);

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
                group.WindowTypeIds.RemoveAll(typeId =>
                    string.IsNullOrWhiteSpace(typeId) ||
                    !windowTypeIds.Add(typeId));
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
