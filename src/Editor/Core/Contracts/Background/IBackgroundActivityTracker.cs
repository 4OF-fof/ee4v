using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ee4v.Core.Background
{
    public enum BackgroundTaskStatus
    {
        Running,
        CancellationRequested,
        Succeeded,
        Failed,
        Canceled
    }

    public sealed class BackgroundTaskState
    {
        public BackgroundTaskState(
            long id,
            BackgroundTaskStatus status,
            string message,
            string errorMessage,
            DateTimeOffset startedAtUtc,
            DateTimeOffset? finishedAtUtc)
        {
            Id = id;
            Status = status;
            Message = message ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            StartedAtUtc = startedAtUtc;
            FinishedAtUtc = finishedAtUtc;
        }

        public long Id { get; }

        public BackgroundTaskStatus Status { get; }

        public string Message { get; }

        public string ErrorMessage { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public DateTimeOffset? FinishedAtUtc { get; }

        public bool IsActive =>
            Status == BackgroundTaskStatus.Running ||
            Status == BackgroundTaskStatus.CancellationRequested;
    }

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

    public interface IBackgroundTaskContext
    {
        CancellationToken CancellationToken { get; }

        void Report(string message);
    }

    public interface IBackgroundTaskHandle : IDisposable
    {
        long Id { get; }

        Task Completion { get; }

        BackgroundTaskState State { get; }

        void Cancel();
    }

    public interface IBackgroundActivityTracker
    {
        IDisposable Begin(string message);

        BackgroundActivityState GetState();

        void Clear();
    }

    public interface IBackgroundTaskManager : IBackgroundActivityTracker
    {
        IBackgroundTaskHandle Run(
            string message,
            Func<IBackgroundTaskContext, Task> operation);

        IReadOnlyList<BackgroundTaskState> GetTasks();

        bool TryGetTask(long id, out BackgroundTaskState state);

        void ClearCompleted();
    }
}
