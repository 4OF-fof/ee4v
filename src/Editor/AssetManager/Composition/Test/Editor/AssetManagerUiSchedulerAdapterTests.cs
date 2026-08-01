using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Ee4v.AssetManager.Composition.Tests
{
    public sealed class AssetManagerUiSchedulerAdapterTests
    {
        [UnityTest]
        public IEnumerator RunOnMainThread_FromWorker_UsesUnityMainThread()
        {
            var scheduler =
                new AssetManagerUiSchedulerAdapter();
            var mainThreadId =
                Thread.CurrentThread.ManagedThreadId;
            var callbackThreadId = -1;
            var completed = false;

            ThreadPool.QueueUserWorkItem(_ =>
                scheduler.RunOnMainThread(() =>
                {
                    callbackThreadId =
                        Thread.CurrentThread.ManagedThreadId;
                    completed = true;
                }));

            var remainingFrames = 120;
            while (!completed && remainingFrames-- > 0)
            {
                yield return null;
            }

            Assert.That(completed, Is.True);
            Assert.That(
                callbackThreadId,
                Is.EqualTo(mainThreadId));
        }

        [Test]
        public void RunOnMainThread_FromMainThread_RunsImmediately()
        {
            var scheduler =
                new AssetManagerUiSchedulerAdapter();
            var invoked = false;

            scheduler.RunOnMainThread(() => invoked = true);

            Assert.That(invoked, Is.True);
        }
    }

    public sealed class AssetManagerStartupSyncTests
    {
        [TestCase(false, true, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        public void ManualReload_NotifiesCatalogWhenSyncDidNot(
            bool catalogAlreadyNotified,
            bool manualReloadRequested,
            bool expected)
        {
            Assert.That(
                AssetManagerStartupSync.ShouldNotifyManualReload(
                    catalogAlreadyNotified,
                    manualReloadRequested),
                Is.EqualTo(expected));
        }
    }
}
