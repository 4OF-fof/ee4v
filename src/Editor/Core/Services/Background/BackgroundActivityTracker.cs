using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ee4v.Core.Background
{
    public sealed class BackgroundActivityTracker
        : IBackgroundTaskManager
    {
        private const int MaximumCompletedTaskCount = 64;
        private readonly object _gate = new object();
        private readonly Dictionary<long, TaskEntry> _tasks =
            new Dictionary<long, TaskEntry>();
        private long _nextId;

        public IDisposable Begin(string message)
        {
            lock (_gate)
            {
                var entry = CreateEntry(message);
                return new ActivityHandle(this, entry);
            }
        }

        public IBackgroundTaskHandle Run(
            string message,
            Func<IBackgroundTaskContext, Task> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            TaskEntry entry;
            BackgroundTaskHandle handle;
            lock (_gate)
            {
                entry = CreateEntry(message);
                handle = new BackgroundTaskHandle(this, entry);
            }

            handle.SetCompletion(ExecuteAsync(entry, handle, operation));
            return handle;
        }

        public BackgroundActivityState GetState()
        {
            lock (_gate)
            {
                var activeTasks = _tasks.Values
                    .Where(task => IsActive(task.Status))
                    .ToArray();
                if (activeTasks.Length == 0)
                {
                    return new BackgroundActivityState(
                        false,
                        string.Empty,
                        0);
                }

                var latest = activeTasks
                    .OrderByDescending(task => task.Id)
                    .First();

                return new BackgroundActivityState(
                    true,
                    latest.Message,
                    activeTasks.Length);
            }
        }

        public IReadOnlyList<BackgroundTaskState> GetTasks()
        {
            lock (_gate)
            {
                return _tasks.Values
                    .OrderBy(task => task.Id)
                    .Select(CreateState)
                    .ToArray();
            }
        }

        public bool TryGetTask(
            long id,
            out BackgroundTaskState state)
        {
            lock (_gate)
            {
                if (_tasks.TryGetValue(id, out var entry))
                {
                    state = CreateState(entry);
                    return true;
                }

                state = null;
                return false;
            }
        }

        public void ClearCompleted()
        {
            lock (_gate)
            {
                var completedIds = _tasks.Values
                    .Where(task => !IsActive(task.Status))
                    .Select(task => task.Id)
                    .ToArray();
                for (var i = 0; i < completedIds.Length; i++)
                {
                    _tasks.Remove(completedIds[i]);
                }
            }
        }

        public void Clear()
        {
            CancellationTokenSource[] cancellations;
            lock (_gate)
            {
                var activeTasks = _tasks.Values
                    .Where(task => IsActive(task.Status))
                    .ToArray();
                for (var i = 0; i < activeTasks.Length; i++)
                {
                    activeTasks[i].Status =
                        BackgroundTaskStatus.CancellationRequested;
                }

                cancellations = activeTasks
                    .Select(task => task.Cancellation)
                    .ToArray();
                _tasks.Clear();
            }

            for (var i = 0; i < cancellations.Length; i++)
            {
                cancellations[i].Cancel();
            }
        }

        private TaskEntry CreateEntry(string message)
        {
            var entry = new TaskEntry(
                ++_nextId,
                message ?? string.Empty,
                DateTimeOffset.UtcNow);
            _tasks.Add(entry.Id, entry);
            return entry;
        }

        private async Task ExecuteAsync(
            TaskEntry entry,
            IBackgroundTaskContext context,
            Func<IBackgroundTaskContext, Task> operation)
        {
            try
            {
                await operation(context);
                Finish(entry, BackgroundTaskStatus.Succeeded, string.Empty);
            }
            catch (OperationCanceledException)
            {
                Finish(entry, BackgroundTaskStatus.Canceled, string.Empty);
                throw;
            }
            catch (Exception exception)
            {
                Finish(
                    entry,
                    BackgroundTaskStatus.Failed,
                    exception.Message);
                throw;
            }
        }

        private void Report(TaskEntry entry, string message)
        {
            lock (_gate)
            {
                if (!IsActive(entry.Status))
                {
                    return;
                }

                entry.Message = message ?? string.Empty;
            }
        }

        private void RequestCancellation(TaskEntry entry)
        {
            var shouldCancel = false;
            lock (_gate)
            {
                if (entry.Status == BackgroundTaskStatus.Running)
                {
                    entry.Status =
                        BackgroundTaskStatus.CancellationRequested;
                    shouldCancel = true;
                }
            }

            if (shouldCancel)
            {
                entry.Cancellation.Cancel();
            }
        }

        private void Finish(
            TaskEntry entry,
            BackgroundTaskStatus status,
            string errorMessage)
        {
            lock (_gate)
            {
                if (!IsActive(entry.Status))
                {
                    return;
                }

                entry.Status = status;
                entry.ErrorMessage = errorMessage ?? string.Empty;
                entry.FinishedAtUtc = DateTimeOffset.UtcNow;
                TrimCompletedTasks();
            }
        }

        private BackgroundTaskState GetState(TaskEntry entry)
        {
            lock (_gate)
            {
                return CreateState(entry);
            }
        }

        private void TrimCompletedTasks()
        {
            var excessIds = _tasks.Values
                .Where(task => !IsActive(task.Status))
                .OrderBy(task => task.Id)
                .Take(Math.Max(
                    0,
                    _tasks.Values.Count(
                        task => !IsActive(task.Status)) -
                    MaximumCompletedTaskCount))
                .Select(task => task.Id)
                .ToArray();
            for (var i = 0; i < excessIds.Length; i++)
            {
                _tasks.Remove(excessIds[i]);
            }
        }

        private static bool IsActive(BackgroundTaskStatus status)
        {
            return status == BackgroundTaskStatus.Running ||
                   status ==
                   BackgroundTaskStatus.CancellationRequested;
        }

        private static BackgroundTaskState CreateState(TaskEntry entry)
        {
            return new BackgroundTaskState(
                entry.Id,
                entry.Status,
                entry.Message,
                entry.ErrorMessage,
                entry.StartedAtUtc,
                entry.FinishedAtUtc);
        }

        private sealed class ActivityHandle : IDisposable
        {
            private BackgroundActivityTracker _owner;
            private readonly TaskEntry _entry;

            public ActivityHandle(
                BackgroundActivityTracker owner,
                TaskEntry entry)
            {
                _owner = owner;
                _entry = entry;
            }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.Finish(
                    _entry,
                    BackgroundTaskStatus.Succeeded,
                    string.Empty);
            }
        }

        private sealed class BackgroundTaskHandle
            : IBackgroundTaskHandle, IBackgroundTaskContext
        {
            private readonly BackgroundActivityTracker _owner;
            private readonly TaskEntry _entry;
            private Task _completion = Task.CompletedTask;

            public BackgroundTaskHandle(
                BackgroundActivityTracker owner,
                TaskEntry entry)
            {
                _owner = owner;
                _entry = entry;
            }

            public long Id => _entry.Id;

            public Task Completion => _completion;

            public BackgroundTaskState State =>
                _owner.GetState(_entry);

            public CancellationToken CancellationToken =>
                _entry.Cancellation.Token;

            public void Report(string message)
            {
                _owner.Report(_entry, message);
            }

            public void Cancel()
            {
                _owner.RequestCancellation(_entry);
            }

            public void Dispose()
            {
                Cancel();
            }

            public void SetCompletion(Task completion)
            {
                _completion = completion ?? Task.CompletedTask;
            }
        }

        private sealed class TaskEntry
        {
            public TaskEntry(
                long id,
                string message,
                DateTimeOffset startedAtUtc)
            {
                Id = id;
                Message = message;
                StartedAtUtc = startedAtUtc;
                Status = BackgroundTaskStatus.Running;
                Cancellation = new CancellationTokenSource();
            }

            public long Id { get; }

            public string Message { get; set; }

            public string ErrorMessage { get; set; } = string.Empty;

            public DateTimeOffset StartedAtUtc { get; }

            public DateTimeOffset? FinishedAtUtc { get; set; }

            public BackgroundTaskStatus Status { get; set; }

            public CancellationTokenSource Cancellation { get; }
        }
    }
}
