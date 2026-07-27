using System;

namespace Ee4v.Core.Background
{
    public sealed class BackgroundActivityState
    {
        public BackgroundActivityState(
            bool isActive,
            string message,
            int activityCount)
        {
            IsActive = isActive;
            Message = message ?? string.Empty;
            ActivityCount = Math.Max(0, activityCount);
        }

        public bool IsActive { get; }

        public string Message { get; }

        public int ActivityCount { get; }
    }

    public interface IBackgroundActivityTracker
    {
        IDisposable Begin(string message);

        BackgroundActivityState GetState();

        void Clear();
    }
}
