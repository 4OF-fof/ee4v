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
        public void BackgroundStatusOverlayHost_ReleaseRemovesWindowRegistration()
        {
            BackgroundStatusOverlayApi.ResetAllHosts();
            var window = UnityEngine.ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                BackgroundStatusOverlayApi.EnsureHost(window);
                Assert.That(BackgroundStatusOverlayApi.HostCount, Is.EqualTo(1));

                BackgroundStatusOverlayApi.ReleaseHost(window);

                Assert.That(BackgroundStatusOverlayApi.HostCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                BackgroundStatusOverlayApi.ResetAllHosts();
            }
        }
    }
}
