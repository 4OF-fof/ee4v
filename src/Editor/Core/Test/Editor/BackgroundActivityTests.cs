using System;
using System.Collections;
using System.Threading.Tasks;
using Ee4v.Core.Background;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

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

        [UnityTest]
        public IEnumerator Run_TracksProgressAndSuccessfulCompletion()
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
            yield return WaitForCompletion(task.Completion);

            Assert.That(task.State.Status, Is.EqualTo(
                BackgroundTaskStatus.Succeeded));
            Assert.That(task.State.FinishedAtUtc, Is.Not.Null);
            Assert.That(tracker.GetState().IsActive, Is.False);

            tracker.ClearCompleted();
            Assert.That(tracker.GetTasks(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Run_RecordsFailure()
        {
            var tracker = new BackgroundActivityTracker();
            var task = tracker.Run(
                "failing",
                _ => Task.FromException(
                    new InvalidOperationException("failure")));

            yield return WaitForCompletion(task.Completion);

            Assert.That(task.Completion.IsFaulted, Is.True);
            Assert.That(task.State.Status, Is.EqualTo(
                BackgroundTaskStatus.Failed));
            Assert.That(task.State.ErrorMessage, Is.EqualTo("failure"));
        }

        [UnityTest]
        public IEnumerator Cancel_RequestsCancellationAndRecordsCanceled()
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
            yield return WaitForCompletion(task.Completion);

            Assert.That(task.Completion.IsCanceled, Is.True);
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

        private static IEnumerator WaitForCompletion(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }
    }
}
