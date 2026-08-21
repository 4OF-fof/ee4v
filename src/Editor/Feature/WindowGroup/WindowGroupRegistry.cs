using System;
using System.Collections.Generic;

namespace Ee4v.WindowGroup
{
    internal readonly struct WindowGroupMembership
    {
        internal WindowGroupMembership(
            string groupId,
            bool isFollower)
        {
            GroupId = groupId;
            IsFollower = isFollower;
        }

        internal string GroupId { get; }

        internal bool IsFollower { get; }
    }

    internal sealed class WindowGroupRegistry<TWindow>
        where TWindow : class
    {
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _managedEntries =
            new List<Entry>();
        private long _nextRegistrationId;

        internal IDisposable Register(
            TWindow window,
            string groupId,
            bool isFollower = false)
        {
            if (!isFollower && HasRegularMembership(
                    _entries,
                    window,
                    groupId))
            {
                throw new InvalidOperationException(
                    "A window can be a regular member of only one group.");
            }

            Remove(_entries, window, groupId);
            var registrationId = ++_nextRegistrationId;
            _entries.Add(new Entry(
                window,
                groupId,
                isFollower,
                registrationId));
            return new Registration(
                () => Remove(window, registrationId));
        }

        internal bool TryGetRegularGroupId(
            TWindow window,
            out string groupId)
        {
            var entries = Find(window) == null
                ? _managedEntries
                : _entries;
            var entry = entries.Find(candidate =>
                ReferenceEquals(candidate.Window, window) &&
                !candidate.IsFollower);
            groupId = entry?.GroupId;
            return entry != null;
        }

        internal IReadOnlyList<TWindow> GetWindows(string groupId)
        {
            var windows = new List<TWindow>();
            foreach (var entry in _entries)
            {
                if (string.Equals(
                        entry.GroupId,
                        groupId,
                        StringComparison.Ordinal))
                {
                    windows.Add(entry.Window);
                }
            }

            foreach (var entry in _managedEntries)
            {
                if (Find(entry.Window) == null &&
                    string.Equals(
                        entry.GroupId,
                        groupId,
                        StringComparison.Ordinal))
                {
                    windows.Add(entry.Window);
                }
            }

            return windows;
        }

        internal void SetManaged(
            TWindow window,
            IReadOnlyList<WindowGroupMembership> memberships)
        {
            var hasRegularMembership = false;
            for (var i = 0; i < memberships.Count; i++)
            {
                if (!memberships[i].IsFollower &&
                    hasRegularMembership)
                {
                    throw new ArgumentException(
                        "A window can be a regular member of only one group.",
                        nameof(memberships));
                }

                hasRegularMembership |= !memberships[i].IsFollower;
            }

            Remove(_managedEntries, window);
            for (var i = 0; i < memberships.Count; i++)
            {
                var membership = memberships[i];
                _managedEntries.Add(new Entry(
                    window,
                    membership.GroupId,
                    membership.IsFollower,
                    0));
            }
        }

        internal void RemoveWhere(Predicate<TWindow> predicate)
        {
            _entries.RemoveAll(entry => predicate(entry.Window));
            _managedEntries.RemoveAll(entry =>
                predicate(entry.Window));
        }

        private Entry Find(TWindow window)
        {
            return Find(_entries, window);
        }

        private static Entry Find(
            List<Entry> entries,
            TWindow window)
        {
            return entries.Find(entry =>
                ReferenceEquals(entry.Window, window));
        }

        private static void Remove(
            List<Entry> entries,
            TWindow window)
        {
            entries.RemoveAll(entry =>
                ReferenceEquals(entry.Window, window));
        }

        private static void Remove(
            List<Entry> entries,
            TWindow window,
            string groupId)
        {
            entries.RemoveAll(entry =>
                ReferenceEquals(entry.Window, window) &&
                string.Equals(
                    entry.GroupId,
                    groupId,
                    StringComparison.Ordinal));
        }

        private static bool HasRegularMembership(
            List<Entry> entries,
            TWindow window,
            string excludedGroupId)
        {
            return entries.Exists(entry =>
                ReferenceEquals(entry.Window, window) &&
                !entry.IsFollower &&
                !string.Equals(
                    entry.GroupId,
                    excludedGroupId,
                    StringComparison.Ordinal));
        }

        private void Remove(TWindow window, long registrationId)
        {
            _entries.RemoveAll(entry =>
                ReferenceEquals(entry.Window, window) &&
                entry.RegistrationId == registrationId);
        }

        private sealed class Entry
        {
            internal Entry(
                TWindow window,
                string groupId,
                bool isFollower,
                long registrationId)
            {
                Window = window;
                GroupId = groupId;
                IsFollower = isFollower;
                RegistrationId = registrationId;
            }

            internal TWindow Window { get; }

            internal string GroupId { get; }

            internal bool IsFollower { get; }

            internal long RegistrationId { get; }
        }

        private sealed class Registration : IDisposable
        {
            private Action _dispose;

            internal Registration(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }
    }
}
