using System;
using System.Collections.Generic;

namespace Ee4v.Core.Background
{
    public sealed class BackgroundActivityTracker
        : IBackgroundActivityTracker
    {
        private readonly object _gate = new object();
        private readonly Dictionary<long, string> _activities =
            new Dictionary<long, string>();
        private long _nextId;

        public IDisposable Begin(string message)
        {
            lock (_gate)
            {
                var id = ++_nextId;
                _activities[id] = message ?? string.Empty;
                return new ActivityHandle(this, id);
            }
        }

        public BackgroundActivityState GetState()
        {
            lock (_gate)
            {
                if (_activities.Count == 0)
                {
                    return new BackgroundActivityState(
                        false,
                        string.Empty,
                        0);
                }

                var latestId = long.MinValue;
                var latestMessage = string.Empty;
                foreach (var pair in _activities)
                {
                    if (pair.Key <= latestId)
                    {
                        continue;
                    }

                    latestId = pair.Key;
                    latestMessage = pair.Value;
                }

                return new BackgroundActivityState(
                    true,
                    latestMessage,
                    _activities.Count);
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _activities.Clear();
                _nextId = 0;
            }
        }

        private void Complete(long id)
        {
            lock (_gate)
            {
                _activities.Remove(id);
            }
        }

        private sealed class ActivityHandle : IDisposable
        {
            private BackgroundActivityTracker _owner;
            private readonly long _id;

            public ActivityHandle(
                BackgroundActivityTracker owner,
                long id)
            {
                _owner = owner;
                _id = id;
            }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.Complete(_id);
            }
        }
    }
}
