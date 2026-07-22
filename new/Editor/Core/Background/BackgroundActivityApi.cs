using System;
using System.Collections.Generic;

namespace Ee4v.Core.Background
{
    internal sealed class BackgroundActivityState
    {
        public BackgroundActivityState(bool isActive, string message, int activityCount)
        {
            IsActive = isActive;
            Message = message ?? string.Empty;
            ActivityCount = Math.Max(0, activityCount);
        }

        public bool IsActive { get; }

        public string Message { get; }

        public int ActivityCount { get; }
    }

    internal static class BackgroundActivityApi
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<long, string> Activities = new Dictionary<long, string>();
        private static long _nextId;

        public static IDisposable Begin(string message)
        {
            lock (Gate)
            {
                var id = ++_nextId;
                Activities[id] = message ?? string.Empty;
                return new ActivityHandle(id);
            }
        }

        public static BackgroundActivityState GetState()
        {
            lock (Gate)
            {
                if (Activities.Count == 0)
                {
                    return new BackgroundActivityState(false, string.Empty, 0);
                }

                var latestId = long.MinValue;
                var latestMessage = string.Empty;
                foreach (var pair in Activities)
                {
                    if (pair.Key <= latestId)
                    {
                        continue;
                    }

                    latestId = pair.Key;
                    latestMessage = pair.Value;
                }

                return new BackgroundActivityState(true, latestMessage, Activities.Count);
            }
        }

        internal static void Reset()
        {
            lock (Gate)
            {
                Activities.Clear();
                _nextId = 0;
            }
        }

        private static void Complete(long id)
        {
            lock (Gate)
            {
                Activities.Remove(id);
            }
        }

        private sealed class ActivityHandle : IDisposable
        {
            private readonly long _id;
            private bool _disposed;

            public ActivityHandle(long id)
            {
                _id = id;
            }

            public void Dispose()
            {
                lock (Gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                }

                Complete(_id);
            }
        }
    }
}
