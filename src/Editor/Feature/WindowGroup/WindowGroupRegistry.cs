using System;
using System.Collections.Generic;

namespace Ee4v.WindowGroup
{
    internal sealed class WindowGroupRegistry<TWindow>
        where TWindow : class
    {
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _managedEntries =
            new List<Entry>();
        private long _nextRegistrationId;

        internal IDisposable Register(
            TWindow window,
            string groupId)
        {
            Remove(window);
            var registrationId = ++_nextRegistrationId;
            _entries.Add(new Entry(
                window,
                groupId,
                registrationId));
            return new Registration(
                () => Remove(window, registrationId));
        }

        internal bool TryGetGroupId(
            TWindow window,
            out string groupId)
        {
            var entry = Find(window);
            if (entry == null)
            {
                entry = Find(_managedEntries, window);
            }

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
            string groupId)
        {
            Remove(_managedEntries, window);
            if (!string.IsNullOrWhiteSpace(groupId))
            {
                _managedEntries.Add(new Entry(window, groupId, 0));
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

        private void Remove(TWindow window)
        {
            Remove(_entries, window);
        }

        private static void Remove(
            List<Entry> entries,
            TWindow window)
        {
            entries.RemoveAll(entry =>
                ReferenceEquals(entry.Window, window));
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
                long registrationId)
            {
                Window = window;
                GroupId = groupId;
                RegistrationId = registrationId;
            }

            internal TWindow Window { get; }

            internal string GroupId { get; }

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
