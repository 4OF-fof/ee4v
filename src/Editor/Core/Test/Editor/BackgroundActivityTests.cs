using System;
using System.Threading.Tasks;
using Ee4v.Core.Background;
using NUnit.Framework;
using UnityEditor;

namespace Ee4v.Core.Tests
{
    public sealed class BackgroundActivityTests
    {
        [Test]
        public void BeginAndDispose_TracksLatestActiveOperation()
        {
            var tracker = new BackgroundActivityTracker();
            var first = tracker.Begin("first");
            var second = tracker.Begin("second");

            var active = tracker.GetState();
            Assert.That(active.IsActive, Is.True);
            Assert.That(active.ActivityCount, Is.EqualTo(2));
            Assert.That(active.Message, Is.EqualTo("second"));

            second.Dispose();
            active = tracker.GetState();
            Assert.That(active.ActivityCount, Is.EqualTo(1));
            Assert.That(active.Message, Is.EqualTo("first"));

            first.Dispose();
            Assert.That(tracker.GetState().IsActive, Is.False);
        }

        [Test]
        public async Task Run_TracksProgressAndSuccessfulCompletion()
        {
            var tracker = new BackgroundActivityTracker();
            var release = new TaskCompletionSource<object>();
            var task = tracker.Run(
                "starting",
                async context =>
                {
                    context.Report("working");
                    await release.Task;
                });

            var active = tracker.GetState();
            Assert.That(active.IsActive, Is.True);
            Assert.That(active.Message, Is.EqualTo("working"));
            Assert.That(task.State.Status, Is.EqualTo(
                BackgroundTaskStatus.Running));

            release.SetResult(null);
            await task.Completion;

            Assert.That(task.State.Status, Is.EqualTo(
                BackgroundTaskStatus.Succeeded));
            Assert.That(task.State.FinishedAtUtc, Is.Not.Null);
            Assert.That(tracker.GetState().IsActive, Is.False);

            tracker.ClearCompleted();
            Assert.That(tracker.GetTasks(), Is.Empty);
        }

        [Test]
        public async Task Run_RecordsFailure()
        {
            var tracker = new BackgroundActivityTracker();
            var task = tracker.Run(
                "failing",
                _ => Task.FromException(
                    new InvalidOperationException("failure")));

            try
            {
                await task.Completion;
                Assert.Fail("The failed operation must fault completion.");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.That(task.State.Status, Is.EqualTo(
                BackgroundTaskStatus.Failed));
            Assert.That(task.State.ErrorMessage, Is.EqualTo("failure"));
        }

        [Test]
        public async Task Cancel_RequestsCancellationAndRecordsCanceled()
        {
            var tracker = new BackgroundActivityTracker();
            var cancellationObserved = new TaskCompletionSource<object>();
            var task = tracker.Run(
                "waiting",
                async context =>
                {
                    using (context.CancellationToken.Register(
                               () => cancellationObserved.TrySetResult(null)))
                    {
                        await cancellationObserved.Task;
                        context.CancellationToken
                            .ThrowIfCancellationRequested();
                    }
                });

            task.Cancel();
            try
            {
                await task.Completion;
                Assert.Fail("The canceled operation must cancel completion.");
            }
            catch (OperationCanceledException)
            {
            }

            Assert.That(task.State.Status, Is.EqualTo(
                BackgroundTaskStatus.Canceled));
            Assert.That(tracker.GetState().IsActive, Is.False);
        }

        [Test]
        public void BackgroundStatusOverlayHost_ReleaseRemovesWindowRegistration()
        {
            BackgroundStatusOverlay.ResetAllHosts();
            var window = UnityEngine.ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                BackgroundStatusOverlay.Attach(window);
                Assert.That(BackgroundStatusOverlay.HostCount, Is.EqualTo(1));

                BackgroundStatusOverlay.Detach(window);

                Assert.That(BackgroundStatusOverlay.HostCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                BackgroundStatusOverlay.ResetAllHosts();
            }
        }
    }
}
